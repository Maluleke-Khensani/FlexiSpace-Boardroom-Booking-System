using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Bridges IRoomService onto GET /api/location and GET /api/boardroom.
// Ported from Flexispace.Web's RealRoomService with two differences:
// - Boardroom.Equipment here is a List<string> of display names, same
//   as Web's prototype model, resolved from the equipment catalogue the
//   same way.
// - This app's Boardroom models "combinable" as a single partner room
//   (IsCombined / CombinableWithRoomId) rather than Web's list-based
//   CombinedRoomIds, so the mapping below picks the first id from
//   whichever list the real API returns.
//
// Registered Singleton in MauiProgram.cs.
public class RealRoomService : IRoomService
{
    private readonly FlexiSpaceApiClient _api;
    private List<ApiEquipment>? _equipmentCatalogue;
    private Dictionary<int, string>? _locationNames;

    public RealRoomService(FlexiSpaceApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync()
    {
        var locations = await _api.GetAsync<List<ApiLocation>>("api/location") ?? new();
        _locationNames = locations.ToDictionary(l => l.Id, l => l.Name);
        return locations.Select(MapLocation).ToList();
    }

    public async Task<OfficeLocation?> GetLocationAsync(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId) || !int.TryParse(locationId, out var id))
            return null;

        var (found, location) = await _api.TryGetAsync<ApiLocation>($"api/location/{id}");
        return found && location is not null ? MapLocation(location) : null;
    }

    public async Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null)
    {
        var boardrooms = await _api.GetAsync<List<ApiBoardroom>>("api/boardroom") ?? new();
        await EnsureEquipmentCatalogueAsync();
        await EnsureLocationNamesAsync();

        var filtered = string.IsNullOrWhiteSpace(locationId)
            ? boardrooms
            : boardrooms.Where(b => b.LocationId.ToString() == locationId).ToList();

        return filtered.Select(MapBoardroom).ToList();
    }

    public async Task<Boardroom?> GetRoomAsync(string roomId)
    {
        if (!int.TryParse(roomId, out var id)) return null;

        var (found, boardroom) = await _api.TryGetAsync<ApiBoardroom>($"api/boardroom/{id}");
        if (!found || boardroom is null) return null;

        await EnsureEquipmentCatalogueAsync();
        await EnsureLocationNamesAsync();
        return MapBoardroom(boardroom);
    }

    // The real backend doesn't have a dedicated "availability on this
    // date" endpoint for boardrooms the way the prototype's model implies
    // - booking conflict-checking happens per-booking-attempt on the
    // backend (BookingService.HasConflictAsync/HasBlockConflictAsync).
    // This returns every room at the location reporting its current
    // Status - good enough for browsing "what rooms exist here", not
    // exact for "is this room free on this specific day". The booking
    // form's own conflict check on submit is still the real source of
    // truth for that.
    public Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date) =>
        GetRoomsAsync(locationId);

    private async Task EnsureEquipmentCatalogueAsync()
    {
        _equipmentCatalogue ??= await _api.GetAsync<List<ApiEquipment>>("api/equipment") ?? new();
    }

    // Room photos are looked up by location name + room name (see
    // LocationPresentation), so the names are cached alongside the rooms.
    private async Task EnsureLocationNamesAsync()
    {
        if (_locationNames is not null) return;
        var locations = await _api.GetAsync<List<ApiLocation>>("api/location") ?? new();
        _locationNames = locations.ToDictionary(l => l.Id, l => l.Name);
    }

    private Boardroom MapBoardroom(ApiBoardroom b)
    {
        // This room IS a combined space if it has components; it's a
        // component OF a combined space if it appears in someone else's
        // CombinedIntoBoardroomIds. Either way, CombinableWithRoomId only
        // has room for one partner id - the real backend supports more
        // than two rooms combining, but every combined pair FlexiSpace
        // actually has today (Eagle Canyon's Thingamajik + Whachamacallit)
        // is exactly two, so taking the first id loses nothing in
        // practice. Worth revisiting if a three-way combination is ever
        // added.
        var isCombined = b.ComponentBoardroomIds.Count > 0;
        var combinableWithRoomId = isCombined
            ? b.ComponentBoardroomIds.FirstOrDefault().ToString()
            : b.CombinedIntoBoardroomIds.Count > 0
                ? b.CombinedIntoBoardroomIds[0].ToString()
                : null;

        return new Boardroom
        {
            Id = b.Id.ToString(),
            Name = b.Name,
            LocationId = b.LocationId.ToString(),
            Capacity = b.Capacity,
            Equipment = b.Equipment
                .Select(e => _equipmentCatalogue?.FirstOrDefault(c => c.Id == e.EquipmentId)?.Name)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList(),
            Status = MapStatus(b.Status),
            IsCombined = isCombined,
            CombinableWithRoomId = combinableWithRoomId,
            ImageKey = LocationPresentation.RoomImage(
                _locationNames?.GetValueOrDefault(b.LocationId), b.Name)
        };
    }

    private static OfficeLocation MapLocation(ApiLocation l)
    {
        var location = new OfficeLocation
        {
            Id = l.Id.ToString(),
            Name = l.Name,
            Address = l.Address
        };
        // Photo, tagline and contact details aren't stored by the API.
        LocationPresentation.Apply(location);
        return location;
    }

    private static RoomStatus MapStatus(ApiBoardroomStatus status) => status switch
    {
        ApiBoardroomStatus.Available => RoomStatus.Available,
        ApiBoardroomStatus.Maintenance => RoomStatus.Maintenance,
        // The real backend's "Unavailable" covers both "blocked" and
        // "otherwise taken out of service" - Blocked is the closer match
        // of this app's states (Occupied/Reserved/Cleaning are all
        // booking-driven, momentary states the real Boardroom entity
        // doesn't track at all).
        ApiBoardroomStatus.Unavailable => RoomStatus.Blocked,
        _ => RoomStatus.Available
    };
}
