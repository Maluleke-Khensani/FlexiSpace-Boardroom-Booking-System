using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class ReportsViewModel(IAuthService auth, IAdminService admin) : ObservableObject
{
    private static readonly Color[] DayAccents =
    [
        Color.FromArgb("#C4A035"),
        Color.FromArgb("#1F6F78"),
        Color.FromArgb("#1E8E5A"),
        Color.FromArgb("#3D5A73"),
        Color.FromArgb("#B5622E"),
        Color.FromArgb("#9A7A1F"),
        Color.FromArgb("#C1392B")
    ];

    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isGamified = true;
    private bool _restoringMode;
    [ObservableProperty] private string title = "Ops Arena";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string generatedLabel = string.Empty;
    [ObservableProperty] private string scopeLabel = string.Empty;

    [ObservableProperty] private string playerName = string.Empty;
    [ObservableProperty] private string rankTitle = "Guest";
    [ObservableProperty] private int accessLevel;
    [ObservableProperty] private string rankLine = string.Empty;
    [ObservableProperty] private int opsXp;
    [ObservableProperty] private int opsLevel = 1;
    [ObservableProperty] private double levelProgress;
    [ObservableProperty] private string xpLabel = string.Empty;
    [ObservableProperty] private string missionBrief = string.Empty;

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
    [ObservableProperty] private double occupancyProgress;
    [ObservableProperty] private string occupancyCaption = "0 booked today · 0 in use now · 0 rooms";
    [ObservableProperty] private double clearAlertsProgress;
    [ObservableProperty] private string mostUsedRoom = "—";
    [ObservableProperty] private string mostUsedLocation = "—";
    [ObservableProperty] private string busiestDayLabel = "—";
    [ObservableProperty] private int unlockedBadgeCount;
    [ObservableProperty] private string badgeProgressLabel = string.Empty;
    [ObservableProperty] private string briefingLine = string.Empty;
    [ObservableProperty] private string updatedLabel = string.Empty;
    [ObservableProperty] private double confirmedShare;
    [ObservableProperty] private string heldLabel = string.Empty;
    [ObservableProperty] private bool hasUserDirectory;
    [ObservableProperty] private int scoreboardPeople;
    [ObservableProperty] private string scoreboardPeopleLabel = "Players";

    public bool ShowArena => HasAccess && IsGamified;
    public bool ShowBriefing => HasAccess && !IsGamified;

    partial void OnHasAccessChanged(bool value) => NotifyMode();

    partial void OnIsGamifiedChanged(bool value)
    {
        Title = value ? "Ops Arena" : "Reports";
        NotifyMode();
        if (_restoringMode || !HasAccess || auth.CurrentUser is not { } user)
            return;
        AppPreferences.SetReportsGamified(user.Role, value);
    }

    public void ApplySavedMode()
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanViewReports(user.Role))
            return;

        _restoringMode = true;
        IsGamified = AppPreferences.GetReportsGamified(user.Role, user.Role != UserRole.CentreManager);
        _restoringMode = false;
        Title = IsGamified ? "Ops Arena" : "Reports";
        NotifyMode();
    }

    [RelayCommand]
    private void ShowArenaMode() => IsGamified = true;

    [RelayCommand]
    private void ShowBriefingMode() => IsGamified = false;

    private void NotifyMode()
    {
        OnPropertyChanged(nameof(ShowArena));
        OnPropertyChanged(nameof(ShowBriefing));
    }

    public ObservableCollection<NamedCount> BookingsByLocation { get; } = [];
    public ObservableCollection<NamedCount> BookingsByStatus { get; } = [];
    public ObservableCollection<NamedCount> UsersByRole { get; } = [];
    public ObservableCollection<LeaderboardRoomItem> TopRoomsBoard { get; } = [];
    public ObservableCollection<WeekdayBarItem> WeekdayBars { get; } = [];
    public ObservableCollection<ReportActivityItem> RecentActivity { get; } = [];
    public ObservableCollection<AchievementBadge> Achievements { get; } = [];
    public ObservableCollection<MeterRowItem> StatusMeters { get; } = [];
    public ObservableCollection<MeterRowItem> LocationMeters { get; } = [];

    public bool HasLocationBreakdown => LocationMeters.Count > 0;
    public bool HasStatusBreakdown => StatusMeters.Count > 0;
    public bool HasRoleBreakdown => UsersByRole.Count > 0;
    public bool HasTopRooms => TopRoomsBoard.Count > 0;
    public bool HasWeekdayActivity => WeekdayBars.Count > 0;
    public bool HasRecentActivity => RecentActivity.Count > 0;
    public bool HasAchievements => Achievements.Count > 0;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanViewReports(user.Role);
        if (HasAccess)
            ApplySavedMode();
        if (!HasAccess)
        {
            Title = "Ops Arena";
            Subtitle = "This dungeon is locked. Flexispace employees only.";
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
        var user = auth.CurrentUser!;
        PlayerName = user.Name.Split(' ')[0];
        RankTitle = RolePermissions.RankTitle(user.Role);
        AccessLevel = RolePermissions.AccessLevel(user.Role);
        RankLine = $"LVL {AccessLevel} · {RankTitle}";

        Title = IsGamified ? "Ops Arena" : "Reports";
        ScopeLabel = report.ScopeLabel;
        Subtitle = $"{RankTitle} campaign · {report.ScopeLabel}";
        GeneratedLabel = $"Scan {report.GeneratedAt:ddd d MMM · HH:mm}";
        MissionBrief = report.TodaysBookings > 0
            ? $"Today's raid: {report.TodaysBookings} live meetings. Keep the centres humming."
            : "Quiet lobby today — perfect window to stack the next booking streak.";

        HasUserDirectory = report.HasUserDirectory;
        TotalUsers = report.TotalUsers;
        ScoreboardPeople = report.HasUserDirectory ? report.TotalUsers : report.ActiveBookers;
        ScoreboardPeopleLabel = report.HasUserDirectory ? "Players" : "Bookers";
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
        OccupancyProgress = Math.Clamp(report.OccupancyPercent / 100.0, 0, 1);
        MostUsedRoom = report.MostUsedRoom;
        MostUsedLocation = report.MostUsedLocation;
        BusiestDayLabel = report.BusiestDayLabel;
        UpdatedLabel = $"Updated {report.GeneratedAt:ddd d MMM · HH:mm}";
        BriefingLine = TodaysBookings switch
        {
            0 => "No meetings booked today.",
            1 => "1 meeting booked today.",
            _ => $"{TodaysBookings} meetings booked today."
        };
        var decided = ConfirmedBookings + CancelledBookings;
        ConfirmedShare = decided == 0 ? 0 : ConfirmedBookings / (double)decided;
        HeldLabel = decided == 0
            ? "No held or cancelled bookings yet."
            : $"{ConfirmedBookings} held · {CancelledBookings} cancelled";

        ClearAlertsProgress = AlertsGenerated == 0
            ? 1
            : Math.Clamp(1 - (UnreadAlerts / (double)Math.Max(1, AlertsGenerated)), 0, 1);

        OpsXp = ConfirmedBookings * 12
                + ActiveBookers * 28
                + (int)OccupancyPercent
                + TotalAttendeesServed
                + ThisWeekBookings * 5;
        OpsLevel = Math.Max(1, OpsXp / 100 + 1);
        LevelProgress = (OpsXp % 100) / 100.0;
        XpLabel = $"{OpsXp % 100} / 100 XP → LVL {OpsLevel + 1}";

        Replace(BookingsByLocation, report.BookingsByLocation);
        Replace(BookingsByStatus, report.BookingsByStatus);
        Replace(UsersByRole, report.UsersByRole);
        Replace(RecentActivity, report.RecentActivity);

        RebuildMeters(LocationMeters, report.BookingsByLocation, DayAccents);
        RebuildMeters(StatusMeters, report.BookingsByStatus,
        [
            Color.FromArgb("#1E8E5A"),
            Color.FromArgb("#C1392B"),
            Color.FromArgb("#C4A035"),
            Color.FromArgb("#3D5A73"),
            Color.FromArgb("#1F6F78")
        ]);
        RebuildWeekdayBars(report.WeekdayActivity);
        RebuildTopRooms(report.TopRooms);
        RebuildAchievements(report);

        OnPropertyChanged(nameof(HasLocationBreakdown));
        OnPropertyChanged(nameof(HasStatusBreakdown));
        OnPropertyChanged(nameof(HasRoleBreakdown));
        OnPropertyChanged(nameof(HasTopRooms));
        OnPropertyChanged(nameof(HasWeekdayActivity));
        OnPropertyChanged(nameof(HasRecentActivity));
        OnPropertyChanged(nameof(HasAchievements));
    }

    private void RebuildWeekdayBars(IReadOnlyList<NamedCount> days)
    {
        WeekdayBars.Clear();
        var max = days.Count == 0 ? 1 : Math.Max(1, days.Max(d => d.Count));
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            WeekdayBars.Add(new WeekdayBarItem
            {
                Name = day.Name,
                Count = day.Count,
                BarHeight = 12 + (78.0 * day.Count / max),
                Accent = DayAccents[i % DayAccents.Length],
                IsPeak = day.Count == max && day.Count > 0
            });
        }
    }

    private void RebuildTopRooms(IReadOnlyList<NamedCount> rooms)
    {
        TopRoomsBoard.Clear();
        var max = rooms.Count == 0 ? 1 : Math.Max(1, rooms.Max(r => r.Count));
        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            TopRoomsBoard.Add(new LeaderboardRoomItem
            {
                Rank = i + 1,
                RankLabel = i switch { 0 => "1ST", 1 => "2ND", 2 => "3RD", _ => $"#{i + 1}" },
                RankColor = i switch
                {
                    0 => Color.FromArgb("#C4A035"),
                    1 => Color.FromArgb("#8E9AAF"),
                    2 => Color.FromArgb("#B5622E"),
                    _ => Color.FromArgb("#3D5A73")
                },
                RankGlyph = i == 0 ? "\ue838" : "\ue86c",
                Name = room.Name,
                Detail = room.Detail,
                Count = room.Count,
                FillPercent = Math.Clamp(room.Count / (double)max, 0.08, 1)
            });
        }
    }

    private void RebuildAchievements(ReportSummary report)
    {
        Achievements.Clear();
        Achievements.Add(new AchievementBadge(
            "\ue878", "Booking Streak", $"{report.ThisWeekBookings} this week",
            report.ThisWeekBookings >= 3, Color.FromArgb("#1F6F78"), Color.FromArgb("#E4F0F1")));
        Achievements.Add(new AchievementBadge(
            "\ue7fd", "Host Hero", $"{report.TotalAttendeesServed} guests",
            report.TotalAttendeesServed >= 15, Color.FromArgb("#1E8E5A"), Color.FromArgb("#E3F3EA")));
        Achievements.Add(new AchievementBadge(
            "\ue7f4", "Inbox Ace", report.UnreadAlerts == 0 ? "Inbox zero" : $"{report.UnreadAlerts} unread",
            report.UnreadAlerts == 0 && report.AlertsGenerated > 0, Color.FromArgb("#3D5A73"), Color.FromArgb("#E7ECF1")));
        Achievements.Add(new AchievementBadge(
            "\ue898", "Block Boss", $"{report.BlockedPeriods} blocks",
            report.BlockedPeriods > 0, Color.FromArgb("#B5622E"), Color.FromArgb("#F7E9DE")));
        Achievements.Add(new AchievementBadge(
            "\ue55f", "Centre Crown", report.MostUsedLocation,
            !string.IsNullOrWhiteSpace(report.MostUsedLocation) && report.MostUsedLocation != "—",
            Color.FromArgb("#9A7A1F"), Color.FromArgb("#F3EBD0")));
        Achievements.Add(new AchievementBadge(
            "\ue8b5", "Peak Day", report.BusiestDayLabel,
            !string.IsNullOrWhiteSpace(report.BusiestDayLabel) && report.BusiestDayLabel != "—",
            Color.FromArgb("#C1392B"), Color.FromArgb("#FBEAE7")));

        UnlockedBadgeCount = Achievements.Count(a => a.IsUnlocked);
        BadgeProgressLabel = $"{UnlockedBadgeCount} / {Achievements.Count} badges unlocked";
    }

    private static void RebuildMeters(ObservableCollection<MeterRowItem> target, IReadOnlyList<NamedCount> source, Color[] accents)
    {
        target.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            target.Add(new MeterRowItem
            {
                Name = item.Name,
                Detail = string.IsNullOrWhiteSpace(item.Detail) ? $"{item.Percent:0}% of mix" : item.Detail,
                Count = item.Count,
                Progress = Math.Clamp(item.Percent / 100.0, 0.04, 1),
                Accent = accents[i % accents.Length]
            });
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }
}

public sealed class WeekdayBarItem
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double BarHeight { get; init; }
    public Color Accent { get; init; } = Color.FromArgb("#C4A035");
    public bool IsPeak { get; init; }
}

public sealed class LeaderboardRoomItem
{
    public int Rank { get; init; }
    public string RankLabel { get; init; } = string.Empty;
    public Color RankColor { get; init; } = Color.FromArgb("#C4A035");
    public string RankGlyph { get; init; } = "\ue838";
    public string Name { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public int Count { get; init; }
    public double FillPercent { get; init; }
}

public sealed class MeterRowItem
{
    public string Name { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Progress { get; init; }
    public Color Accent { get; init; } = Color.FromArgb("#C4A035");
}

public sealed class AchievementBadge(
    string glyph,
    string title,
    string detail,
    bool unlocked,
    Color accent,
    Color wash)
{
    public string Glyph { get; } = glyph;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public bool IsUnlocked { get; } = unlocked;
    public Color Accent { get; } = accent;
    public Color Wash { get; } = wash;
    public double Opacity => IsUnlocked ? 1 : 0.4;
    public string StatusLabel => IsUnlocked ? "UNLOCKED" : "LOCKED";
}
