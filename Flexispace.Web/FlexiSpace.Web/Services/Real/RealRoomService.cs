using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Services.Real;

// Bridges IRoomService onto GET /api/location and GET /api/boardroom.
// The prototype's Boardroom.Equipment is a List<string> of display names;
// the real API models equipment as a list of {EquipmentId, Quantity}. To
// show real names without a bigger UI rework, this service fetches the
// equipment catalogue once per circuit and resolves names by id - see
// EnsureEquipmentCatalogueAsync.
//
// Registered Scoped in Program.cs, same reasoning as RealAuthService.
public class RealRoomService : IRoomService
{
    private readonly FlexiSpaceApiClient _api;
    private List<ApiEquipment>? _equipmentCatalogue;

    public RealRoomService(FlexiSpaceApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync()
    {
        var locations = await _api.GetAsync<List<ApiLocation>>("api/location") ?? new();
        return locations.Select(MapLocation).ToList();
    }

    public async Task<OfficeLocation?> GetLocationAsync(string? locationId)
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
        return MapBoardroom(boardroom);
    }

    // The real backend doesn't have a dedicated "availability on this
    // date" endpoint for boardrooms the way the prototype's model implies
    // (that's what booking conflict-checking on the backend already does
    // per-booking-attempt, via BookingService's HasConflictAsync/
    // HasBlockConflictAsync). Until/unless a real availability endpoint is
    // added, this returns every room at the location, all reported as
    // whatever their current Status is - it does NOT compute "booked
    // during that date" the way MockRoomService's demo data faked it.
    // Good enough for browsing "what rooms exist here", not exact for
    // "is this room free on this specific day" - the booking form's own
    // conflict check on submit is still the real source of truth for that.
    public Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date) =>
        GetRoomsAsync(locationId);

    private async Task EnsureEquipmentCatalogueAsync()
    {
        _equipmentCatalogue ??= await _api.GetAsync<List<ApiEquipment>>("api/equipment") ?? new();
    }

    private Boardroom MapBoardroom(ApiBoardroom b) => new()
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
        CombinedRoomIds = b.ComponentBoardroomIds.Select(id => id.ToString()).ToList()
    };

    private static OfficeLocation MapLocation(ApiLocation l) => new()
    {
        Id = l.Id.ToString(),
        Name = l.Name,
        Address = l.Address
    };

    private static RoomStatus MapStatus(ApiBoardroomStatus status) => status switch
    {
        ApiBoardroomStatus.Available => RoomStatus.Available,
        ApiBoardroomStatus.Maintenance => RoomStatus.Maintenance,
        // The real backend's "Unavailable" covers both "blocked" and
        // "otherwise taken out of service" - Blocked is the closer match
        // of the prototype's states (Occupied/Reserved/Cleaning are all
        // booking-driven, momentary states the real Boardroom entity
        // doesn't track at all).
        ApiBoardroomStatus.Unavailable => RoomStatus.Blocked,
        _ => RoomStatus.Available
    };
}
