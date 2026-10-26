using System.Net.Http.Json;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpBookingService(
    ApiClient api,
    IAuthService auth,
    IRoomService rooms,
    INotificationService notifications,
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
        try
        {
            var id = IdAdapter.ToInt(bookingId);
            var dto = await api.GetAsync<ApiBookingDto>($"api/Booking/{id}");
            return dto is null ? null : MapBooking(dto);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync() =>
        GetBookingsForCurrentUserScopeAsync(DateTime.Today);

    public async Task<IReadOnlyList<OccupiedSlot>> GetOccupiedSlotsAsync(string roomId, DateTime day)
    {
        var roomIds = RoomCombinations.IsCombinedOption(roomId)
            ? new[] { RoomCombinations.ThingamajikId, RoomCombinations.WhachamacallitId }
            : new[] { roomId };

        var fromApi = new List<OccupiedSlot>();
        var sawEndpoint = false;
        foreach (var id in roomIds)
        {
            var apiRoomId = catalog.RoomApiId(id);
            if (apiRoomId is null)
                continue;

            var dtos = await api.GetAsync<List<ApiOccupiedSlotDto>>(
                $"api/booking/occupied?boardroomId={apiRoomId.Value}&date={day:yyyy-MM-dd}");
            if (dtos is null)
                continue;

            sawEndpoint = true;
            fromApi.AddRange(dtos.Select(d => new OccupiedSlot
            {
                Start = d.StartTime.ToTimeSpan(),
                End = d.EndTime.ToTimeSpan()
            }));
        }

        if (sawEndpoint)
            return fromApi;

        return await OccupiedFromBookingsAndBlocksAsync(roomIds, day);
    }

    private async Task<IReadOnlyList<OccupiedSlot>> OccupiedFromBookingsAndBlocksAsync(IReadOnlyList<string> roomIds, DateTime day)
    {
        var conflictIds = roomIds.SelectMany(RoomCombinations.ConflictRoomIds).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slots = new List<OccupiedSlot>();

        foreach (var booking in await GetBookingsAsync(day: day))
        {
            if (booking.Status == BookingStatus.Cancelled)
                continue;
            if (!conflictIds.Contains(booking.RoomId))
                continue;
            slots.Add(ClipToDay(booking.Start, booking.End, day));
        }

        foreach (var roomId in roomIds)
        {
            foreach (var block in await GetBlockedPeriodsAsync(roomId))
            {
                if (block.End <= day.Date || block.Start >= day.Date.AddDays(1))
                    continue;
                slots.Add(ClipToDay(block.Start, block.End, day));
            }
        }

        return slots.Where(s => s.End > s.Start).ToList();
    }

    private static OccupiedSlot ClipToDay(DateTime start, DateTime end, DateTime day)
    {
        var dayStart = day.Date;
        var dayEnd = day.Date.AddDays(1);
        var clippedStart = start < dayStart ? dayStart : start;
        var clippedEnd = end > dayEnd ? dayEnd : end;
        return new OccupiedSlot
        {
            Start = clippedStart.TimeOfDay,
            End = clippedEnd.TimeOfDay >= TimeSpan.FromDays(1) ? new TimeSpan(23, 59, 0) : clippedEnd.TimeOfDay
        };
    }

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

        var saved = new List<Booking>();
        foreach (var slug in roomIds)
        {
            var apiRoomId = catalog.RoomApiId(slug);
            if (apiRoomId is null)
                return new BookingResult { Success = false, Message = "Room is not available on the server yet. Refresh and try again." };

            var physical = await rooms.GetRoomAsync(slug);
            var attendeesForRoom = Math.Max(1, request.Attendees);
            if (RoomCombinations.IsCombinedOption(request.RoomId) && physical is { Capacity: > 0 })
                attendeesForRoom = Math.Min(attendeesForRoom, physical.Capacity);

            var body = new ApiCreateBookingDto
            {
                BoardroomId = apiRoomId.Value,
                UserId = user.ApiId,
                BookingDate = DateOnly.FromDateTime(request.Start),
                StartTime = TimeOnly.FromDateTime(request.Start),
                EndTime = TimeOnly.FromDateTime(request.End),
                Company = string.IsNullOrWhiteSpace(request.Company) ? "Flexispace" : request.Company,
                NumberOfAttendees = attendeesForRoom,
                Notes = RoomCombinations.IsCombinedOption(request.RoomId)
                    ? $"[Combined suite · {request.Attendees} people] {request.Notes}".Trim()
                    : request.Notes
            };

            using var response = await api.PostAsJsonAsync("api/Booking", body);
            if (!response.IsSuccessStatusCode)
            {
                var err = await ReadErrorAsync(response);
                var timeClash = err is not null &&
                                err.Contains("overlapping", StringComparison.OrdinalIgnoreCase);
                return new BookingResult
                {
                    Success = false,
                    Message = err ?? "Could not create booking.",
                    SuggestedSlots = timeClash
                        ? SuggestSlots(request.RoomId, request.Start.Date, request.End - request.Start)
                        : []
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ApiBookingDto>(ApiClient.JsonOptions);
            if (dto is not null)
                saved.Add(MapBooking(dto));
        }

        var location = await rooms.GetLocationAsync(room.LocationId);
        if (saved.Count == 0)
            saved.Add(FillFromRequest(null, request, room, location?.Name, user.Name));
        else
        {
            foreach (var item in saved)
            {
                var physical = await rooms.GetRoomAsync(item.RoomId) ?? room;
                FillFromRequest(item, request, physical, location?.Name, user.Name);
            }
        }

        BookingHandoff.Remember(saved);
        await SyncAlertsAsync();
        BookingFeed.Publish();
        return new BookingResult
        {
            Success = true,
            Message = "Your boardroom is booked. Check Alerts for the confirmation.",
            Booking = saved[0]
        };
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        using var response = await api.DeleteAsync($"api/Booking/{IdAdapter.ToInt(bookingId)}");
        if (response.IsSuccessStatusCode)
        {
            await SyncAlertsAsync();
            BookingFeed.Publish();
            return true;
        }

        // Administrators are not allowed to delete, so cancel through status instead.
        return await UpdateBookingStatusAsync(bookingId, BookingStatus.Cancelled);
    }

    public async Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        if (auth.CurrentUser?.Role is UserRole.Staff or UserRole.Client)
            return false;
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
        if (response.IsSuccessStatusCode)
        {
            await SyncAlertsAsync();
            BookingFeed.Publish();
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        if (status is not (BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed))
            return false;

        using var response = await api.PatchAsJsonAsync(
            $"api/Booking/{IdAdapter.ToInt(bookingId)}/status",
            new ApiBookingStatusDto { Status = status.ToString() });
        if (response.IsSuccessStatusCode)
        {
            await SyncAlertsAsync();
            BookingFeed.Publish();
        }
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
            Status = MapStatus(dto.Status)
        };
    }

    private static Booking FillFromRequest(Booking? created, BookingRequest request, Boardroom room, string? locationName, string bookerName)
    {
        created ??= new Booking { Status = BookingStatus.Confirmed };

        if (string.IsNullOrWhiteSpace(created.RoomName) || created.RoomName == created.RoomId)
            created.RoomName = room.Name;
        if (string.IsNullOrWhiteSpace(created.LocationName) || created.LocationName == created.LocationId)
            created.LocationName = string.IsNullOrWhiteSpace(locationName) ? room.LocationId : locationName;
        if (created.Start.Year < 2000)
        {
            created.Start = request.Start;
            created.End = request.End;
        }
        if (string.IsNullOrWhiteSpace(created.BookerName))
            created.BookerName = bookerName;
        if (string.IsNullOrWhiteSpace(created.Company))
            created.Company = request.Company;
        if (created.Attendees <= 0)
            created.Attendees = request.Attendees;
        if (string.IsNullOrWhiteSpace(created.Notes))
            created.Notes = request.Notes ?? string.Empty;
        if (created.Equipment.Count == 0)
            created.Equipment = request.Equipment.ToList();
        if (created.Catering.Count == 0)
            created.Catering = request.Catering.ToList();

        return created;
    }

    /// <summary>
    /// API status numbers are Pending 0, Cancelled 1, Completed 2, Confirmed 3.
    /// The mobile enum uses a different order, so a raw "3" must not become Completed.
    /// </summary>
    private static BookingStatus MapStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return BookingStatus.Confirmed;

        if (int.TryParse(status, out var number))
        {
            return number switch
            {
                0 => BookingStatus.Pending,
                1 => BookingStatus.Cancelled,
                2 => BookingStatus.Completed,
                _ => BookingStatus.Confirmed
            };
        }

        return Enum.TryParse<BookingStatus>(status, true, out var parsed)
            ? parsed
            : BookingStatus.Confirmed;
    }

    private async Task SyncAlertsAsync()
    {
        try
        {
            await notifications.GetUnreadCountAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The booking itself succeeded. The badge refreshes the next time Alerts opens.
        }
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
