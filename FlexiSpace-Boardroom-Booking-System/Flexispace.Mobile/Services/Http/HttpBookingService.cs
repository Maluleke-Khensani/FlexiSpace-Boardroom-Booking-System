using System.Net.Http.Json;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpBookingService(
    ApiClient api,
    IAuthService auth,
    IRoomService rooms,
    CatalogSlugs catalog) : IBookingService
{
    public async Task<IReadOnlyList<Booking>> GetBookingsAsync(string? locationId = null, Guid? userId = null, DateTime? day = null)
    {
        var dtos = await api.GetAsync<List<ApiBookingDto>>("api/Booking") ?? [];
        IEnumerable<Booking> query = dtos.Select(MapBooking);

        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);
        if (userId.HasValue)
            query = query.Where(b => b.BookerId == userId.Value);
        if (day.HasValue)
            query = query.Where(b => b.Start.Date == day.Value.Date);

        return query.OrderBy(b => b.Start).ToList();
    }

    public Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null) =>
        GetBookingsAsync(day: day); // API already scopes GET /api/Booking

    public async Task<Booking?> GetBookingAsync(Guid bookingId)
    {
        var id = IdAdapter.ToInt(bookingId);
        var dto = await api.GetAsync<ApiBookingDto>($"api/Booking/{id}");
        return dto is null ? null : MapBooking(dto);
    }

    public Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync() =>
        GetBookingsForCurrentUserScopeAsync(DateTime.Today);

    public async Task<BookingResult> CreateBookingAsync(BookingRequest request)
    {
        var user = auth.CurrentUser;
        if (user is null)
            return new BookingResult { Success = false, Message = "Please sign in to book a room." };

        if (!RolePermissions.CanBookRooms(user.Role))
            return new BookingResult { Success = false, Message = "Your role cannot create bookings." };

        if (request.Start.Date < DateTime.Today)
            return new BookingResult { Success = false, Message = "Bookings cannot be made for a date before today." };

        if (request.End <= request.Start)
            return new BookingResult { Success = false, Message = "End time must be after start time." };

        var room = await rooms.GetRoomAsync(request.RoomId);
        if (room is null)
            return new BookingResult { Success = false, Message = "Room not found." };

        if (user.Role == UserRole.CentreManager &&
            !string.IsNullOrEmpty(user.LocationId) &&
            room.LocationId != user.LocationId)
        {
            return new BookingResult
            {
                Success = false,
                Message = "Centre Managers can only book rooms at their assigned location."
            };
        }

        // Combined suite: hold both physical rooms in the API.
        var roomIds = RoomCombinations.IsCombinedOption(request.RoomId)
            ? new[] { RoomCombinations.ThingamajikId, RoomCombinations.WhachamacallitId }
            : new[] { request.RoomId };

        Booking? created = null;
        foreach (var slug in roomIds)
        {
            var apiRoomId = catalog.RoomApiId(slug);
            if (apiRoomId is null)
                return new BookingResult { Success = false, Message = "Room is not available on the server yet. Refresh and try again." };

            var body = new ApiCreateBookingDto
            {
                BoardroomId = apiRoomId.Value,
                UserId = user.ApiId,
                BookingDate = DateOnly.FromDateTime(request.Start),
                StartTime = TimeOnly.FromDateTime(request.Start),
                EndTime = TimeOnly.FromDateTime(request.End),
                Company = string.IsNullOrWhiteSpace(request.Company) ? "Flexispace" : request.Company,
                NumberOfAttendees = Math.Max(1, request.Attendees),
                Notes = RoomCombinations.IsCombinedOption(request.RoomId)
                    ? $"[Combined suite] {request.Notes}".Trim()
                    : request.Notes
            };

            using var response = await api.PostAsJsonAsync("api/Booking", body);
            if (!response.IsSuccessStatusCode)
            {
                var err = await ReadErrorAsync(response);
                return new BookingResult
                {
                    Success = false,
                    Message = err ?? "Could not create booking.",
                    SuggestedSlots = SuggestSlots(request.RoomId, request.Start.Date, request.End - request.Start)
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ApiBookingDto>(ApiClient.JsonOptions);
            if (dto is not null)
                created = MapBooking(dto);
        }

        if (created is not null && RoomCombinations.IsCombinedOption(request.RoomId))
        {
            created.RoomId = RoomCombinations.CombinedId;
            created.RoomName = "Thingamajik + Whachamacallit";
        }

        return new BookingResult
        {
            Success = true,
            Message = "Your boardroom is booked. Check Alerts for the confirmation.",
            Booking = created
        };
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        using var response = await api.DeleteAsync($"api/Booking/{IdAdapter.ToInt(bookingId)}");
        if (response.IsSuccessStatusCode)
            return true;

        // Some APIs cancel via status patch.
        return await UpdateBookingStatusAsync(bookingId, BookingStatus.Cancelled);
    }

    public async Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var existing = await GetBookingAsync(bookingId);
        if (existing is null) return false;

        var apiRoomId = catalog.RoomApiId(existing.RoomId);
        if (apiRoomId is null) return false;

        var body = new ApiUpdateBookingDto
        {
            BoardroomId = apiRoomId.Value,
            BookingDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            Company = existing.Company,
            NumberOfAttendees = Math.Max(1, attendees),
            Notes = notes
        };

        using var response = await api.PutAsJsonAsync($"api/Booking/{IdAdapter.ToInt(bookingId)}", body);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        if (status is not (BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed))
            return false;

        using var response = await api.PatchAsJsonAsync(
            $"api/Booking/{IdAdapter.ToInt(bookingId)}/status",
            new ApiBookingStatusDto { Status = status.ToString() });
        return response.IsSuccessStatusCode;
    }

    public async Task<BlockRoomResult> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason, Guid? relatedBookingId = null)
    {
        _ = relatedBookingId;
        var apiRoomId = catalog.RoomApiId(roomId);
        if (apiRoomId is null)
            return new BlockRoomResult { Success = false, Title = "Block failed", Message = "Unknown room." };

        var body = new ApiCreateBlockedPeriodDto
        {
            BoardroomId = apiRoomId.Value,
            StartDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndDate = DateOnly.FromDateTime(end),
            EndTime = TimeOnly.FromDateTime(end),
            Reason = string.IsNullOrWhiteSpace(reason) ? "Blocked by Centre Manager" : reason
        };

        using var response = await api.PostAsJsonAsync("api/BlockedPeriod", body);
        if (!response.IsSuccessStatusCode)
        {
            var err = await ReadErrorAsync(response);
            return new BlockRoomResult { Success = false, Title = "Block failed", Message = err ?? "Could not block room." };
        }

        return new BlockRoomResult
        {
            Success = true,
            Title = "Room blocked",
            Message = $"{start:ddd d MMM HH:mm}–{end:HH:mm} is blocked."
        };
    }

    public async Task<IReadOnlyList<BlockedPeriod>> GetBlockedPeriodsAsync(string roomId)
    {
        var apiRoomId = catalog.RoomApiId(roomId);
        var url = apiRoomId is null ? "api/BlockedPeriod" : $"api/BlockedPeriod?boardroomId={apiRoomId}";
        var items = await api.GetAsync<List<ApiBlockedPeriodDto>>(url) ?? [];
        return items
            .Where(b => catalog.RoomSlugFromApi(b.BoardroomId) == roomId || RoomCombinations.ConflictRoomIds(roomId).Contains(catalog.RoomSlugFromApi(b.BoardroomId)))
            .Select(b => new BlockedPeriod
            {
                Id = IdAdapter.ToGuid(b.Id),
                RoomId = catalog.RoomSlugFromApi(b.BoardroomId),
                Start = b.StartDate.ToDateTime(b.StartTime),
                End = b.EndDate.ToDateTime(b.EndTime),
                Reason = b.Reason,
                CreatedBy = b.CreatedByName
            })
            .ToList();
    }

    private Booking MapBooking(ApiBookingDto dto)
    {
        var roomSlug = catalog.RoomSlugFromApi(dto.BoardroomId);
        var locationSlug = dto.LocationId > 0
            ? catalog.LocationSlugFromApi(dto.LocationId)
            : catalog.LocationSlugForRoom(roomSlug) ?? string.Empty;

        var bookerId = auth.CurrentUser?.ApiId == dto.UserId && auth.CurrentUser is not null
            ? auth.CurrentUser.Id
            : IdAdapter.ToGuid(dto.UserId);

        return new Booking
        {
            Id = IdAdapter.ToGuid(dto.Id),
            RoomId = roomSlug,
            LocationId = locationSlug,
            RoomName = string.IsNullOrWhiteSpace(dto.BoardroomName) ? roomSlug : dto.BoardroomName,
            LocationName = string.IsNullOrWhiteSpace(dto.LocationName) ? locationSlug : dto.LocationName,
            Start = dto.BookingDate.ToDateTime(dto.StartTime),
            End = dto.BookingDate.ToDateTime(dto.EndTime),
            BookerId = bookerId,
            BookerName = dto.UserName,
            Company = dto.Company ?? "Flexispace",
            Attendees = dto.NumberOfAttendees,
            Notes = dto.Notes ?? string.Empty,
            Status = Enum.TryParse<BookingStatus>(dto.Status, true, out var s) ? s : BookingStatus.Confirmed
        };
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(ApiClient.JsonOptions);
            if (body?.Errors is { Count: > 0 })
                return string.Join(" ", body.Errors);
            return body?.Message;
        }
        catch
        {
            return null;
        }
    }

    private List<DateTime> SuggestSlots(string roomId, DateTime day, TimeSpan duration)
    {
        // Lightweight client-side suggestions; conflict accuracy comes from API on create.
        var suggestions = new List<DateTime>();
        for (var hour = 8; hour <= 17 && suggestions.Count < 3; hour++)
        {
            var start = day.Date.AddHours(hour);
            if (start + duration <= day.Date.AddHours(18) && start >= DateTime.Now.AddMinutes(-1))
                suggestions.Add(start);
        }

        _ = roomId;
        return suggestions;
    }
}
