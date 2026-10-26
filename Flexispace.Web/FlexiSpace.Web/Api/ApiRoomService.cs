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
    private List<CatalogItem>? _equipmentItems;
    private List<CatalogItem>? _cateringItems;

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
        var blocked = await LoadActiveBlocksAsync();
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

                var liveBlock = blocked.FirstOrDefault(b =>
                    conflictIds.Contains(b.RoomId) && b.Start <= now && b.End > now);
                if (liveBlock is not null)
                    clone.Status = StatusFromBlockReason(liveBlock.Reason);
                else if (clone.Status is RoomStatus.Cleaning or RoomStatus.Maintenance or RoomStatus.Blocked)
                    return clone;
                else if (active.Any(b => b.Start <= now && b.End > now))
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
        return _equipmentItems!.Where(item => item.IsActive).Select(item => item.Name).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCateringCatalogAsync()
    {
        await EnsureCatalogAsync();
        return _cateringItems!.Where(item => item.IsActive).Select(item => item.Name).ToList();
    }

    public async Task<IReadOnlyList<CatalogItem>> GetEquipmentCatalogItemsAsync()
    {
        var equipment = await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? [];
        return equipment
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new CatalogItem
            {
                Name = item.Name,
                Description = item.Description ?? string.Empty,
                IsActive = item.IsActive
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogItem>> GetCateringCatalogItemsAsync()
    {
        var catering = await api.GetAsync<List<CateringDto>>("api/catering") ?? [];
        return catering
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new CatalogItem
            {
                Name = item.Name,
                Description = item.Description ?? string.Empty,
                IsActive = item.IsActive
            })
            .ToList();
    }

    public void InvalidateCatalog()
    {
        _locations = null;
        _rooms = null;
        _equipment = null;
        _catering = null;
        _equipmentItems = null;
        _cateringItems = null;
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

        List<LocationDto> locations = [];
        List<BoardroomDto> boardrooms = [];
        List<EquipmentDto> equipment = [];
        List<CateringDto> catering = [];
        try { locations = await api.GetAsync<List<LocationDto>>("api/location") ?? []; } catch { }
        try { boardrooms = await api.GetAsync<List<BoardroomDto>>("api/boardroom") ?? []; } catch { }
        try { equipment = await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? []; } catch { }
        try { catering = await api.GetAsync<List<CateringDto>>("api/catering") ?? []; } catch { }

        if (locations.Count == 0)
        {
            _locations = SeedData.Locations.Select(l => new OfficeLocation
            {
                Id = l.Id,
                Name = l.Name,
                Address = l.Address,
                Phone = l.Phone,
                CentreManager = l.CentreManager,
                ManagerEmail = l.ManagerEmail,
                ImageKey = l.ImageKey,
                Tagline = l.Tagline
            }).ToList();
            _rooms = SeedData.Rooms.Select(Clone).ToList();
            _equipment = [];
            _catering = [];
            _equipmentItems = [.. SeedData.EquipmentCatalog];
            _cateringItems = [.. SeedData.CateringCatalog];
            CatalogMapper.AttachCombinations(_rooms);
            return;
        }

        _equipment = equipment
            .Where(e => e.IsActive)
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
        _catering = catering
            .Where(c => c.IsActive)
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
        _equipmentItems = ToCatalogItems(equipment.Select(e => (e.Name, e.Description, e.IsActive)), SeedData.EquipmentCatalog);
        _cateringItems = ToCatalogItems(catering.Select(c => (c.Name, c.Description ?? string.Empty, c.IsActive)), SeedData.CateringCatalog);

        _locations = locations.Select(CatalogMapper.ToLocation).ToList();
        _rooms = boardrooms.Select(r => CatalogMapper.ToRoom(r, _equipment)).ToList();
        CatalogMapper.AttachCombinations(_rooms);
    }

    private static List<CatalogItem> ToCatalogItems(
        IEnumerable<(string Name, string Description, bool IsActive)> source,
        IReadOnlyList<CatalogItem> fallback)
    {
        var items = source
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new CatalogItem
                {
                    Name = first.Name,
                    Description = string.IsNullOrWhiteSpace(first.Description) ? string.Empty : first.Description,
                    IsActive = group.Any(item => item.IsActive)
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return items.Count > 0 ? items : [.. fallback];
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

    private async Task<List<BlockedPeriod>> LoadActiveBlocksAsync()
    {
        try
        {
            var dtos = await api.GetAsync<List<BlockedPeriodDto>>("api/blockedperiod") ?? [];
            return dtos.Select(dto => new BlockedPeriod
            {
                RoomId = dto.BoardroomId.ToString(),
                Start = dto.Start ?? (dto.StartDate == default ? default : dto.StartDate.ToDateTime(dto.StartTime)),
                End = dto.End ?? (dto.EndDate == default ? default : dto.EndDate.ToDateTime(dto.EndTime)),
                Reason = dto.Reason
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static RoomStatus StatusFromBlockReason(string reason)
    {
        if (reason.Contains("clean", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Cleaning;
        if (reason.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Maintenance;
        return RoomStatus.Blocked;
    }
}
