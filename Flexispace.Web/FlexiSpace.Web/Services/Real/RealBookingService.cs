using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Services.Real;

// Bridges IBookingService onto /api/booking, /api/booking/search and
// /api/blockedperiod. Registered Scoped in Program.cs.
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

    public async Task<IReadOnlyList<string>> GetEquipmentOptionsAsync() =>
        (await GetEquipmentCatalogueAsync()).Where(e => e.IsActive).Select(e => e.Name).ToList();

    public async Task<IReadOnlyList<string>> GetCateringOptionsAsync() =>
        (await GetCateringCatalogueAsync()).Where(c => c.IsActive).Select(c => c.Name).ToList();

    public async Task<IReadOnlyList<Booking>> GetBookingsAsync(
        string? locationId = null, Guid? userId = null, DateTime? day = null)
    {
        // NOTE: userId isn't applied. The prototype's User.Id is a Guid
        // (see RealAuthService.MapUser for why), but the API filters by
        // its own int UserId - there's no reversible way to turn the Guid
        // back into that int here without an extra lookup this interface
        // doesn't give us a user list for. Every call site in this app
        // that actually needs "my bookings" goes through
        // GetBookingsForCurrentUserScopeAsync below instead, which the
        // API already scopes server-side - so in practice this gap isn't
        // hit. Flagged here in case a future screen calls this overload
        // with a real userId expecting it to filter.
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(locationId)) query.Add($"locationId={locationId}");
        if (day.HasValue) query.Add(DayFilter(day.Value));

        var bookings = await SearchAllAsync(string.Join("&", query));
        return await MapAllAsync(bookings);
    }

    public async Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null)
    {
        // The API already scopes /api/booking (GetAllBookings) and
        // /api/booking/search by the caller's role - Administrator sees
        // everything, CentreManager sees their location, everyone else
        // sees their own bookings - via BookingService.ApplyVisibilityScope
        // on the backend. So the client doesn't need to pass anything
        // extra here; whatever comes back IS "my scope".
        var bookings = day.HasValue
            ? await SearchAllAsync(DayFilter(day.Value))
            : (await _api.GetAsync<List<ApiBooking>>("api/booking") ?? new List<ApiBooking>());
        return await MapAllAsync(bookings);
    }

    // GET /api/booking/search returns one page ({ items, totalCount, page,
    // pageSize }), not a bare array - reading it as List<ApiBooking> was
    // the JsonException on My bookings (and why the dashboard's "Today"
    // section silently came back empty). This walks every page so callers
    // still get the full list. The API caps pageSize at 100.
    private async Task<List<ApiBooking>> SearchAllAsync(string filters)
    {
        const int pageSize = 100;
        const int maxPages = 50; // safety stop: 5,000 bookings
        var prefix = string.IsNullOrEmpty(filters) ? string.Empty : filters + "&";
        var all = new List<ApiBooking>();

        for (var page = 1; page <= maxPages; page++)
        {
            var result = await _api.GetAsync<ApiPagedResult<ApiBooking>>(
                $"api/booking/search?{prefix}page={page}&pageSize={pageSize}");
            if (result is null || result.Items.Count == 0) break;

            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount) break;
        }

        return all;
    }

    private static string DayFilter(DateTime day)
    {
        var date = DateOnly.FromDateTime(day);
        return $"fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}";
    }

    public async Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync()
    {
        var bookings = await SearchAllAsync(DayFilter(DateTime.Today));
        return await MapAllAsync(bookings);
    }

    public async Task<Booking?> GetBookingAsync(Guid bookingId)
    {
        var id = DecodeBookingId(bookingId);
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
        var id = DecodeBookingId(bookingId);
        if (id is null) return false;

        var (found, existing) = await _api.TryGetAsync<ApiBooking>($"api/booking/{id}");
        if (!found || existing is null) return false;

        var dto = await BuildCreateOrUpdateDtoAsync(
            existing.BoardroomId, start, end, existing.Company, attendees,
            equipmentNames: null, cateringNames: null, notes,
            existingEquipment: existing.Equipment, existingCatering: existing.Catering);

        return await _api.PutAsync($"api/booking/{id}", dto);
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        var id = DecodeBookingId(bookingId);
        if (id is null) return false;
        return await _api.DeleteAsync($"api/booking/{id}");
    }

    // Dropped: the real backend has no Pending/approval step any more
    // (BookingStatus is Confirmed/Cancelled/Completed only - see
    // FlexiSpace.Core.Enums.BookingStatus). The UI already never shows
    // Approve/Decline buttons for real data as a result (they're gated on
    // Booking.Status == BookingStatus.Pending in BookingListViewModels.cs
    // and OtherViewModels.cs, which real bookings can never be), so these
    // are just safe no-ops rather than dead UI to hunt down and remove.
    public Task<bool> ApproveBookingAsync(Guid bookingId) => Task.FromResult(false);
    public Task<bool> DeclineBookingAsync(Guid bookingId) => Task.FromResult(false);

    public async Task<bool> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason)
    {
        if (!int.TryParse(roomId, out var boardroomId)) return false;

        var dto = new ApiBlockedPeriodCreate
        {
            BoardroomId = boardroomId,
            Start = start,
            End = end,
            Reason = reason
        };

        return await _api.PostAsync("api/blockedperiod", dto);
    }

    private async Task<ApiBookingCreateOrUpdate> BuildCreateOrUpdateDtoAsync(
        int boardroomId, DateTime start, DateTime end, string? company, int attendees,
        List<string>? equipmentNames, List<string>? cateringNames, string notes,
        List<ApiBookingEquipment>? existingEquipment = null,
        List<ApiBookingCatering>? existingCatering = null)
    {
        var equipment = existingEquipment ?? await ResolveEquipmentAsync(equipmentNames ?? new());
        var catering = existingCatering ?? await ResolveCateringAsync(cateringNames ?? new());

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

    // Turns the booking form's chosen equipment/catering *names* back into
    // the ids the real API needs. The form's options come from these same
    // catalogues (GetEquipmentOptionsAsync/GetCateringOptionsAsync), so
    // every name matches.
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
            Id = EncodeBookingId(b.Id),
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

    // The prototype's Booking.Id is a Guid; the real API's is an int. See
    // ApiId for the shared encode/decode scheme (also used for
    // AppNotification.Id/BookingId so a notification's deep link resolves
    // to the same encoded booking id a booking list would produce).
    private static Guid EncodeBookingId(int id) => ApiId.Encode(id);

    private static int? DecodeBookingId(Guid guid) => ApiId.Decode(guid);

    private static BookingStatus MapStatus(ApiBookingStatus status) => status switch
    {
        ApiBookingStatus.Confirmed => BookingStatus.Confirmed,
        ApiBookingStatus.Cancelled => BookingStatus.Cancelled,
        ApiBookingStatus.Completed => BookingStatus.Completed,
        _ => BookingStatus.Confirmed
    };
}
