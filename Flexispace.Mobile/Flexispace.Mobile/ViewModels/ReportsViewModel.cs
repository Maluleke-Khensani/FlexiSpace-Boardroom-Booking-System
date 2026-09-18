using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class ReportsViewModel(IAuthService auth, IAdminService admin) : ObservableObject
{
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string title = "Operations report";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string generatedLabel = string.Empty;
    [ObservableProperty] private string scopeLabel = string.Empty;

    [ObservableProperty] private int totalUsers;
    [ObservableProperty] private int employeeUsers;
    [ObservableProperty] private int clientUsers;
    [ObservableProperty] private int totalBookings;
    [ObservableProperty] private int todaysBookings;
    [ObservableProperty] private int thisWeekBookings;
    [ObservableProperty] private int thisMonthBookings;
    [ObservableProperty] private int upcomingBookings;
    [ObservableProperty] private int confirmedBookings;
    [ObservableProperty] private int cancelledBookings;
    [ObservableProperty] private double averageAttendees;
    [ObservableProperty] private int totalAttendeesServed;
    [ObservableProperty] private int activeBookers;
    [ObservableProperty] private double bookingsPerActiveUser;
    [ObservableProperty] private double avgBookingsPerDayThisWeek;
    [ObservableProperty] private int alertsGenerated;
    [ObservableProperty] private int unreadAlerts;
    [ObservableProperty] private int blockedPeriods;
    [ObservableProperty] private int roomsInScope;
    [ObservableProperty] private double occupancyPercent;
    [ObservableProperty] private string mostUsedRoom = "—";
    [ObservableProperty] private string mostUsedLocation = "—";
    [ObservableProperty] private string busiestDayLabel = "—";

    public ObservableCollection<NamedCount> BookingsByLocation { get; } = [];
    public ObservableCollection<NamedCount> BookingsByStatus { get; } = [];
    public ObservableCollection<NamedCount> UsersByRole { get; } = [];
    public ObservableCollection<NamedCount> TopRooms { get; } = [];
    public ObservableCollection<NamedCount> WeekdayActivity { get; } = [];
    public ObservableCollection<ReportActivityItem> RecentActivity { get; } = [];

    public bool HasLocationBreakdown => BookingsByLocation.Count > 0;
    public bool HasStatusBreakdown => BookingsByStatus.Count > 0;
    public bool HasRoleBreakdown => UsersByRole.Count > 0;
    public bool HasTopRooms => TopRooms.Count > 0;
    public bool HasWeekdayActivity => WeekdayActivity.Count > 0;
    public bool HasRecentActivity => RecentActivity.Count > 0;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanViewReports(user.Role);
        if (!HasAccess)
        {
            Title = "Reports";
            Subtitle = "Operations reports are available to Flexispace employees only.";
            return;
        }

        IsBusy = true;
        try
        {
            var report = await admin.GetReportSummaryAsync();
            Apply(report);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(ReportSummary report)
    {
        Title = "Operations report";
        ScopeLabel = report.ScopeLabel;
        Subtitle = $"{RolePermissions.DisplayName(auth.CurrentUser!.Role)} view · {report.ScopeLabel}";
        GeneratedLabel = $"Generated {report.GeneratedAt:ddd d MMM yyyy · HH:mm}";

        TotalUsers = report.TotalUsers;
        EmployeeUsers = report.EmployeeUsers;
        ClientUsers = report.ClientUsers;
        TotalBookings = report.TotalBookings;
        TodaysBookings = report.TodaysBookings;
        ThisWeekBookings = report.ThisWeekBookings;
        ThisMonthBookings = report.ThisMonthBookings;
        UpcomingBookings = report.UpcomingBookings;
        ConfirmedBookings = report.ConfirmedBookings;
        CancelledBookings = report.CancelledBookings;
        AverageAttendees = report.AverageAttendees;
        TotalAttendeesServed = report.TotalAttendeesServed;
        ActiveBookers = report.ActiveBookers;
        BookingsPerActiveUser = report.BookingsPerActiveUser;
        AvgBookingsPerDayThisWeek = report.AvgBookingsPerDayThisWeek;
        AlertsGenerated = report.AlertsGenerated;
        UnreadAlerts = report.UnreadAlerts;
        BlockedPeriods = report.BlockedPeriods;
        RoomsInScope = report.RoomsInScope;
        OccupancyPercent = report.OccupancyPercent;
        MostUsedRoom = report.MostUsedRoom;
        MostUsedLocation = report.MostUsedLocation;
        BusiestDayLabel = report.BusiestDayLabel;

        Replace(BookingsByLocation, report.BookingsByLocation);
        Replace(BookingsByStatus, report.BookingsByStatus);
        Replace(UsersByRole, report.UsersByRole);
        Replace(TopRooms, report.TopRooms);
        Replace(WeekdayActivity, report.WeekdayActivity);
        Replace(RecentActivity, report.RecentActivity);

        OnPropertyChanged(nameof(HasLocationBreakdown));
        OnPropertyChanged(nameof(HasStatusBreakdown));
        OnPropertyChanged(nameof(HasRoleBreakdown));
        OnPropertyChanged(nameof(HasTopRooms));
        OnPropertyChanged(nameof(HasWeekdayActivity));
        OnPropertyChanged(nameof(HasRecentActivity));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }
}
