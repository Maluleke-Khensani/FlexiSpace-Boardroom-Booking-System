using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface IAdminService
{
    Task<IReadOnlyList<User>> GetUsersAsync();
    Task<IReadOnlyList<User>> GetEntraDirectoryAsync();
    Task<(bool Ok, string Message)> ProvisionUserAsync(User user);
    Task<(bool Ok, string Message)> UpdateUserAsync(User user);
    Task<(bool Ok, string Message)> RemoveUserAsync(int apiUserId);
    Task<(bool Ok, string Message)> SetUserActiveAsync(int apiUserId, bool isActive);
    Task<bool> AddRoomAsync(Boardroom room);
    Task<bool> RemoveRoomAsync(string roomId);
    Task<ReportSummary> GetReportSummaryAsync();
}

public class ReportSummary
{
    public bool HasAccess { get; set; }
    public string ScopeLabel { get; set; } = "All locations";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    // People
    public int TotalUsers { get; set; }
    public int EmployeeUsers { get; set; }
    public int ClientUsers { get; set; }
    public int StaffCount { get; set; }
    public int CentreManagerCount { get; set; }
    public int AdministratorCount { get; set; }

    // Bookings
    public int TotalBookings { get; set; }
    public int TodaysBookings { get; set; }
    public int ThisWeekBookings { get; set; }
    public int ThisMonthBookings { get; set; }
    public int UpcomingBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public double AverageAttendees { get; set; }
    public int TotalAttendeesServed { get; set; }

    // Usage / activity
    public int ActiveBookers { get; set; }
    public double BookingsPerActiveUser { get; set; }
    public double AvgBookingsPerDayThisWeek { get; set; }
    public int AlertsGenerated { get; set; }
    public int UnreadAlerts { get; set; }
    public int BlockedPeriods { get; set; }
    public int RoomsInScope { get; set; }
    public double OccupancyPercent { get; set; }
    public string MostUsedRoom { get; set; } = "—";
    public string MostUsedLocation { get; set; } = "—";
    public string BusiestDayLabel { get; set; } = "—";

    public List<NamedCount> BookingsByLocation { get; set; } = [];
    public List<NamedCount> BookingsByStatus { get; set; } = [];
    public List<NamedCount> UsersByRole { get; set; } = [];
    public List<NamedCount> TopRooms { get; set; } = [];
    public List<NamedCount> WeekdayActivity { get; set; } = [];
    public List<ReportActivityItem> RecentActivity { get; set; } = [];
}

public class NamedCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Detail { get; set; } = string.Empty;
    public double Percent { get; set; }
}

public class ReportActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string WhenLabel { get; set; } = string.Empty;
    public string Kind { get; set; } = "Info";
}
