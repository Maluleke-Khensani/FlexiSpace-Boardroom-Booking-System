using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Core.Helpers;

public static class ReportSummaryBuilder
{
    public static ReportSummary Build(
        User currentUser,
        IReadOnlyList<OfficeLocation> locations,
        IReadOnlyList<Boardroom> rooms,
        IReadOnlyList<Booking> bookingList,
        IReadOnlyList<User> users,
        int alertsGenerated,
        int unreadAlerts,
        int blockedPeriods)
    {
        var scopeLocation = currentUser.Role == UserRole.CentreManager ? currentUser.LocationId : null;
        var scopeLabel = string.IsNullOrEmpty(scopeLocation)
            ? "All Flexispace locations"
            : locations.FirstOrDefault(l => l.Id == scopeLocation)?.Name ?? scopeLocation;

        var physicalRooms = rooms.Where(r => !r.IsCombined).ToList();
        var active = bookingList.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var thisWeek = active.Count(b => b.Start.Date >= weekStart);
        var activeBookers = active.Select(b => b.BookerId).Distinct().Count();
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
            TotalBookings = bookingList.Count,
            TodaysBookings = active.Count(b => b.Start.Date == today),
            ThisWeekBookings = thisWeek,
            ThisMonthBookings = active.Count(b => b.Start.Date >= monthStart),
            UpcomingBookings = active.Count(b => b.Start >= DateTime.Now),
            CancelledBookings = bookingList.Count(b => b.Status == BookingStatus.Cancelled),
            ConfirmedBookings = active.Count(b => b.Status == BookingStatus.Confirmed),
            PendingCount = bookingList.Count(b => b.Status == BookingStatus.Pending),
            AverageAttendees = active.Count == 0 ? 0 : Math.Round(active.Average(b => b.Attendees), 1),
            TotalAttendeesServed = active.Sum(b => b.Attendees),
            ActiveBookers = activeBookers,
            BookingsPerActiveUser = activeBookers == 0 ? 0 : Math.Round(active.Count / (double)activeBookers, 1),
            AvgBookingsPerDayThisWeek = Math.Round(thisWeek / 7.0, 1),
            AlertsGenerated = alertsGenerated,
            UnreadAlerts = unreadAlerts,
            BlockedPeriods = blockedPeriods,
            RoomsInScope = physicalRooms.Count,
            OccupancyPercent = physicalRooms.Count == 0
                ? 0
                : Math.Round(Math.Min(100, active.Count(b => b.Start.Date == today) * 100.0 / physicalRooms.Count), 1),
            MostUsedRoom = mostUsedRoom?.Key ?? "—",
            MostUsedLocation = mostUsedLocation?.Key ?? "—",
            BusiestDayLabel = weekday is null ? "—" : weekday.Key.ToString(),
            BookingsByLocation = WithPercent(active
                .GroupBy(b => string.IsNullOrWhiteSpace(b.LocationName) ? "Unknown" : b.LocationName)
                .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)),
            BookingsByStatus = WithPercent(bookingList
                .GroupBy(b => b.Status.ToString())
                .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })),
            UsersByRole = WithPercent(users
                .GroupBy(u => RolePermissions.DisplayName(u.Role))
                .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })),
            TopRooms = active
                .GroupBy(b => b.RoomName)
                .Select(g => new NamedCount
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Detail = g.First().LocationName
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList(),
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

    private static List<NamedCount> WithPercent(IEnumerable<NamedCount> items)
    {
        var list = items.ToList();
        var total = Math.Max(1, list.Sum(x => x.Count));
        foreach (var item in list)
            item.Percent = Math.Round(100.0 * item.Count / total, 0);
        return list;
    }
}
