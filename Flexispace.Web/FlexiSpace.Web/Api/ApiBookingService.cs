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
        try
        {
            if (IdMap.ToInt(bookingId) > 0)
            {
                var dto = await api.GetAsync<BookingDto>($"api/booking/{IdMap.ToInt(bookingId)}");
                if (dto is not null)
                    return await MapAsync(dto);
            }
        }
        catch
        {
            // Fall through to the list lookup so a missing single-booking
            // endpoint cannot leave the details page spinning forever.
        }

        try
        {
            return (await LoadBookingsAsync()).FirstOrDefault(b => b.Id == bookingId);
        }
        catch
        {
            return null;
        }
    }

    public Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync() =>
        GetBookingsForCurrentUserScopeAsync(DateTime.Today);

    public async Task<IReadOnlyList<OccupiedSlot>> GetOccupiedSlotsAsync(string roomId, DateTime day)
    {
        var linked = RoomCombinations.GetConflictRoomIds(roomId);
        var boardroomIds = linked
            .Select(id => IdMap.TryToInt(id, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToHashSet();

        var date = DateOnly.FromDateTime(day);
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        var slots = new List<OccupiedSlot>();

        foreach (var boardroomId in boardroomIds)
        {
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
            foreach (var booking in await LoadBookingsAsync())
            {
                if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
                    continue;
                if (!RoomMatches(linked, boardroomIds, booking.RoomId))
                    continue;
                if (booking.Start >= dayEnd || booking.End <= dayStart)
                    continue;

                var occStart = booking.Start <= dayStart ? TimeSpan.Zero : booking.Start.TimeOfDay;
                var occEnd = booking.End >= dayEnd ? TimeSpan.FromHours(24) : booking.End.TimeOfDay;
                if (occEnd > occStart)
                    slots.Add(new OccupiedSlot { Start = occStart, End = occEnd });
            }
        }
        catch
        {
            // Keep whatever occupied slots we already collected.
        }

        try
        {
            foreach (var block in await GetBlockedPeriodsAsync())
            {
                if (!RoomMatches(linked, boardroomIds, block.RoomId))
                    continue;
                if (block.Start >= dayEnd || block.End <= dayStart)
                    continue;

                var occStart = block.Start <= dayStart ? TimeSpan.Zero : block.Start.TimeOfDay;
                var occEnd = block.End >= dayEnd ? TimeSpan.FromHours(24) : block.End.TimeOfDay;
                if (occEnd > occStart)
                    slots.Add(new OccupiedSlot { Start = occStart, End = occEnd });
            }
        }
        catch
        {
            // Blocked periods are optional occupancy data.
        }

        return slots
            .GroupBy(slot => (Start: FloorToHalfHour(slot.Start), End: slot.End))
            .Select(group => new OccupiedSlot { Start = group.Key.Start, End = group.Key.End })
            .ToList();
    }

    private static bool RoomMatches(IReadOnlyList<string> linked, HashSet<int> boardroomIds, string roomId) =>
        linked.Contains(roomId) ||
        (IdMap.TryToInt(roomId, out var id) && boardroomIds.Contains(id));

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

    public async Task<BookingResult> CancelBookingAsync(Guid bookingId)
    {
        var id = IdMap.ToInt(bookingId);
        if (id <= 0)
            return Fail("This booking could not be cancelled.");

        var response = await api.DeleteAsync($"api/booking/{id}");
        if (response.IsSuccessStatusCode)
            return Ok("Booking cancelled.");

        // Hosted API forbids DELETE for administrators; status patch is allowed for them.
        var fallback = await api.PatchAsJsonAsync($"api/booking/{id}/status", new BookingStatusPayload
        {
            Status = 1
        });
        if (fallback.IsSuccessStatusCode)
            return Ok("Booking cancelled.");

        var detail = await FlexiSpaceApiClient.ReadErrorAsync(fallback);
        return Fail(string.IsNullOrWhiteSpace(detail) || detail.StartsWith("Request failed", StringComparison.Ordinal)
            ? "You don't have permission to cancel this booking."
            : detail);
    }

    public Task<bool> ApproveBookingAsync(Guid bookingId)
    {
        _ = bookingId;
        return Task.FromResult(true);
    }

    public async Task<bool> DeclineBookingAsync(Guid bookingId) =>
        (await CancelBookingAsync(bookingId)).Success;

    public async Task<BookingResult> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var id = IdMap.ToInt(bookingId);
        if (id <= 0)
            return Fail("This booking could not be updated.");

        if (end <= start)
            return Fail("End time must be after start time.");

        BookingDto? dto = null;
        try
        {
            dto = await api.GetAsync<BookingDto>($"api/booking/{id}");
        }
        catch
        {
        }

        if (dto is null)
        {
            var mapped = await GetBookingAsync(bookingId);
            if (mapped is null || !IdMap.TryToInt(mapped.RoomId, out var roomId))
                return Fail("Booking not found.");

            dto = new BookingDto
            {
                Id = id,
                BoardroomId = roomId,
                Company = mapped.Company,
                NumberOfAttendees = mapped.Attendees,
                Notes = mapped.Notes
            };
        }

        var payload = new BookingUpdatePayload
        {
            BoardroomId = dto.BoardroomId,
            BookingDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            Company = string.IsNullOrWhiteSpace(dto.Company) ? "Flexispace" : dto.Company,
            NumberOfAttendees = Math.Max(1, attendees),
            Notes = notes,
            Equipment = dto.Equipment ?? [],
            Catering = dto.Catering ?? []
        };

        var response = await api.PutAsJsonAsync($"api/booking/{id}", payload);
        if (response.IsSuccessStatusCode)
            return Ok("Booking updated.");

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return Fail("The server did not allow this account to change the booking times. Sign in as the booker or a centre manager, then try again.");

        var detail = await FlexiSpaceApiClient.ReadErrorAsync(response);
        return Fail(string.IsNullOrWhiteSpace(detail) ? "Update failed — check the times and try again." : detail);
    }

    private static BookingResult Ok(string message) =>
        new() { Success = true, Message = message };

    private static BookingResult Fail(string message) =>
        new() { Success = false, Message = message };

    public async Task<BookingResult> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason)
    {
        if (!IdMap.TryToInt(roomId, out var boardroomId) || end <= start)
            return Fail("Choose a valid room and a period that ends after it starts.");

        var text = string.IsNullOrWhiteSpace(reason) ? "Blocked by Centre Manager" : reason.Trim();
        var startLocal = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);

        // Hosted API expects Start/End datetimes. Local API also accepts split date/time.
        var response = await api.PostAsJsonAsync("api/blockedperiod", new
        {
            boardroomId,
            start = startLocal,
            end = endLocal,
            reason = text
        });
        if (!response.IsSuccessStatusCode)
        {
            response = await api.PostAsJsonAsync("api/blockedperiod", new BlockedPeriodCreatePayload
            {
                BoardroomId = boardroomId,
                Start = startLocal,
                End = endLocal,
                StartDate = DateOnly.FromDateTime(startLocal),
                StartTime = TimeOnly.FromDateTime(startLocal),
                EndDate = DateOnly.FromDateTime(endLocal),
                EndTime = TimeOnly.FromDateTime(endLocal),
                Reason = text
            });
        }

        if (response.IsSuccessStatusCode)
            return Ok("The room is now blocked for that period.");

        var detail = await FlexiSpaceApiClient.ReadErrorAsync(response);
        return Fail(string.IsNullOrWhiteSpace(detail) || detail.StartsWith("Request failed", StringComparison.Ordinal)
            ? "The room could not be blocked. Check the times and try again."
            : detail);
    }

    public async Task<IReadOnlyList<BlockedPeriod>> GetBlockedPeriodsAsync(string? roomId = null)
    {
        try
        {
            var url = "api/blockedperiod";
            if (!string.IsNullOrEmpty(roomId) && IdMap.TryToInt(roomId, out var boardroomId))
                url += $"?boardroomId={boardroomId}";

            var dtos = await api.GetAsync<List<BlockedPeriodDto>>(url) ?? [];
            var locations = (await rooms.GetLocationsAsync()).ToDictionary(l => l.Id, l => l.Name);
            return dtos.Select(dto => new BlockedPeriod
            {
                Id = IdMap.ToGuid(dto.Id),
                RoomId = dto.BoardroomId.ToString(),
                RoomName = dto.BoardroomName,
                LocationId = dto.LocationId.ToString(),
                LocationName = locations.GetValueOrDefault(dto.LocationId.ToString(), string.Empty),
                Start = dto.Start ?? (dto.StartDate == default
                    ? default
                    : dto.StartDate.ToDateTime(dto.StartTime)),
                End = dto.End ?? (dto.EndDate == default
                    ? default
                    : dto.EndDate.ToDateTime(dto.EndTime)),
                Reason = dto.Reason,
                CreatedBy = dto.CreatedByName
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> RemoveBlockedPeriodAsync(Guid blockId)
    {
        var id = IdMap.ToInt(blockId);
        if (id <= 0)
            return false;

        var response = await api.DeleteAsync($"api/blockedperiod/{id}");
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
