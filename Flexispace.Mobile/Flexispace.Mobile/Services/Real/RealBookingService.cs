using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Bridges IBookingService onto /api/booking, /api/booking/search and
// /api/blockedperiod. Ported from Flexispace.Web's RealBookingService;
// see that file for the fuller commentary this one trims for brevity.
// Registered Singleton in MauiProgram.cs, matching MockBookingService.
public class RealBookingService : IBookingService
{
    private readonly FlexiSpaceApiClient _api;
    private readonly IRoomService _rooms;
    private List<ApiEquipment>? _equipmentCatalogue;
    private List<ApiCatering>? _cateringCatalogue;

    public RealBookingService(FlexiSpaceApiClient api, IRoomService rooms)
    {
        _api = api;
        _rooms = rooms;
    }

    private async Task<List<ApiEquipment>> GetEquipmentCatalogueAsync() =>
        _equipmentCatalogue ??= await _api.GetAsync<List<ApiEquipment>>("api/equipment") ?? new();

    private async Task<List<ApiCatering>> GetCateringCatalogueAsync() =>
        _cateringCatalogue ??= await _api.GetAsync<List<ApiCatering>>("api/catering") ?? new();

    public async Task<IReadOnlyList<Booking>> GetBookingsAsync(
        string? locationId = null, Guid? userId = null, DateTime? day = null)
    {
        // NOTE: userId isn't applied - see Flexispace.Web's identical
        // method for why (this app's Booking.BookerId is a Guid with no
        // reversible mapping back to the API's int UserId). Every call
        // site that actually needs "my bookings" goes through
        // GetBookingsForCurrentUserScopeAsync below instead, which the
        // API already scopes server-side.
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(locationId)) query.Add($"locationId={locationId}");
        if (day.HasValue) query.Add($"fromDate={DateOnly.FromDateTime(day.Value):O}&toDate={DateOnly.FromDateTime(day.Value):O}");

        var path = "api/booking/search" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var bookings = await _api.GetAsync<List<ApiBooking>>(path) ?? new();
        return await MapAllAsync(bookings);
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null)
    {
        // /api/booking and /api/booking/search are already scoped
        // server-side by the caller's role (Administrator: everything,
        // CentreManager: their location, everyone else: their own
        // bookings - BookingService.ApplyVisibilityScope), so whatever
        // comes back IS "my scope".
        var path = day.HasValue
            ? $"api/booking/search?fromDate={DateOnly.FromDateTime(day.Value):O}&toDate={DateOnly.FromDateTime(day.Value):O}"
            : "api/booking";
        var bookings = await _api.GetAsync<List<ApiBooking>>(path) ?? new();
        return await MapAllAsync(bookings);
    }

    public async Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var bookings = await _api.GetAsync<List<ApiBooking>>(
            $"api/booking/search?fromDate={today:O}&toDate={today:O}") ?? new();
        return await MapAllAsync(bookings);
    }

    public async Task<Booking?> GetBookingAsync(Guid bookingId)
    {
        var id = ApiId.Decode(bookingId);
        if (id is null) return null;

        var (found, booking) = await _api.TryGetAsync<ApiBooking>($"api/booking/{id}");
        return found && booking is not null ? await MapAsync(booking) : null;
    }

    public async Task<BookingResult> CreateBookingAsync(BookingRequest request)
    {
        if (!int.TryParse(request.RoomId, out var boardroomId))
            return new BookingResult { Success = false, Message = "Invalid room." };

        var dto = await BuildCreateOrUpdateDtoAsync(boardroomId, request.Start, request.End,
            request.Company, request.Attendees, request.Equipment, request.Catering, request.Notes);

        var result = await _api.PostAsync<ApiBookingCreateOrUpdate, ApiBooking>("api/booking", dto);

        if (!result.Success || result.Value is null)
            return new BookingResult { Success = false, Message = result.ErrorMessage ?? "Could not create the booking." };

        return new BookingResult
        {
            Success = true,
            Message = "Booking confirmed.",
            Booking = await MapAsync(result.Value)
        };
    }

    public async Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var id = ApiId.Decode(bookingId);
        if (id is null) return false;

        var (found, existing) = await _api.TryGetAsync<ApiBooking>($"api/booking/{id}");
        if (!found || existing is null) return false;

        var dto = new ApiBookingCreateOrUpdate
        {
            BoardroomId = existing.BoardroomId,
            BookingDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            Company = existing.Company,
            NumberOfAttendees = attendees,
            Notes = notes,
            Equipment = existing.Equipment,
            Catering = existing.Catering
        };

        return await _api.PutAsync($"api/booking/{id}", dto);
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        var id = ApiId.Decode(bookingId);
        if (id is null) return false;
        return await _api.DeleteAsync($"api/booking/{id}");
    }

    // relatedBookingId isn't sent anywhere - the real BlockedPeriod entity
    // has no concept of "the booking that prompted this block" (see
    // FlexiSpace.Core.Entities.BlockedPeriod). It's accepted here only to
    // satisfy IBookingService's signature; ManagePage/ManageViewModel
    // don't need to change.
    public async Task<BlockRoomResult> BlockRoomAsync(
        string roomId, DateTime start, DateTime end, string reason, Guid? relatedBookingId = null)
    {
        if (!int.TryParse(roomId, out var boardroomId))
            return new BlockRoomResult { Success = false, Title = "Couldn't block room", Message = "Invalid room." };

        var dto = new ApiBlockedPeriodCreate
        {
            BoardroomId = boardroomId,
            Start = start,
            End = end,
            Reason = reason
        };

        var result = await _api.PostAsync<ApiBlockedPeriodCreate, ApiBlockedPeriod>("api/blockedperiod", dto);

        return result.Success
            ? new BlockRoomResult { Success = true, Title = "Room blocked", Message = "The room has been blocked for that period." }
            : new BlockRoomResult { Success = false, Title = "Couldn't block room", Message = result.ErrorMessage ?? "The room could not be blocked." };
    }

    public async Task<IReadOnlyList<BlockedPeriod>> GetBlockedPeriodsAsync(string roomId)
    {
        if (!int.TryParse(roomId, out var boardroomId)) return Array.Empty<BlockedPeriod>();

        var periods = await _api.GetAsync<List<ApiBlockedPeriod>>(
            $"api/blockedperiod?boardroomId={boardroomId}") ?? new();

        return periods.Select(p => new BlockedPeriod
        {
            Id = ApiId.Encode(p.Id),
            RoomId = p.BoardroomId.ToString(),
            Start = p.Start,
            End = p.End,
            Reason = p.Reason,
            CreatedBy = p.CreatedByName
        }).ToList();
    }

    private async Task<ApiBookingCreateOrUpdate> BuildCreateOrUpdateDtoAsync(
        int boardroomId, DateTime start, DateTime end, string? company, int attendees,
        List<string> equipmentNames, List<string> cateringNames, string notes)
    {
        var equipment = await ResolveEquipmentAsync(equipmentNames);
        var catering = await ResolveCateringAsync(cateringNames);

        return new ApiBookingCreateOrUpdate
        {
            BoardroomId = boardroomId,
            BookingDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            Company = company,
            NumberOfAttendees = attendees,
            Notes = notes,
            Equipment = equipment,
            Catering = catering
        };
    }

    // Turns the booking form's chosen equipment/catering *names* into the
    // ids the real API needs. A name that doesn't match anything in the
    // catalogue is silently dropped rather than failing the whole
    // booking - same rough edge flagged in Flexispace.Web's version,
    // worth replacing with real catalogue-driven pickers later.
    private async Task<List<ApiBookingEquipment>> ResolveEquipmentAsync(List<string> names)
    {
        if (names.Count == 0) return new();
        var catalogue = await GetEquipmentCatalogueAsync();
        return names
            .Select(n => catalogue.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)))
            .Where(c => c is not null)
            .Select(c => new ApiBookingEquipment { EquipmentId = c!.Id, Quantity = 1 })
            .ToList();
    }

    private async Task<List<ApiBookingCatering>> ResolveCateringAsync(List<string> names)
    {
        if (names.Count == 0) return new();
        var catalogue = await GetCateringCatalogueAsync();
        return names
            .Select(n => catalogue.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)))
            .Where(c => c is not null)
            .Select(c => new ApiBookingCatering { CateringId = c!.Id, Quantity = 1 })
            .ToList();
    }

    private async Task<List<Booking>> MapAllAsync(List<ApiBooking> bookings)
    {
        var result = new List<Booking>(bookings.Count);
        foreach (var b in bookings)
            result.Add(await MapAsync(b));
        return result;
    }

    private async Task<Booking> MapAsync(ApiBooking b)
    {
        var room = await _rooms.GetRoomAsync(b.BoardroomId.ToString());
        var location = room is null ? null : await _rooms.GetLocationAsync(room.LocationId);
        var equipmentCatalogue = await GetEquipmentCatalogueAsync();
        var cateringCatalogue = await GetCateringCatalogueAsync();

        return new Booking
        {
            Id = ApiId.Encode(b.Id),
            RoomId = b.BoardroomId.ToString(),
            LocationId = room?.LocationId ?? string.Empty,
            RoomName = room?.Name ?? $"Room #{b.BoardroomId}",
            LocationName = location?.Name ?? string.Empty,
            Start = b.BookingDate.ToDateTime(b.StartTime),
            End = b.BookingDate.ToDateTime(b.EndTime),
            BookerId = Guid.Empty, // see GetBookingsAsync's note on userId
            BookerName = string.Empty,
            Company = b.Company ?? string.Empty,
            Attendees = b.NumberOfAttendees,
            Equipment = b.Equipment
                .Select(e => equipmentCatalogue.FirstOrDefault(c => c.Id == e.EquipmentId)?.Name)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList(),
            Catering = b.Catering
                .Select(c => cateringCatalogue.FirstOrDefault(cat => cat.Id == c.CateringId)?.Name)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList(),
            Notes = b.Notes ?? string.Empty,
            Status = MapStatus(b.Status),
            OutlookEventId = b.OutlookEventId
        };
    }

    private static BookingStatus MapStatus(ApiBookingStatus status) => status switch
    {
        ApiBookingStatus.Confirmed => BookingStatus.Confirmed,
        ApiBookingStatus.Cancelled => BookingStatus.Cancelled,
        ApiBookingStatus.Completed => BookingStatus.Completed,
        // The real backend has no Pending/approval status any more (see
        // FlexiSpace.Core.Enums.BookingStatus) - never actually reached.
        _ => BookingStatus.Confirmed
    };
}
