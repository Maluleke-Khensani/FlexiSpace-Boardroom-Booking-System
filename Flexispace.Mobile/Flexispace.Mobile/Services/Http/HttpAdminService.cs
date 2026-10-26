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

    public async Task<IReadOnlyList<User>> GetEntraDirectoryAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var users = await api.GetRequiredAsync<List<ApiEntraUserDto>>("api/EntraUser");
        return users.Select(MapEntraUser).ToList();
    }

    public async Task<AuthResult> ProvisionUserAsync(Guid entraObjectId, string? locationId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return new AuthResult { Success = false, Message = "Only Administrators can provision users." };
        if (entraObjectId == Guid.Empty)
            return new AuthResult { Success = false, Message = "This Microsoft identity could not be identified." };

        var locationApiId = ToLocationApiId(locationId);
        if (!string.IsNullOrWhiteSpace(locationId) && locationApiId is null)
            return new AuthResult { Success = false, Message = "Choose a valid location." };

        using var response = await api.PostAsJsonAsync("api/User/provision", new
        {
            entraObjectId,
            locationId = locationApiId
        });

        if (response.IsSuccessStatusCode)
            return new AuthResult { Success = true, Message = "User provisioned in FlexiSpace." };

        return new AuthResult { Success = false, Message = await ReadAdminErrorAsync(response, "Could not provision this user.") };
    }

    public async Task<AuthResult> UpdateUserLocationAsync(User user, string? locationId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return new AuthResult { Success = false, Message = "Only Administrators can update users." };
        if (user.ApiId <= 0)
            return new AuthResult { Success = false, Message = "User not found." };

        var locationApiId = ToLocationApiId(locationId);
        if (!string.IsNullOrWhiteSpace(locationId) && locationApiId is null)
            return new AuthResult { Success = false, Message = "Choose a valid location." };

        // The API's existing update contract requires names; send their current values unchanged.
        using var response = await api.PutAsJsonAsync($"api/User/{user.ApiId}", new
        {
            firstName = user.FirstName,
            lastName = user.LastName,
            locationId = locationApiId
        });

        return response.IsSuccessStatusCode
            ? new AuthResult { Success = true, Message = "User location updated." }
            : new AuthResult { Success = false, Message = await ReadAdminErrorAsync(response, "Could not update this user's location.") };
    }

    private User MapUser(ApiUserDto u) => new()
    {
        Id = u.EntraObjectId == Guid.Empty ? IdAdapter.ToGuid(u.Id) : u.EntraObjectId,
        ApiId = u.Id,
        EntraObjectId = u.EntraObjectId,
        Name = $"{u.FirstName} {u.LastName}".Trim(),
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Role = Enum.TryParse<UserRole>(u.Role, true, out var role) ? role : UserRole.Staff,
        LocationId = u.LocationId is int lid ? catalog.LocationSlugFromApi(lid) : null,
        CreatedAt = u.CreatedAt,
        IsActive = u.IsActive
    };

    private User MapEntraUser(ApiEntraUserDto u)
    {
        var name = string.IsNullOrWhiteSpace(u.DisplayName)
            ? $"{u.FirstName} {u.LastName}".Trim()
            : u.DisplayName;
        return new User
        {
            Id = u.EntraObjectId,
            EntraObjectId = u.EntraObjectId,
            Name = string.IsNullOrWhiteSpace(name) ? u.Email : name,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Role = u.Role ?? UserRole.Staff,
            HasAssignedFlexiSpaceRole = u.Role.HasValue,
            IsActive = u.IsActive
        };
    }

    private int? ToLocationApiId(string? locationId) =>
        string.IsNullOrWhiteSpace(locationId) ? null : catalog.LocationApiId(locationId);

    private static async Task<string> ReadAdminErrorAsync(HttpResponseMessage response, string fallback)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            var error = System.Text.Json.JsonSerializer.Deserialize<ApiErrorBody>(body, ApiClient.JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Message)) return error.Message;
        }
        catch (System.Text.Json.JsonException) { }
        return body.Trim().Trim('"');
    }

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
        var scopeLabel = user.Role == UserRole.CentreManager
            ? "Staff and client bookings"
            : string.IsNullOrEmpty(scopeLocation)
                ? "All Flexispace locations"
                : locations.FirstOrDefault(l => l.Id == scopeLocation)?.Name ?? scopeLocation;

        var allRooms = await rooms.GetRoomsAsync(scopeLocation);
        var physicalRooms = allRooms.Where(r => !r.IsCombined).ToList();
        var bookingList = user.Role == UserRole.CentreManager
            ? await bookings.GetBookingsForCurrentUserScopeAsync()
            : await bookings.GetBookingsAsync(locationId: scopeLocation);
        var active = bookingList.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var now = DateTime.Now;
        var today = DateTime.Today;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var users = await GetUsersForReportAsync();
        var hasUserDirectory = user.Role is UserRole.Administrator or UserRole.CentreManager;
        var unread = await notifications.GetUnreadCountAsync();
        var blockedCount = 0;
        foreach (var room in physicalRooms)
            blockedCount += (await bookings.GetBlockedPeriodsAsync(room.Id)).Count;

        var activeBookers = active.Select(b => b.BookerId).Distinct().Count();
        var mostUsedRoom = active.GroupBy(b => b.RoomName).OrderByDescending(g => g.Count()).FirstOrDefault();
        var mostUsedLocation = active.GroupBy(b => b.LocationName).OrderByDescending(g => g.Count()).FirstOrDefault();
        var weekday = active.GroupBy(b => b.Start.DayOfWeek).OrderByDescending(g => g.Count()).FirstOrDefault();

        return new ReportSummary
        {
            HasAccess = true,
            HasUserDirectory = hasUserDirectory,
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
            ActiveBookers = activeBookers,
            BookingsPerActiveUser = activeBookers == 0 ? 0 : Math.Round(active.Count / (double)activeBookers, 1),
            AvgBookingsPerDayThisWeek = active.Count(b => b.Start.Date >= weekStart) / 7.0,
            AlertsGenerated = (await notifications.GetNotificationsAsync()).Count,
            UnreadAlerts = unread,
            BlockedPeriods = blockedCount,
            RoomsInScope = physicalRooms.Count,
            InUseNow = active.Count(b => b.Start <= now && b.End > now),
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
        // Administrators see every user. Centre managers see their location.
        // Staff are not allowed to list the directory, so their report omits it.
        if (auth.CurrentUser!.Role is not (UserRole.Administrator or UserRole.CentreManager))
            return [];

        var users = await api.GetAsync<List<ApiUserDto>>("api/User") ?? [];
        return users.Select(MapUser).ToList();
    }
}
