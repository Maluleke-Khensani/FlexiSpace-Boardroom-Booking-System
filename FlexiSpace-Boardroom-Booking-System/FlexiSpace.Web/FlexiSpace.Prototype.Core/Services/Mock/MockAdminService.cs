using Flexispace.Core.Helpers;
using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

public class MockAdminService(MockDataStore store, IAuthService auth, IRoomService rooms, INotificationService notifications) : IAdminService
{
    public Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult<IReadOnlyList<User>>([]);

        return Task.FromResult<IReadOnlyList<User>>(SeedData.Users.ToList());
    }

    public Task<IReadOnlyList<User>> GetEntraDirectoryAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult<IReadOnlyList<User>>([]);

        IReadOnlyList<User> directory =
        [
            .. SeedData.Users.Select(u => new User
            {
                Id = u.Id,
                ApiId = u.ApiId,
                Name = u.Name,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Role = u.Role,
                LocationId = u.LocationId,
                IsActive = u.IsActive,
                EntraObjectId = u.Id
            }),
            new User
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                EntraObjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                FirstName = "Sipho",
                LastName = "Mahlangu",
                Name = "Sipho Mahlangu",
                Email = "sipho.mahlangu@fspace.onmicrosoft.com",
                Role = UserRole.Staff,
                IsActive = true
            },
            new User
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                EntraObjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                FirstName = "Naledi",
                LastName = "Khumalo",
                Name = "Naledi Khumalo",
                Email = "naledi.khumalo@fspace.onmicrosoft.com",
                Role = UserRole.Staff,
                IsActive = true
            }
        ];

        return Task.FromResult(directory);
    }

    public Task<(bool Ok, string Message)> CreateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult((false, "Only Administrators can add users."));
        if (SeedData.Users.Any(u => u.Email.Equals(user.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult((false, "A FlexiSpace account already exists for this email address."));

        user.Id = Guid.NewGuid();
        user.ApiId = SeedData.Users.Count == 0 ? 1 : SeedData.Users.Max(u => u.ApiId) + 1;
        user.Name = $"{user.FirstName} {user.LastName}".Trim();
        user.IsActive = true;
        user.CreatedAt = DateTime.UtcNow;
        SeedData.Users.Add(user);
        return Task.FromResult((true, "User added."));
    }

    public Task<(bool Ok, string Message)> ProvisionUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult((false, "Only Administrators can provision users."));
        if (user.EntraObjectId == Guid.Empty)
            return Task.FromResult((false, "This Microsoft identity could not be identified."));
        if (SeedData.Users.Any(u => u.Email.Equals(user.Email.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                    u.EntraObjectId == user.EntraObjectId))
            return Task.FromResult((false, "This user is already provisioned."));

        user.Id = user.EntraObjectId;
        user.ApiId = SeedData.Users.Count == 0 ? 1 : SeedData.Users.Max(u => u.ApiId) + 1;
        user.Name = $"{user.FirstName} {user.LastName}".Trim();
        user.IsActive = true;
        user.CreatedAt = DateTime.UtcNow;
        SeedData.Users.Add(user);
        return Task.FromResult((true, $"{user.Name} is now provisioned in FlexiSpace."));
    }

    public Task<(bool Ok, string Message)> UpdateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult((false, "Only Administrators can update users."));

        var existing = SeedData.Users.FirstOrDefault(u => u.Id == user.Id || u.ApiId == user.ApiId);
        if (existing is null)
            return Task.FromResult((false, "User not found."));

        existing.FirstName = user.FirstName;
        existing.LastName = user.LastName;
        existing.Email = user.Email;
        existing.Name = $"{user.FirstName} {user.LastName}".Trim();
        existing.Role = user.Role;
        existing.LocationId = user.LocationId;
        return Task.FromResult((true, "User updated."));
    }

    public Task<(bool Ok, string Message)> RemoveUserAsync(int apiId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult((false, "Only Administrators can remove users."));

        return SetUserActiveAsync(apiId, false);
    }

    public Task<(bool Ok, string Message)> SetUserActiveAsync(int apiId, bool isActive)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult((false, "Only Administrators can update users."));

        var existing = SeedData.Users.FirstOrDefault(u => u.ApiId == apiId);
        if (existing is null)
            return Task.FromResult((false, "User not found."));
        if (!isActive && existing.Id == auth.CurrentUser.Id)
            return Task.FromResult((false, "You cannot remove your own account."));

        existing.IsActive = isActive;
        return Task.FromResult((true, isActive
            ? "User activated."
            : "User deactivated. Open Edit to activate them again."));
    }

    public async Task<bool> AddRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;

        if (string.IsNullOrWhiteSpace(room.Name) || string.IsNullOrWhiteSpace(room.LocationId))
            return false;

        room.Id = $"{room.LocationId}-{Guid.NewGuid().ToString("N")[..6]}";
        SeedData.Rooms.Add(room);
        await Task.CompletedTask;
        return true;
    }

    public Task<bool> RemoveRoomAsync(string roomId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return Task.FromResult(false);

        var room = SeedData.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return Task.FromResult(false);
        SeedData.Rooms.Remove(room);
        return Task.FromResult(true);
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanViewReports(user.Role))
            return new ReportSummary();

        var scopeLocation = user.Role == UserRole.CentreManager ? user.LocationId : null;
        var locations = await rooms.GetLocationsAsync();
        var roomList = await rooms.GetRoomsAsync(scopeLocation);
        var bookingList = string.IsNullOrEmpty(scopeLocation)
            ? store.Bookings
            : store.Bookings.Where(b => b.LocationId == scopeLocation).ToList();
        var alerts = await notifications.GetNotificationsAsync();
        var unread = await notifications.GetUnreadCountAsync();

        return ReportSummaryBuilder.Build(
            user,
            locations,
            roomList,
            bookingList,
            SeedData.Users,
            alerts.Count,
            unread,
            store.BlockedPeriods.Count);
    }
}
