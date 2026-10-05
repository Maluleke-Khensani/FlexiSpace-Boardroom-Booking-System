using System.Net.Http.Json;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpAdminService(
    ApiClient api,
    IAuthService auth,
    IRoomService rooms,
    IBookingService bookings,
    INotificationService notifications,
    CatalogSlugs catalog) : IAdminService
{
    public async Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var users = await api.GetAsync<List<ApiUserDto>>("api/User") ?? [];
        return users.Select(MapUser).ToList();
    }

    public async Task<AuthResult> AddUserAsync(AdminUserCreateRequest request)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return new AuthResult { Success = false, Message = "Only Administrators can add users." };

        int? locationApiId = null;
        if (!string.IsNullOrWhiteSpace(request.LocationId))
            locationApiId = catalog.LocationApiId(request.LocationId);

        using var response = await api.PostAsJsonAsync("api/User/directory", new
        {
            firstName = request.FirstName.Trim(),
            lastName = request.LastName.Trim(),
            email = request.Email.Trim(),
            role = request.Role,
            locationId = locationApiId
        });

        if (response.IsSuccessStatusCode)
            return new AuthResult { Success = true, Message = "User added." };

        try
        {
            var err = await response.Content.ReadFromJsonAsync<ApiErrorBody>(ApiClient.JsonOptions);
            return new AuthResult
            {
                Success = false,
                Message = err?.Message ?? "Could not add user."
            };
        }
        catch
        {
            return new AuthResult { Success = false, Message = "Could not add user." };
        }
    }

    public async Task<bool> SetUserActiveAsync(int apiUserId, bool isActive)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return false;

        using var response = await api.PatchAsJsonAsync($"api/User/{apiUserId}/status", new { isActive });
        return response.IsSuccessStatusCode;
    }

    private User MapUser(ApiUserDto u) => new()
    {
        Id = u.EntraObjectId == Guid.Empty ? IdAdapter.ToGuid(u.Id) : u.EntraObjectId,
        ApiId = u.Id,
        Name = $"{u.FirstName} {u.LastName}".Trim(),
        Email = u.Email,
        Role = Enum.TryParse<UserRole>(u.Role, true, out var role) ? role : UserRole.Staff,
        LocationId = u.LocationId is int lid ? catalog.LocationSlugFromApi(lid) : null,
        IsActive = u.IsActive
    };

    public async Task<bool> AddRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;

        var locationApiId = catalog.LocationApiId(room.LocationId);
        if (locationApiId is null) return false;

        var body = new
        {
            name = room.Name,
            capacity = room.Capacity,
            status = "Available",
            locationId = locationApiId.Value,
            equipment = Array.Empty<object>()
        };

        using var response = await api.PostAsJsonAsync("api/Boardroom", body);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveRoomAsync(string roomId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;

        var apiId = catalog.RoomApiId(roomId);
        if (apiId is null) return false;

        using var response = await api.DeleteAsync($"api/Boardroom/{apiId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanViewReports(user.Role))
            return new ReportSummary { HasAccess = false };

        var scopeLocation = user.Role == UserRole.CentreManager ? user.LocationId : null;
        var locations = await rooms.GetLocationsAsync();
        var scopeLabel = string.IsNullOrEmpty(scopeLocation)
            ? "All Flexispace locations"
            : locations.FirstOrDefault(l => l.Id == scopeLocation)?.Name ?? scopeLocation;

        var allRooms = await rooms.GetRoomsAsync(scopeLocation);
        var physicalRooms = allRooms.Where(r => !r.IsCombined).ToList();
        var bookingList = await bookings.GetBookingsAsync(locationId: scopeLocation);
        var active = bookingList.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var users = await GetUsersForReportAsync();
        var unread = await notifications.GetUnreadCountAsync();
        var blockedCount = 0;
        foreach (var room in physicalRooms)
            blockedCount += (await bookings.GetBlockedPeriodsAsync(room.Id)).Count;

        var mostUsedRoom = active.GroupBy(b => b.RoomName).OrderByDescending(g => g.Count()).FirstOrDefault();
        var mostUsedLocation = active.GroupBy(b => b.LocationName).OrderByDescending(g => g.Count()).FirstOrDefault();
        var weekday = active.GroupBy(b => b.Start.DayOfWeek).OrderByDescending(g => g.Count()).FirstOrDefault();

        return new ReportSummary
        {
            HasAccess = true,
            ScopeLabel = scopeLabel,
            GeneratedAt = DateTime.Now,
            TotalUsers = users.Count,
            EmployeeUsers = users.Count(u => u.Role != UserRole.Client),
            ClientUsers = users.Count(u => u.Role == UserRole.Client),
            StaffCount = users.Count(u => u.Role == UserRole.Staff),
            CentreManagerCount = users.Count(u => u.Role == UserRole.CentreManager),
            AdministratorCount = users.Count(u => u.Role == UserRole.Administrator),
            TotalBookings = bookingList.Count,
            TodaysBookings = active.Count(b => b.Start.Date == today),
            ThisWeekBookings = active.Count(b => b.Start.Date >= weekStart),
            ThisMonthBookings = active.Count(b => b.Start.Date >= monthStart),
            UpcomingBookings = active.Count(b => b.Start >= DateTime.Now),
            CancelledBookings = bookingList.Count(b => b.Status == BookingStatus.Cancelled),
            ConfirmedBookings = active.Count(b => b.Status == BookingStatus.Confirmed),
            AverageAttendees = active.Count == 0 ? 0 : active.Average(b => b.Attendees),
            TotalAttendeesServed = active.Sum(b => b.Attendees),
            ActiveBookers = active.Select(b => b.BookerId).Distinct().Count(),
            BookingsPerActiveUser = 0,
            AvgBookingsPerDayThisWeek = active.Count(b => b.Start.Date >= weekStart) / 7.0,
            AlertsGenerated = (await notifications.GetNotificationsAsync()).Count,
            UnreadAlerts = unread,
            BlockedPeriods = blockedCount,
            RoomsInScope = physicalRooms.Count,
            OccupancyPercent = physicalRooms.Count == 0 ? 0 : Math.Min(100, active.Count(b => b.Start.Date == today) * 100.0 / physicalRooms.Count),
            MostUsedRoom = mostUsedRoom?.Key ?? "—",
            MostUsedLocation = mostUsedLocation?.Key ?? "—",
            BusiestDayLabel = weekday is null ? "—" : weekday.Key.ToString(),
            BookingsByLocation = active.GroupBy(b => b.LocationName).Select(g => new NamedCount { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToList(),
            BookingsByStatus = bookingList.GroupBy(b => b.Status.ToString()).Select(g => new NamedCount { Name = g.Key, Count = g.Count() }).ToList(),
            UsersByRole = users.GroupBy(u => u.Role.ToString()).Select(g => new NamedCount { Name = g.Key, Count = g.Count() }).ToList(),
            TopRooms = active.GroupBy(b => b.RoomName).Select(g => new NamedCount { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(5).ToList(),
            WeekdayActivity = Enum.GetValues<DayOfWeek>().Select(d => new NamedCount
            {
                Name = d.ToString()[..3],
                Count = active.Count(b => b.Start.DayOfWeek == d)
            }).ToList(),
            RecentActivity = active.OrderByDescending(b => b.Start).Take(8).Select(b => new ReportActivityItem
            {
                Title = b.RoomName,
                Detail = $"{b.LocationName} · {b.Company}",
                WhenLabel = b.Start.ToString("ddd d MMM HH:mm"),
                Kind = b.Status.ToString()
            }).ToList()
        };
    }

    private async Task<List<User>> GetUsersForReportAsync()
    {
        if (RolePermissions.CanManageUsers(auth.CurrentUser!.Role))
            return (await GetUsersAsync()).ToList();

        // Centre managers: approximate from seed demo directory for role counts.
        return SeedData.Users
            .Where(u => string.IsNullOrEmpty(auth.CurrentUser.LocationId) || u.LocationId == auth.CurrentUser.LocationId || u.Role == UserRole.Administrator)
            .ToList();
    }
}
