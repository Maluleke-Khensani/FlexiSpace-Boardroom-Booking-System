using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpRoomService(
    ApiClient api,
    IAuthService auth,
    CatalogSlugs catalog) : IRoomService
{
    private List<OfficeLocation>? _locations;
    private List<Boardroom>? _rooms;

    public async Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync()
    {
        await EnsureLoadedAsync();
        var scope = RolePermissions.ScopedLocationId(auth.CurrentUser);
        IEnumerable<OfficeLocation> locations = _locations!;
        if (!string.IsNullOrEmpty(scope))
            locations = locations.Where(l => l.Id == scope);
        return locations.ToList();
    }

    public async Task<OfficeLocation?> GetLocationAsync(string locationId)
    {
        if (!RolePermissions.CanAccessLocation(auth.CurrentUser, locationId))
            return null;
        await EnsureLoadedAsync();
        return _locations!.FirstOrDefault(l => l.Id == locationId);
    }

    public async Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null)
    {
        await EnsureLoadedAsync();
        locationId = EffectiveLocationFilter(locationId);
        var rooms = _rooms!.AsEnumerable();
        if (!string.IsNullOrEmpty(locationId))
            rooms = rooms.Where(r => r.LocationId == locationId);
        return RoomCombinations.WithCombineOptions(rooms, locationId);
    }

    public async Task<Boardroom?> GetRoomAsync(string roomId)
    {
        if (RoomCombinations.IsCombinedOption(roomId))
        {
            var combined = RoomCombinations.CreateCombinedOption();
            if (!RolePermissions.CanAccessLocation(auth.CurrentUser, combined.LocationId))
                return null;
            return combined;
        }

        await EnsureLoadedAsync();
        var room = _rooms!.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return null;
        if (!RolePermissions.CanAccessLocation(auth.CurrentUser, room.LocationId))
            return null;

        var clone = Clone(room);
        RoomCombinations.ApplyPairHints(clone);
        return clone;
    }

    public async Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date)
    {
        await EnsureLoadedAsync();
        locationId = EffectiveLocationFilter(locationId);
        var day = date.Date;
        var now = DateTime.Now;

        var bookingDtos = await api.GetAsync<List<ApiBookingDto>>("api/Booking") ?? [];
        var dayBookings = bookingDtos
            .Where(b => b.BookingDate == DateOnly.FromDateTime(day))
            .Where(b => string.IsNullOrEmpty(locationId) || catalog.LocationSlugFromApi(b.LocationId) == locationId)
            .Select(MapBookingLite)
            .ToList();

        var blocked = await LoadBlockedAsync(locationId);

        var rooms = _rooms!
            .Where(r => string.IsNullOrEmpty(locationId) || r.LocationId == locationId)
            .Select(r =>
            {
                var clone = Clone(r);
                RoomCombinations.ApplyPairHints(clone);
                var conflictIds = RoomCombinations.ConflictRoomIds(r.Id);
                var active = dayBookings
                    .Where(b => conflictIds.Contains(b.RoomId) && b.Status != BookingStatus.Cancelled)
                    .ToList();

                if (blocked.Any(b =>
                        conflictIds.Contains(b.RoomId) &&
                        b.Start.Date <= day &&
                        b.End > day))
                    clone.Status = RoomStatus.Blocked;
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

        return rooms;
    }

    private Booking MapBookingLite(ApiBookingDto dto) => new()
    {
        Id = IdAdapter.ToGuid(dto.Id),
        RoomId = catalog.RoomSlugFromApi(dto.BoardroomId),
        LocationId = catalog.LocationSlugFromApi(dto.LocationId),
        Start = dto.BookingDate.ToDateTime(dto.StartTime),
        End = dto.BookingDate.ToDateTime(dto.EndTime),
        Status = Enum.TryParse<BookingStatus>(dto.Status, true, out var s) ? s : BookingStatus.Confirmed
    };

    private async Task EnsureLoadedAsync()
    {
        if (_locations is not null && _rooms is not null)
            return;

        var locations = await api.GetAsync<List<ApiLocationDto>>("api/Location") ?? [];
        var rooms = await api.GetAsync<List<ApiBoardroomDto>>("api/Boardroom") ?? [];

        foreach (var loc in locations)
            catalog.RegisterLocation(loc.Id, loc.Name);
        foreach (var room in rooms)
            catalog.RegisterRoom(room.Id, room.Name, room.LocationId);

        _locations = locations.Select(MapLocation).ToList();
        _rooms = rooms.Select(MapRoom).ToList();
    }

    private async Task<IReadOnlyList<BlockedPeriod>> LoadBlockedAsync(string? locationId)
    {
        var url = "api/BlockedPeriod";
        if (!string.IsNullOrEmpty(locationId) && catalog.LocationApiId(locationId) is int lid)
            url += $"?locationId={lid}";

        var items = await api.GetAsync<List<ApiBlockedPeriodDto>>(url) ?? [];
        return items.Select(MapBlocked).ToList();
    }

    private OfficeLocation MapLocation(ApiLocationDto dto)
    {
        var slug = catalog.LocationSlugFromApi(dto.Id);
        var seed = SeedData.Locations.FirstOrDefault(l => l.Id == slug);
        return new OfficeLocation
        {
            Id = slug,
            Name = dto.Name,
            Address = dto.Address,
            Phone = seed?.Phone ?? string.Empty,
            CentreManager = seed?.CentreManager ?? string.Empty,
            ManagerEmail = seed?.ManagerEmail ?? string.Empty,
            ImageKey = seed?.ImageKey ?? "loc_centurion.jpg",
            Tagline = seed?.Tagline ?? string.Empty
        };
    }

    private Boardroom MapRoom(ApiBoardroomDto dto)
    {
        var slug = catalog.RoomSlugFromApi(dto.Id);
        var locationSlug = catalog.LocationSlugFromApi(dto.LocationId);
        var seed = SeedData.Rooms.FirstOrDefault(r => r.Id == slug);
        return new Boardroom
        {
            Id = slug,
            Name = dto.Name,
            LocationId = locationSlug,
            Capacity = dto.Capacity,
            Equipment = seed?.Equipment?.ToList() ?? [],
            ImageKey = seed?.ImageKey ?? "room_meeting",
            Status = Enum.TryParse<RoomStatus>(dto.Status, true, out var status) ? status : RoomStatus.Available
        };
    }

    private BlockedPeriod MapBlocked(ApiBlockedPeriodDto dto) => new()
    {
        Id = IdAdapter.ToGuid(dto.Id),
        RoomId = catalog.RoomSlugFromApi(dto.BoardroomId),
        Start = dto.StartDate.ToDateTime(dto.StartTime),
        End = dto.EndDate.ToDateTime(dto.EndTime),
        Reason = dto.Reason,
        CreatedBy = dto.CreatedByName
    };

    private string? EffectiveLocationFilter(string? locationId)
    {
        var scope = RolePermissions.ScopedLocationId(auth.CurrentUser);
        if (string.IsNullOrEmpty(scope))
            return locationId;

        if (string.IsNullOrEmpty(locationId) || locationId == scope)
            return scope;

        return "__out_of_scope__";
    }

    private static Boardroom Clone(Boardroom r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        LocationId = r.LocationId,
        Capacity = r.Capacity,
        Equipment = [.. r.Equipment],
        Status = r.Status,
        ImageKey = r.ImageKey,
        IsCombined = r.IsCombined,
        CombinableWithRoomId = r.CombinableWithRoomId,
        CombineHint = r.CombineHint,
        CapacityLabel = r.CapacityLabel
    };
}
