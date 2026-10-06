using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Mock;

public class MockRoomService(IAuthService auth, MockDataStore store) : IRoomService
{
    public Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync()
    {
        var scope = RolePermissions.ScopedLocationId(auth.CurrentUser);
        IEnumerable<OfficeLocation> locations = SeedData.Locations;
        if (!string.IsNullOrEmpty(scope))
            locations = locations.Where(l => l.Id == scope);

        return Task.FromResult<IReadOnlyList<OfficeLocation>>(locations.ToList());
    }

    public Task<OfficeLocation?> GetLocationAsync(string locationId)
    {
        if (!RolePermissions.CanAccessLocation(auth.CurrentUser, locationId))
            return Task.FromResult<OfficeLocation?>(null);

        return Task.FromResult(SeedData.Locations.FirstOrDefault(l => l.Id == locationId));
    }

    public Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null)
    {
        locationId = EffectiveLocationFilter(locationId);
        var rooms = SeedData.Rooms.AsEnumerable();
        if (!string.IsNullOrEmpty(locationId))
            rooms = rooms.Where(r => r.LocationId == locationId);

        var withCombine = RoomCombinations.WithCombineOptions(rooms, locationId);
        return Task.FromResult<IReadOnlyList<Boardroom>>(withCombine);
    }

    public Task<Boardroom?> GetRoomAsync(string roomId)
    {
        if (RoomCombinations.IsCombinedOption(roomId))
        {
            var combined = RoomCombinations.CreateCombinedOption();
            if (!RolePermissions.CanAccessLocation(auth.CurrentUser, combined.LocationId))
                return Task.FromResult<Boardroom?>(null);
            return Task.FromResult<Boardroom?>(combined);
        }

        var room = SeedData.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return Task.FromResult<Boardroom?>(null);
        if (!RolePermissions.CanAccessLocation(auth.CurrentUser, room.LocationId))
            return Task.FromResult<Boardroom?>(null);

        var clone = new Boardroom
        {
            Id = room.Id,
            Name = room.Name,
            LocationId = room.LocationId,
            Capacity = room.Capacity,
            Equipment = [.. room.Equipment],
            ImageKey = room.ImageKey,
            Status = room.Status
        };
        RoomCombinations.ApplyPairHints(clone);
        return Task.FromResult<Boardroom?>(clone);
    }

    public Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date)
    {
        locationId = EffectiveLocationFilter(locationId);
        var day = date.Date;
        var now = DateTime.Now;
        var rooms = SeedData.Rooms
            .Where(r => string.IsNullOrEmpty(locationId) || r.LocationId == locationId)
            .Select(r =>
            {
                var clone = new Boardroom
                {
                    Id = r.Id,
                    Name = r.Name,
                    LocationId = r.LocationId,
                    Capacity = r.Capacity,
                    Equipment = [.. r.Equipment],
                    ImageKey = r.ImageKey
                };
                RoomCombinations.ApplyPairHints(clone);

                var conflictIds = RoomCombinations.ConflictRoomIds(r.Id);
                var active = store.Bookings
                    .Where(b => conflictIds.Contains(b.RoomId) &&
                                b.Status != BookingStatus.Cancelled &&
                                b.Start.Date == day)
                    .ToList();

                if (store.BlockedPeriods.Any(b =>
                        conflictIds.Contains(b.RoomId) &&
                        b.Start.Date <= day &&
                        b.End > day))
                {
                    clone.Status = RoomStatus.Blocked;
                }
                else if (active.Any(b => b.Start <= now && b.End > now))
                    clone.Status = RoomStatus.Occupied;
                else if (active.Any(b => b.Start > now && b.Start < now.AddHours(2)))
                    clone.Status = RoomStatus.Reserved;
                else if (active.Count > 0 && day == DateTime.Today)
                    clone.Status = RoomStatus.Reserved;
                else
                    clone.Status = RoomStatus.Available;

                // Demo maintenance room when not otherwise blocked
                if (r.Id == "cen-t" && day == DateTime.Today && clone.Status == RoomStatus.Available)
                    clone.Status = RoomStatus.Maintenance;

                return clone;
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<Boardroom>>(rooms);
    }

    /// <summary>
    /// Centre Managers never query “all locations” — empty/null filters collapse to their centre.
    /// </summary>
    private string? EffectiveLocationFilter(string? locationId)
    {
        var scope = RolePermissions.ScopedLocationId(auth.CurrentUser);
        if (string.IsNullOrEmpty(scope))
            return locationId;

        if (string.IsNullOrEmpty(locationId) || locationId == scope)
            return scope;

        // Out-of-scope filter requested — return a filter that matches nothing.
        return "__out_of_scope__";
    }
}
