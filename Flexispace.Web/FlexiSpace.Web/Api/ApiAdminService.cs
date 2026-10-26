using System.Net.Http.Json;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiAdminService(
    FlexiSpaceApiClient api,
    IAuthService auth,
    IRoomService rooms,
    IBookingService bookings,
    INotificationService notifications) : IAdminService
{
    public async Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var dtos = await api.GetAsync<List<UserDto>>("api/user") ?? [];
        return dtos.Select(CatalogMapper.ToUser).ToList();
    }

    public async Task<IReadOnlyList<User>> GetEntraDirectoryAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var dtos = await api.GetRequiredAsync<List<EntraUserDto>>("api/entrauser");
        return dtos.Select(ToEntraUser).ToList();
    }

    public async Task<(bool Ok, string Message)> CreateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can add users.");

        int? locationId = null;
        if (!string.IsNullOrWhiteSpace(user.LocationId))
        {
            if (!IdMap.TryToInt(user.LocationId, out var parsed))
                return (false, "Choose a valid location.");
            locationId = parsed;
        }

        var response = await api.PostAsJsonAsync("api/user/directory", new UserCreatePayload
        {
            FirstName = user.FirstName.Trim(),
            LastName = user.LastName.Trim(),
            Email = user.Email.Trim(),
            Role = CatalogMapper.ToApiRole(user.Role),
            LocationId = locationId,
            EntraObjectId = user.EntraObjectId == Guid.Empty ? null : user.EntraObjectId
        });

        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        var created = await response.Content.ReadFromJsonAsync<UserCreateResultDto>(FlexiSpaceApiClient.Json);
        return (true, string.IsNullOrWhiteSpace(created?.Message) ? "User added." : created.Message);
    }

    public async Task<(bool Ok, string Message)> ProvisionUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can provision users.");
        if (user.EntraObjectId == Guid.Empty)
            return (false, "This Microsoft identity could not be identified.");
        if (user.Role == UserRole.CentreManager && string.IsNullOrWhiteSpace(user.LocationId))
            return (false, "Centre Managers need a location.");

        int? locationId = null;
        if (!string.IsNullOrWhiteSpace(user.LocationId))
        {
            if (!IdMap.TryToInt(user.LocationId, out var parsed))
                return (false, "Choose a valid location.");
            locationId = parsed;
        }

        var provision = await api.PostAsJsonAsync("api/user/provision", new UserProvisionPayload
        {
            EntraObjectId = user.EntraObjectId,
            LocationId = locationId
        });
        if (provision.IsSuccessStatusCode)
            return (true, $"{user.Name} is now provisioned in FlexiSpace.");
        return (false, await FlexiSpaceApiClient.ReadErrorAsync(provision));
    }

    public async Task<(bool Ok, string Message)> UpdateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can update users.");
        if (user.ApiId <= 0)
            return (false, "User not found.");

        int? locationId = null;
        if (!string.IsNullOrWhiteSpace(user.LocationId))
        {
            if (!IdMap.TryToInt(user.LocationId, out var parsed))
                return (false, "Choose a valid location.");
            locationId = parsed;
        }

        var response = await api.PutAsJsonAsync($"api/user/{user.ApiId}", new UserUpdatePayload
        {
            FirstName = user.FirstName.Trim(),
            LastName = user.LastName.Trim(),
            Email = user.Email.Trim(),
            Role = CatalogMapper.ToApiRole(user.Role),
            LocationId = locationId
        });

        return response.IsSuccessStatusCode
            ? (true, "User updated.")
            : (false, await FlexiSpaceApiClient.ReadErrorAsync(response));
    }

    public async Task<(bool Ok, string Message)> RemoveUserAsync(int apiId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can remove users.");
        if (apiId <= 0)
            return (false, "This user could not be identified. Refresh the page and try again.");

        return await SetUserActiveAsync(apiId, false);
    }

    public async Task<(bool Ok, string Message)> SetUserActiveAsync(int apiId, bool isActive)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can update users.");
        if (apiId <= 0)
            return (false, "User not found.");

        var response = await api.PatchAsJsonAsync($"api/user/{apiId}/status", new UserStatusPayload { IsActive = isActive });
        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        return (true, isActive
            ? "User activated."
            : "User deactivated. Open Edit to activate them again.");
    }

    public async Task<(bool Ok, string Message)> AddRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return (false, "Only Administrators can add boardrooms.");
        if (string.IsNullOrWhiteSpace(room.Name) || !IdMap.TryToInt(room.LocationId, out var locationId))
            return (false, "Choose a location and enter a boardroom name.");

        var catalog = await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? [];
        var equipment = room.Equipment
            .Select(name => catalog.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(e => e is not null)
            .Select(e => new BoardroomEquipmentDto { EquipmentId = e!.Id, Quantity = 1 })
            .ToList();

        var response = await api.PostAsJsonAsync("api/boardroom", new BoardroomCreatePayload
        {
            Name = room.Name.Trim(),
            Capacity = Math.Max(1, room.Capacity),
            LocationId = locationId,
            Status = 0,
            Equipment = equipment
        });

        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, $"{room.Name.Trim()} was added.");
    }

    public async Task<(bool Ok, string Message)> UpdateRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return (false, "Only Administrators can update boardrooms.");
        if (!IdMap.TryToInt(room.Id, out var id) ||
            string.IsNullOrWhiteSpace(room.Name) ||
            !IdMap.TryToInt(room.LocationId, out var locationId))
            return (false, "Choose a location and enter a boardroom name.");

        var catalog = await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? [];
        var equipment = room.Equipment
            .Select(name => catalog.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(e => e is not null)
            .Select(e => new BoardroomEquipmentDto { EquipmentId = e!.Id, Quantity = 1 })
            .ToList();

        var response = await api.PutAsJsonAsync($"api/boardroom/{id}", new BoardroomUpdatePayload
        {
            Name = room.Name.Trim(),
            Capacity = Math.Max(1, room.Capacity),
            LocationId = locationId,
            Status = ToApiRoomStatus(room.Status),
            Equipment = equipment
        });

        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, $"{room.Name.Trim()} was updated.");
    }

    public async Task<(bool Ok, string Message)> RemoveRoomAsync(string roomId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return (false, "Only Administrators can remove boardrooms.");
        if (!IdMap.TryToInt(roomId, out var id))
            return (false, "Boardroom not found.");

        var response = await api.DeleteAsync($"api/boardroom/{id}");
        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, "Boardroom removed.");
    }

    public async Task<(bool Ok, string Message)> CreateLocationAsync(OfficeLocation location)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanConfigureLocations(auth.CurrentUser.Role))
            return (false, "Only Administrators can add locations.");
        if (string.IsNullOrWhiteSpace(location.Name))
            return (false, "Enter a location name.");

        var response = await api.PostAsJsonAsync("api/location", new LocationWritePayload
        {
            Name = location.Name.Trim(),
            Address = location.Address.Trim()
        });
        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, $"{location.Name.Trim()} was added.");
    }

    public async Task<(bool Ok, string Message)> UpdateLocationAsync(OfficeLocation location)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanConfigureLocations(auth.CurrentUser.Role))
            return (false, "Only Administrators can update locations.");
        if (!IdMap.TryToInt(location.Id, out var id))
            return (false, "Location not found.");

        var response = await api.PutAsJsonAsync($"api/location/{id}", new LocationWritePayload
        {
            Name = location.Name.Trim(),
            Address = location.Address.Trim()
        });
        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, $"{location.Name.Trim()} was updated.");
    }

    public async Task<(bool Ok, string Message)> DeleteLocationAsync(string locationId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanConfigureLocations(auth.CurrentUser.Role))
            return (false, "Only Administrators can remove locations.");
        if (!IdMap.TryToInt(locationId, out var id))
            return (false, "Location not found.");

        var response = await api.DeleteAsync($"api/location/{id}");
        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        rooms.InvalidateCatalog();
        return (true, "Location removed.");
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAuditAsync(int count = 100)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var dtos = await api.GetAsync<List<AuditLogDto>>($"api/auditlog/recent?count={Math.Clamp(count, 1, 500)}") ?? [];
        return dtos.Select(dto => new AuditLogEntry
        {
            Id = dto.Id,
            ActingUserId = dto.ActingUserId,
            ActingUserName = string.IsNullOrWhiteSpace(dto.ActingUserName) ? "—" : dto.ActingUserName,
            Action = dto.Action,
            EntityName = dto.EntityName,
            EntityId = dto.EntityId,
            Timestamp = dto.Timestamp.Kind == DateTimeKind.Utc ? dto.Timestamp.ToLocalTime() : dto.Timestamp,
            OldValues = dto.OldValues,
            NewValues = dto.NewValues
        }).ToList();
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanViewReports(user.Role))
            return new ReportSummary();

        var scopeLocation = user.Role == UserRole.CentreManager ? user.LocationId : null;
        var locations = await rooms.GetLocationsAsync();
        var roomList = await rooms.GetRoomsAsync(scopeLocation);
        var bookingList = await bookings.GetBookingsAsync(locationId: scopeLocation);
        var reportUsers = await GetUsersForReportAsync();
        var alerts = await notifications.GetNotificationsAsync();
        var unread = await notifications.GetUnreadCountAsync();
        var blocked = await api.GetAsync<List<BlockedPeriodDto>>("api/blockedperiod") ?? [];

        return ReportSummaryBuilder.Build(
            user,
            locations,
            roomList,
            bookingList,
            reportUsers,
            alerts.Count,
            unread,
            blocked.Count);
    }

    private async Task<IReadOnlyList<User>> GetUsersForReportAsync()
    {
        if (auth.CurrentUser is not null && RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return await GetUsersAsync();

        var dtos = await api.GetAsync<List<UserDto>>("api/user") ?? [];
        return dtos.Select(CatalogMapper.ToUser).ToList();
    }

    private static int ToApiRoomStatus(RoomStatus status) => status switch
    {
        RoomStatus.Maintenance => 1,
        RoomStatus.Blocked => 2,
        RoomStatus.Cleaning => 3,
        _ => 0
    };

    private static User ToEntraUser(EntraUserDto dto)
    {
        var first = dto.FirstName.Trim();
        var last = dto.LastName.Trim();
        var name = $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Email : dto.DisplayName;

        return new User
        {
            Id = dto.EntraObjectId == Guid.Empty ? Guid.NewGuid() : dto.EntraObjectId,
            EntraObjectId = dto.EntraObjectId,
            FirstName = first,
            LastName = last,
            Name = name,
            Email = dto.Email,
            Role = dto.Role ?? UserRole.Staff,
            HasAssignedFlexiSpaceRole = dto.Role.HasValue,
            IsActive = dto.IsActive
        };
    }
}
