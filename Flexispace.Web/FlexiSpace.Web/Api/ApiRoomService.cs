using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiRoomService(FlexiSpaceApiClient api) : IRoomService
{
    private List<OfficeLocation>? _locations;
    private List<Boardroom>? _rooms;
    private Dictionary<int, string>? _equipment;
    private Dictionary<int, string>? _catering;

    public async Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync()
    {
        await EnsureCatalogAsync();
        return _locations!;
    }

    public async Task<OfficeLocation?> GetLocationAsync(string locationId)
    {
        await EnsureCatalogAsync();
        return _locations!.FirstOrDefault(l => l.Id == locationId);
    }

    public async Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null)
    {
        await EnsureCatalogAsync();
        IEnumerable<Boardroom> rooms = _rooms!.Where(r => !r.IsCombined);
        if (!string.IsNullOrEmpty(locationId))
            rooms = rooms.Where(r => r.LocationId == locationId);
        return rooms.ToList();
    }

    public async Task<Boardroom?> GetRoomAsync(string roomId)
    {
        await EnsureCatalogAsync();
        return _rooms!.FirstOrDefault(r => r.Id == roomId);
    }

    public async Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date)
    {
        await EnsureCatalogAsync();
        var bookings = await api.GetAsync<List<BookingDto>>("api/booking") ?? [];
        var day = date.Date;
        var now = DateTime.Now;

        return _rooms!
            .Where(r => !r.IsCombined)
            .Where(r => string.IsNullOrEmpty(locationId) || r.LocationId == locationId)
            .Select(r =>
            {
                var clone = Clone(r);
                var conflictIds = RoomCombinations.GetConflictRoomIds(r.Id);
                var active = bookings
                    .Where(b => conflictIds.Contains(b.BoardroomId.ToString()) &&
                                CatalogMapper.ToWebStatus(b.Status) != BookingStatus.Cancelled &&
                                b.BookingDate.ToDateTime(TimeOnly.MinValue).Date == day)
                    .Select(b => (
                        Start: b.BookingDate.ToDateTime(b.StartTime),
                        End: b.BookingDate.ToDateTime(b.EndTime)))
                    .ToList();

                if (clone.Status is RoomStatus.Maintenance or RoomStatus.Blocked)
                    return clone;
                if (active.Any(b => b.Start <= now && b.End > now))
                    clone.Status = RoomStatus.Occupied;
                else if (active.Any(b => b.Start > now && b.Start < now.AddHours(2)))
                    clone.Status = RoomStatus.Reserved;
                else if (active.Count > 0 && day == DateTime.Today)
                    clone.Status = RoomStatus.Reserved;
                else
                    clone.Status = RoomStatus.Available;
                return clone;
            })
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetEquipmentCatalogAsync()
    {
        await EnsureCatalogAsync();
        return _equipment!.Values.OrderBy(n => n).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCateringCatalogAsync()
    {
        await EnsureCatalogAsync();
        return _catering!.Values.OrderBy(n => n).ToList();
    }

    internal async Task<IReadOnlyDictionary<int, string>> GetEquipmentMapAsync()
    {
        await EnsureCatalogAsync();
        return _equipment!;
    }

    internal async Task<IReadOnlyDictionary<int, string>> GetCateringMapAsync()
    {
        await EnsureCatalogAsync();
        return _catering!;
    }

    private async Task EnsureCatalogAsync()
    {
        if (_locations is not null && _rooms is not null)
            return;

        var locations = await api.GetAsync<List<LocationDto>>("api/location") ?? [];
        var boardrooms = await api.GetAsync<List<BoardroomDto>>("api/boardroom") ?? [];
        var equipment = await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? [];
        var catering = await api.GetAsync<List<CateringDto>>("api/catering") ?? [];

        _equipment = equipment
            .Where(e => e.IsActive)
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
        _catering = catering
            .Where(c => c.IsActive)
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        _locations = locations.Select(CatalogMapper.ToLocation).ToList();
        _rooms = boardrooms.Select(r => CatalogMapper.ToRoom(r, _equipment)).ToList();
        CatalogMapper.AttachCombinations(_rooms);
    }

    private static Boardroom Clone(Boardroom r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        LocationId = r.LocationId,
        Capacity = r.Capacity,
        Equipment = [.. r.Equipment],
        ImageKey = r.ImageKey,
        CombinedRoomIds = [.. r.CombinedRoomIds],
        Status = r.Status
    };
}
