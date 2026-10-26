using Flexispace.Core.Helpers;
using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

public class MockRoomService(MockDataStore store) : IRoomService
{
    public Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync() =>
        Task.FromResult<IReadOnlyList<OfficeLocation>>(SeedData.Locations);

    public Task<OfficeLocation?> GetLocationAsync(string locationId) =>
        Task.FromResult(SeedData.Locations.FirstOrDefault(l => l.Id == locationId));

    public Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null)
    {
        var rooms = SeedData.Rooms.Where(r => !r.IsCombined);
        if (!string.IsNullOrEmpty(locationId))
            rooms = rooms.Where(r => r.LocationId == locationId);
        return Task.FromResult<IReadOnlyList<Boardroom>>(rooms.ToList());
    }

    public Task<Boardroom?> GetRoomAsync(string roomId) =>
        Task.FromResult(SeedData.Rooms.FirstOrDefault(r => r.Id == roomId));

    public Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date)
    {
        var day = date.Date;
        var now = DateTime.Now;
        var rooms = SeedData.Rooms
            .Where(r => !r.IsCombined)
            .Where(r => string.IsNullOrEmpty(locationId) || r.LocationId == locationId)
            .Select(r =>
            {
                var clone = Clone(r);
                var conflictIds = RoomCombinations.GetConflictRoomIds(r.Id);

                var active = store.Bookings
                    .Where(b => conflictIds.Contains(b.RoomId) &&
                                b.Status != BookingStatus.Cancelled &&
                                b.Start.Date == day)
                    .ToList();

                var liveBlock = store.BlockedPeriods.FirstOrDefault(b =>
                    conflictIds.Contains(b.RoomId) && b.Start <= now && b.End > now);
                if (liveBlock is not null)
                    clone.Status = RoomStatusFromBlockReason(liveBlock.Reason);
                else if (clone.Status is RoomStatus.Maintenance or RoomStatus.Blocked)
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

        return Task.FromResult<IReadOnlyList<Boardroom>>(rooms);
    }

    public Task<IReadOnlyList<string>> GetEquipmentCatalogAsync() =>
        Task.FromResult<IReadOnlyList<string>>(SeedData.EquipmentOptions);

    public Task<IReadOnlyList<string>> GetCateringCatalogAsync() =>
        Task.FromResult<IReadOnlyList<string>>(SeedData.CateringOptions);

    public Task<IReadOnlyList<CatalogItem>> GetEquipmentCatalogItemsAsync() =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(SeedData.EquipmentCatalog);

    public Task<IReadOnlyList<CatalogItem>> GetCateringCatalogItemsAsync() =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(SeedData.CateringCatalog);

    public void InvalidateCatalog()
    {
    }

    private static RoomStatus RoomStatusFromBlockReason(string reason)
    {
        if (reason.Contains("clean", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Cleaning;
        if (reason.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Maintenance;
        return RoomStatus.Blocked;
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
