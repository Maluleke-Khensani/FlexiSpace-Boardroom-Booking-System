using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Mock;

public class MockAdminService(
    MockDataStore store,
    IAuthService auth,
    IRoomService rooms,
    INotificationService notifications) : IAdminService
{
    public Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult<IReadOnlyList<User>>([]);

        var i = 1;
        foreach (var u in SeedData.Users)
        {
            if (u.ApiId == 0) u.ApiId = i;
            i++;
        }

        return Task.FromResult<IReadOnlyList<User>>(SeedData.Users.ToList());
    }

    public Task<AuthResult> AddUserAsync(AdminUserCreateRequest request)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult(new AuthResult { Success = false, Message = "Only Administrators can add users." });

        var email = request.Email.Trim();
        if (SeedData.Users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new AuthResult { Success = false, Message = "A user with that email already exists." });

        SeedData.Users.Add(new User
        {
            Name = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            Email = email,
            Role = request.Role,
            LocationId = request.LocationId,
            IsActive = true,
            Password = "demo123"
        });

        return Task.FromResult(new AuthResult { Success = true, Message = "User added." });
    }

    public Task<bool> SetUserActiveAsync(int apiUserId, bool isActive)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult(false);

        var user = SeedData.Users.FirstOrDefault(u => u.ApiId == apiUserId);
        if (user is null) return Task.FromResult(false);
        user.IsActive = isActive;
        return Task.FromResult(true);
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
            return new ReportSummary { HasAccess = false };

        var scopeLocation = user.Role == UserRole.CentreManager ? user.LocationId : null;
        var scopeLabel = string.IsNullOrEmpty(scopeLocation)
            ? "All Flexispace locations"
            : SeedData.Locations.FirstOrDefault(l => l.Id == scopeLocation)?.Name ?? scopeLocation;

        var allRooms = await rooms.GetRoomsAsync(scopeLocation);
        // Exclude virtual combined suite from room inventory counts
        var physicalRooms = allRooms.Where(r => !r.IsCombined).ToList();

        var bookings = store.Bookings
            .Where(b => string.IsNullOrEmpty(scopeLocation) || b.LocationId == scopeLocation)
            .ToList();

        var active = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7); // Monday
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var users = SeedData.Users;
        var employees = users.Where(u => u.Role != UserRole.Client).ToList();

        var mostUsedRoom = active
            .GroupBy(b => b.RoomName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var mostUsedLocation = active
            .GroupBy(b => b.LocationName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var weekdayGroups = active
            .Where(b => b.Start.Date >= weekStart && b.Start.Date < weekStart.AddDays(7))
            .GroupBy(b => b.Start.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToList();

        var busiest = weekdayGroups.OrderByDescending(x => x.Count).FirstOrDefault();

        var slotsToday = Math.Max(1, physicalRooms.Count) * 8.0;
        var occupiedHours = active
            .Where(b => b.Start.Date == today)
            .Sum(b => Math.Max(0, (b.End - b.Start).TotalHours));

        var activeBookers = active.Select(b => b.BookerId).Distinct().Count();
        var thisWeek = active.Count(b => b.Start.Date >= weekStart && b.Start.Date < weekStart.AddDays(7));
        var alerts = await notifications.GetNotificationsAsync();
        var unread = await notifications.GetUnreadCountAsync();

        var blocked = store.BlockedPeriods
            .Where(p =>
            {
                if (string.IsNullOrEmpty(scopeLocation)) return true;
                var room = SeedData.Rooms.FirstOrDefault(r => r.Id == p.RoomId);
                return room?.LocationId == scopeLocation;
            })
            .ToList();

        var totalForPercent = Math.Max(1, bookings.Count);

        return new ReportSummary
        {
            HasAccess = true,
            ScopeLabel = scopeLabel,
            GeneratedAt = DateTime.Now,

            TotalUsers = users.Count,
            EmployeeUsers = employees.Count,
            ClientUsers = users.Count(u => u.Role == UserRole.Client),
            StaffCount = users.Count(u => u.Role == UserRole.Staff),
            CentreManagerCount = users.Count(u => u.Role == UserRole.CentreManager),
            AdministratorCount = users.Count(u => u.Role == UserRole.Administrator),

            TotalBookings = bookings.Count,
            TodaysBookings = active.Count(b => b.Start.Date == today),
            ThisWeekBookings = thisWeek,
            ThisMonthBookings = active.Count(b => b.Start.Date >= monthStart),
            UpcomingBookings = active.Count(b => b.Start >= DateTime.Now),
            CancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            ConfirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed),
            AverageAttendees = active.Count == 0 ? 0 : Math.Round(active.Average(b => b.Attendees), 1),
            TotalAttendeesServed = active.Sum(b => b.Attendees),

            ActiveBookers = activeBookers,
            BookingsPerActiveUser = activeBookers == 0 ? 0 : Math.Round(active.Count / (double)activeBookers, 1),
            AvgBookingsPerDayThisWeek = Math.Round(thisWeek / 7.0, 1),
            AlertsGenerated = alerts.Count,
            UnreadAlerts = unread,
            BlockedPeriods = blocked.Count,
            RoomsInScope = physicalRooms.Count,
            OccupancyPercent = Math.Round(Math.Min(100, occupiedHours / slotsToday * 100), 1),
            MostUsedRoom = mostUsedRoom?.Key ?? "—",
            MostUsedLocation = mostUsedLocation?.Key ?? "—",
            BusiestDayLabel = busiest is null ? "—" : $"{busiest.Day} ({busiest.Count})",

            BookingsByLocation = active
                .GroupBy(b => b.LocationName)
                .OrderByDescending(g => g.Count())
                .Select(g => new NamedCount
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percent = Math.Round(g.Count() * 100.0 / Math.Max(1, active.Count), 0),
                    Detail = $"{g.Sum(b => b.Attendees)} attendees"
                })
                .ToList(),

            BookingsByStatus = bookings
                .GroupBy(b => b.Status.ToString())
                .OrderByDescending(g => g.Count())
                .Select(g => new NamedCount
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percent = Math.Round(g.Count() * 100.0 / totalForPercent, 0)
                })
                .ToList(),

            UsersByRole = users
                .GroupBy(u => RolePermissions.DisplayName(u.Role))
                .OrderByDescending(g => g.Count())
                .Select(g => new NamedCount
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percent = Math.Round(g.Count() * 100.0 / Math.Max(1, users.Count), 0)
                })
                .ToList(),

            TopRooms = active
                .GroupBy(b => b.RoomName)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new NamedCount
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Detail = g.First().LocationName,
                    Percent = Math.Round(g.Count() * 100.0 / Math.Max(1, active.Count), 0)
                })
                .ToList(),

            WeekdayActivity = Enum.GetValues<DayOfWeek>()
                .OrderBy(d => ((int)d + 6) % 7)
                .Select(d => new NamedCount
                {
                    Name = d.ToString()[..3],
                    Count = weekdayGroups.FirstOrDefault(x => x.Day == d)?.Count ?? 0
                })
                .ToList(),

            RecentActivity = BuildRecentActivity(bookings, blocked, alerts)
        };
    }

    private static List<ReportActivityItem> BuildRecentActivity(
        IReadOnlyList<Booking> bookings,
        IReadOnlyList<BlockedPeriod> blocked,
        IReadOnlyList<AppNotification> alerts)
    {
        var items = new List<ReportActivityItem>();

        foreach (var b in bookings.OrderByDescending(x => x.Start).Take(8))
        {
            items.Add(new ReportActivityItem
            {
                Title = b.Status == BookingStatus.Cancelled ? "Booking cancelled" : "Booking confirmed",
                Detail = $"{b.RoomName} · {b.LocationName} · {b.BookerName} · {b.Attendees} guests",
                WhenLabel = RelativeWhen(b.Start),
                Kind = b.Status == BookingStatus.Cancelled ? "Cancellation" : "Confirmation"
            });
        }

        foreach (var block in blocked.OrderByDescending(x => x.Start).Take(3))
        {
            items.Add(new ReportActivityItem
            {
                Title = "Room blocked",
                Detail = $"{block.RoomId} · {block.Reason} · by {block.CreatedBy}",
                WhenLabel = RelativeWhen(block.Start),
                Kind = "Info"
            });
        }

        foreach (var n in alerts.OrderByDescending(x => x.CreatedAt).Take(4))
        {
            items.Add(new ReportActivityItem
            {
                Title = n.Title,
                Detail = n.Message,
                WhenLabel = RelativeWhen(n.CreatedAt),
                Kind = n.Type
            });
        }

        return items
            .Take(12)
            .ToList();
    }

    private static string RelativeWhen(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalMinutes < 0) return when.ToString("ddd d MMM · HH:mm");
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return when.ToString("d MMM");
    }
}
