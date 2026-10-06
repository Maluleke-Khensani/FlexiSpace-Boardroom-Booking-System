using System.Net.Http.Json;
using System.Text.Json;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiBookingService(
    FlexiSpaceApiClient api,
    IAuthService auth,
    ApiRoomService rooms,
    BookingSessionCache session) : IBookingService
{
    public async Task<IReadOnlyList<Booking>> GetBookingsAsync(string? locationId = null, Guid? userId = null, DateTime? day = null)
    {
        var mapped = await LoadBookingsAsync();
        IEnumerable<Booking> query = mapped;
        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);
        if (userId.HasValue)
            query = query.Where(b => b.BookerId == userId.Value);
        if (day.HasValue)
            query = query.Where(b => b.Start.Date == day.Value.Date);
        return query.OrderBy(b => b.Start).ToList();
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null)
    {
        var user = auth.CurrentUser;
        if (user is null)
            return [];

        return user.Role switch
        {
            UserRole.Administrator => await GetBookingsAsync(day: day),
            UserRole.CentreManager => await GetBookingsAsync(locationId: user.LocationId, day: day),
            _ => await GetBookingsAsync(userId: user.Id, day: day)
        };
    }

    public async Task<Booking?> GetBookingAsync(Guid bookingId)
    {
        if (IdMap.ToInt(bookingId) <= 0)
            return (await LoadBookingsAsync()).FirstOrDefault(b => b.Id == bookingId);

        var dto = await api.GetAsync<BookingDto>($"api/booking/{IdMap.ToInt(bookingId)}");
        if (dto is null)
            return null;
        return await MapAsync(dto);
    }

    public Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync() =>
        GetBookingsForCurrentUserScopeAsync(DateTime.Today);

    public async Task<IReadOnlyList<OccupiedSlot>> GetOccupiedSlotsAsync(string roomId, DateTime day)
    {
        var room = await rooms.GetRoomAsync(roomId);
        var physicalIds = room?.IsCombined == true && room.CombinedRoomIds.Count > 0
            ? room.CombinedRoomIds
            : [roomId];

        var date = DateOnly.FromDateTime(day);
        var boardroomIds = new HashSet<int>();
        var slots = new List<OccupiedSlot>();

        foreach (var physicalId in physicalIds)
        {
            if (!IdMap.TryToInt(physicalId, out var boardroomId))
                continue;

            boardroomIds.Add(boardroomId);
            try
            {
                var dtos = await api.GetAsync<List<OccupiedSlotDto>>(
                    $"api/booking/occupied?boardroomId={boardroomId}&date={date:yyyy-MM-dd}") ?? [];
                foreach (var dto in dtos)
                {
                    if (TryParseOccupied(dto, out var slot))
                        slots.Add(slot);
                }
            }
            catch
            {
                // Occupied endpoint is missing or unreadable on some API deploys; fall back below.
            }
        }

        try
        {
            var bookings = await api.GetAsync<List<BookingDto>>("api/booking") ?? [];
            foreach (var booking in bookings)
            {
                if (!boardroomIds.Contains(booking.BoardroomId) || booking.BookingDate != date)
                    continue;
                if (booking.Status is 1 or 2)
                    continue;

                slots.Add(new OccupiedSlot
                {
                    Start = booking.StartTime.ToTimeSpan(),
                    End = booking.EndTime.ToTimeSpan()
                });
            }
        }
        catch
        {
            // Keep whatever occupied slots we already collected.
        }

        return slots
            .GroupBy(slot => (Start: FloorToHalfHour(slot.Start), End: slot.End))
            .Select(group => new OccupiedSlot { Start = group.Key.Start, End = group.Key.End })
            .ToList();
    }

    private static bool TryParseOccupied(OccupiedSlotDto dto, out OccupiedSlot slot)
    {
        slot = default!;
        if (!TryParseTime(dto.StartTime, out var start) || !TryParseTime(dto.EndTime, out var end) || end <= start)
            return false;

        slot = new OccupiedSlot { Start = start, End = end };
        return true;
    }

    private static bool TryParseTime(JsonElement value, out TimeSpan time)
    {
        time = default;
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (TimeOnly.TryParse(text, out var timeOnly))
            {
                time = timeOnly.ToTimeSpan();
                return true;
            }

            if (TimeSpan.TryParse(text, out time))
                return true;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var hour = value.TryGetProperty("hour", out var hourEl) ? hourEl.GetInt32() : 0;
            var minute = value.TryGetProperty("minute", out var minuteEl) ? minuteEl.GetInt32() : 0;
            time = new TimeSpan(hour, minute, 0);
            return true;
        }

        return false;
    }

    private static TimeSpan FloorToHalfHour(TimeSpan value)
    {
        var minutes = Math.Max(0, (int)value.TotalMinutes);
        return TimeSpan.FromMinutes(minutes - minutes % 30);
    }

    public async Task<BookingResult> CreateBookingAsync(BookingRequest request)
    {
        if (auth.CurrentUser is null)
            return new BookingResult { Success = false, Message = "Please sign in to book a room." };

        if (auth.CurrentUser.ApiId <= 0)
            return new BookingResult { Success = false, Message = "Your FlexiSpace profile did not load. Sign out and sign in again." };

        if (request.End <= request.Start)
            return new BookingResult { Success = false, Message = "End time must be after start time." };

        var room = await rooms.GetRoomAsync(request.RoomId);
        if (room is null)
            return new BookingResult { Success = false, Message = "Room not found." };

        var physicalIds = room.IsCombined ? room.CombinedRoomIds : [room.Id];
        Booking? first = null;
        var createdIds = new List<int>();

        foreach (var physicalId in physicalIds)
        {
            if (!IdMap.TryToInt(physicalId, out var boardroomId))
                return new BookingResult { Success = false, Message = "Room not found." };

            var payload = await BuildCreatePayloadAsync(boardroomId, request, room.IsCombined);
            var response = await api.PostAsJsonAsync("api/booking", payload);
            if (!response.IsSuccessStatusCode)
            {
                foreach (var id in createdIds)
                    await api.DeleteAsync($"api/booking/{id}");

                return new BookingResult
                {
                    Success = false,
                    Message = await FlexiSpaceApiClient.ReadErrorAsync(response)
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<BookingDto>(FlexiSpaceApiClient.Json);
            if (dto is null)
                return new BookingResult { Success = false, Message = "The API did not return the new booking." };

            createdIds.Add(dto.Id);
            first ??= await MapAsync(dto, room);
        }

        if (first is not null && room.IsCombined)
        {
            first.RoomId = room.Id;
            first.RoomName = room.Name;
        }

        session.LastCreated = first;
        return new BookingResult
        {
            Success = true,
            Message = "Your boardroom is booked.",
            Booking = first
        };
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        var response = await api.DeleteAsync($"api/booking/{IdMap.ToInt(bookingId)}");
        return response.IsSuccessStatusCode;
    }

    public Task<bool> ApproveBookingAsync(Guid bookingId)
    {
        _ = bookingId;
        return Task.FromResult(true);
    }

    public Task<bool> DeclineBookingAsync(Guid bookingId) => CancelBookingAsync(bookingId);

    public async Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var existing = await GetBookingAsync(bookingId);
        if (existing is null || !IdMap.TryToInt(existing.RoomId, out var boardroomId))
            return false;

        var payload = new BookingUpdatePayload
        {
            BoardroomId = boardroomId,
            BookingDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            Company = existing.Company,
            NumberOfAttendees = Math.Max(1, attendees),
            Notes = notes
        };

        var response = await api.PutAsJsonAsync($"api/booking/{IdMap.ToInt(bookingId)}", payload);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason)
    {
        _ = start;
        _ = end;
        if (!IdMap.TryToInt(roomId, out var boardroomId))
            return false;

        var dto = await api.GetAsync<BoardroomDto>($"api/boardroom/{boardroomId}");
        if (dto is null)
            return false;

        var payload = new BoardroomUpdatePayload
        {
            Name = dto.Name,
            Capacity = dto.Capacity,
            Status = 2,
            LocationId = dto.LocationId,
            Equipment = dto.Equipment
        };

        var response = await api.PutAsJsonAsync($"api/boardroom/{boardroomId}", payload);
        return response.IsSuccessStatusCode;
    }

    private async Task<List<Booking>> LoadBookingsAsync()
    {
        var dtos = await api.GetAsync<List<BookingDto>>("api/booking") ?? [];
        var result = new List<Booking>();
        foreach (var dto in dtos)
            result.Add(await MapAsync(dto));
        return result;
    }

    private async Task<BookingCreatePayload> BuildCreatePayloadAsync(int boardroomId, BookingRequest request, bool combined)
    {
        var equipment = await rooms.GetEquipmentMapAsync();
        var catering = await rooms.GetCateringMapAsync();
        var notes = request.Notes;
        if (combined)
        {
            var suffix = "Conjoined Thingamajik + Whachamacallit booking.";
            notes = string.IsNullOrWhiteSpace(notes) ? suffix : $"{notes}\n{suffix}";
        }

        return new BookingCreatePayload
        {
            BoardroomId = boardroomId,
            UserId = auth.CurrentUser!.ApiId,
            BookingDate = DateOnly.FromDateTime(request.Start),
            StartTime = TimeOnly.FromDateTime(request.Start),
            EndTime = TimeOnly.FromDateTime(request.End),
            Company = string.IsNullOrWhiteSpace(request.Company) ? "Flexispace" : request.Company,
            NumberOfAttendees = request.Attendees,
            Notes = notes,
            Equipment = request.Equipment
                .Select(name => equipment.FirstOrDefault(kv => kv.Value.Equals(name, StringComparison.OrdinalIgnoreCase)).Key)
                .Where(id => id > 0)
                .Select(id => new BookingItemDto { EquipmentId = id, Quantity = 1 })
                .ToList(),
            Catering = request.Catering
                .Select(name => catering.FirstOrDefault(kv => kv.Value.Equals(name, StringComparison.OrdinalIgnoreCase)).Key)
                .Where(id => id > 0)
                .Select(id => new BookingItemDto { CateringId = id, Quantity = 1 })
                .ToList()
        };
    }

    private async Task<Booking> MapAsync(BookingDto dto, Boardroom? preferredRoom = null)
    {
        var room = preferredRoom ?? await rooms.GetRoomAsync(dto.BoardroomId.ToString());
        var location = room is null ? null : await rooms.GetLocationAsync(room.LocationId);
        var equipment = await rooms.GetEquipmentMapAsync();
        var catering = await rooms.GetCateringMapAsync();
        var bookerName = auth.CurrentUser?.ApiId == dto.UserId
            ? auth.CurrentUser.Name
            : $"User {dto.UserId}";
        return CatalogMapper.ToBooking(dto, room, location, bookerName, equipment, catering);
    }
}
