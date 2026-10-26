using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class NotificationsViewModel(INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private string filter = "All";
    [ObservableProperty] private int unreadCount;
    [ObservableProperty] private int readCount;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private double clearProgress;
    [ObservableProperty] private string summary = "No alerts yet.";
    [ObservableProperty] private string emptyTitle = "Inbox zero";
    [ObservableProperty] private string emptySubtitle = "You're clear. New alerts will land here.";

    public bool HasUnread => UnreadCount > 0;

    public ObservableCollection<AppNotification> Items { get; } = [];
    public ObservableCollection<AppNotification> VisibleItems { get; } = [];

    private bool _watching;
    private bool _syncing;

    /// <summary>
    /// While the inbox is on screen, a change in the unread count reloads the list
    /// so an alert created for this person by someone else appears in place.
    /// </summary>
    public void WatchInbox(bool watch)
    {
        if (watch == _watching)
            return;

        if (watch)
            notifications.UnreadCountChanged += OnInboxChanged;
        else
            notifications.UnreadCountChanged -= OnInboxChanged;

        _watching = watch;
    }

    private void OnInboxChanged(int count)
    {
        if (!_watching || _syncing || count == UnreadCount)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_watching || _syncing || count == UnreadCount)
                return;
            _ = LoadAsync(showBusy: false);
        });
    }

    [RelayCommand]
    private Task AppearingAsync() => LoadAsync(showBusy: true);

    private async Task LoadAsync(bool showBusy)
    {
        if (_syncing)
            return;

        _syncing = true;
        if (showBusy)
            IsBusy = true;
        try
        {
            var latest = await notifications.GetNotificationsAsync();
            Items.Clear();
            foreach (var n in latest)
                Items.Add(n);
            ApplyFilter();
        }
        finally
        {
            if (showBusy)
                IsBusy = false;
            _syncing = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Filter = value;
        ApplyFilter();
    }

    partial void OnUnreadCountChanged(int value) => OnPropertyChanged(nameof(HasUnread));

    private void ApplyFilter()
    {
        VisibleItems.Clear();
        foreach (var n in Items.Where(MatchesFilter))
            VisibleItems.Add(n);

        TotalCount = Items.Count;
        UnreadCount = Items.Count(n => !n.IsRead);
        ReadCount = TotalCount - UnreadCount;
        ClearProgress = TotalCount == 0 ? 1 : ReadCount / (double)TotalCount;
        IsEmpty = VisibleItems.Count == 0;
        OnPropertyChanged(nameof(HasUnread));
        Summary = DescribeInbox();
        EmptyTitle = Filter == "All" ? "No alerts" : "Nothing in this filter";
        EmptySubtitle = Filter == "All"
            ? "You're clear. New alerts will land here."
            : "Try All, or swipe a card to change its read state.";
    }

    private bool MatchesFilter(AppNotification n) => Filter switch
    {
        "Unread" => !n.IsRead,
        "Reminder" => n.Type == "Reminder",
        "Booking" => n.Type is "Confirmation" or "Cancellation" or "Update",
        _ => true
    };

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await notifications.MarkAllAsReadAsync();
        foreach (var item in Items)
            item.IsRead = true;
        ApplyFilter();
        await ActionFeedback.SuccessAsync(
            UnreadCount == 0 ? "Every alert is marked read." : "Marked what we could as read.",
            "Alerts");
    }

    /// <summary>
    /// Tapping the card marks it read (and greys it out in place) then opens the booking
    /// if there is one. Swipe still toggles read/unread or deletes.
    /// </summary>
    [RelayCommand]
    private async Task OpenNotificationAsync(AppNotification? notification)
    {
        if (notification is null) return;

        var wasUnread = !notification.IsRead;
        await MarkReadInPlaceAsync(notification);

        // Let the card grey out before we leave the inbox.
        if (wasUnread)
            await Task.Delay(180);

        if (Shell.Current is null) return;

        if (notification.BookingId.HasValue)
            await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={notification.BookingId}");
        else
            await ActionFeedback.MessageAsync(notification.Message, notification.Title);
    }

    private async Task MarkReadInPlaceAsync(AppNotification notification)
    {
        if (notification.IsRead) return;

        await notifications.MarkAsReadAsync(notification.Id);
        notification.IsRead = true;
        RefreshStats();

        if (Filter == "Unread")
            VisibleItems.Remove(notification);
    }

    private void RefreshStats()
    {
        TotalCount = Items.Count;
        UnreadCount = Items.Count(n => !n.IsRead);
        ReadCount = TotalCount - UnreadCount;
        ClearProgress = TotalCount == 0 ? 1 : ReadCount / (double)TotalCount;
        IsEmpty = VisibleItems.Count == 0;
        OnPropertyChanged(nameof(HasUnread));
        Summary = DescribeInbox();
    }

    private string DescribeInbox() => TotalCount == 0
        ? "No alerts yet."
        : UnreadCount == 0
            ? $"{TotalCount} alerts, all read."
            : $"{UnreadCount} unread of {TotalCount}.";

    [RelayCommand]
    private async Task ToggleReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        if (notification.IsRead)
        {
            await notifications.MarkAsUnreadAsync(notification.Id);
            notification.IsRead = false;
        }
        else
        {
            await notifications.MarkAsReadAsync(notification.Id);
            notification.IsRead = true;
        }

        if (Filter == "Unread" && notification.IsRead)
            VisibleItems.Remove(notification);
        else if (Filter == "Unread" && !notification.IsRead && !VisibleItems.Contains(notification))
            VisibleItems.Insert(0, notification);

        RefreshStats();
    }

    [RelayCommand]
    private async Task DeleteAsync(AppNotification? notification)
    {
        if (notification is null) return;
        await notifications.DeleteAsync(notification.Id);
        await AppearingAsync();
        await ActionFeedback.InfoAsync("Alert removed from your inbox.", "Cleared");
    }
}

public partial class ProfileViewModel(IAuthService auth, IBookingService bookings, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string rankTitle = string.Empty;
    [ObservableProperty] private string rankLine = string.Empty;
    [ObservableProperty] private int accessLevel;
    [ObservableProperty] private string roleDescription = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool canViewAvailability;
    [ObservableProperty] private bool canViewReports;
    [ObservableProperty] private bool showPayPlaceholder;
    [ObservableProperty] private int todayCount;
    [ObservableProperty] private int upcomingCount;
    [ObservableProperty] private int unreadAlerts;

    private bool _watchingUnread;

    public void WatchUnread(bool watch)
    {
        if (watch == _watchingUnread)
            return;

        if (watch)
            notifications.UnreadCountChanged += OnLiveUnread;
        else
            notifications.UnreadCountChanged -= OnLiveUnread;

        _watchingUnread = watch;
    }

    private void OnLiveUnread(int count) =>
        MainThread.BeginInvokeOnMainThread(() => UnreadAlerts = count);

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        Name = user?.Name ?? "Guest";
        FirstName = string.IsNullOrWhiteSpace(Name) ? "there" : Name.Split(' ')[0];
        Email = user?.Email ?? string.Empty;
        Role = user is null ? "—" : RolePermissions.DisplayName(user.Role);
        RankTitle = user is null ? "Guest" : RolePermissions.RankTitle(user.Role);
        AccessLevel = user is null ? 0 : RolePermissions.AccessLevel(user.Role);
        RankLine = $"Level {AccessLevel} · {RankTitle}";
        RoleDescription = user is null ? string.Empty : RolePermissions.Describe(user.Role);
        Location = user?.LocationId ?? "All locations";
        CanAccessManage = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        CanViewAvailability = user is not null && RolePermissions.CanViewAvailability(user.Role);
        CanViewReports = user is not null && RolePermissions.CanViewReports(user.Role);
        ShowPayPlaceholder = user is not null && RolePermissions.CanSeePayPlaceholder(user.Role);

        try
        {
            var scope = await bookings.GetBookingsForCurrentUserScopeAsync();
            TodayCount = scope.Count(b => b.Start.Date == DateTime.Today && b.Status != BookingStatus.Cancelled);
            UpcomingCount = scope.Count(b => b.Start >= DateTime.Now && b.Status != BookingStatus.Cancelled);
            UnreadAlerts = await notifications.GetUnreadCountAsync();
        }
        catch
        {
            TodayCount = UpcomingCount = UnreadAlerts = 0;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await auth.LogoutAsync();
        await ActionFeedback.InfoAsync("Signed out. See you next round.", "Session ended");
        await Shell.Current.GoToAsync("//WelcomePage");
    }

    [RelayCommand]
    private async Task OpenManageAsync()
    {
        if (!CanAccessManage) return;
        await Shell.Current.GoToAsync("//ManagePage");
    }

    [RelayCommand]
    private async Task OpenBookingsAsync() =>
        await Shell.Current.GoToAsync("//MyBookingsPage");

    [RelayCommand]
    private async Task OpenLocationsAsync() =>
        await Shell.Current.GoToAsync("LocationsPage");

    [RelayCommand]
    private async Task OpenLiveAsync()
    {
        if (!CanViewAvailability) return;
        await Shell.Current.GoToAsync("//AvailabilityPage");
    }

    [RelayCommand]
    private async Task OpenAlertsAsync() =>
        await Shell.Current.GoToAsync("//NotificationsPage");

    [RelayCommand]
    private async Task OpenReportsAsync()
    {
        if (!CanViewReports) return;
        await Shell.Current.GoToAsync("//ReportsPage");
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");

    [RelayCommand]
    private async Task OpenSettingsAsync() =>
        await Shell.Current.GoToAsync("SettingsPage");
}

[QueryProperty(nameof(LocationId), "locationId")]
public partial class LocationDetailViewModel(IAuthService auth, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private string locationId = string.Empty;
    [ObservableProperty] private OfficeLocation? location;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool canBook;

    public ObservableCollection<Boardroom> Rooms { get; } = [];

    partial void OnLocationIdChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        CanBook = auth.CurrentUser is not null && RolePermissions.CanBookRooms(auth.CurrentUser.Role);
        if (!RolePermissions.CanAccessLocation(auth.CurrentUser, LocationId))
        {
            Location = null;
            Rooms.Clear();
            IsEmpty = true;
            return;
        }

        Location = await rooms.GetLocationAsync(LocationId);
        Rooms.Clear();
        foreach (var room in await rooms.GetRoomsAsync(LocationId))
            Rooms.Add(room);
        IsEmpty = Rooms.Count == 0;
    }

    [RelayCommand]
    private async Task BookRoomAsync(Boardroom? room)
    {
        if (room is null || !CanBook) return;
        await Shell.Current.GoToAsync($"//BookingPage?roomId={room.Id}&locationId={room.LocationId}");
    }
}

public partial class LocationsViewModel(IRoomService rooms) : ObservableObject
{
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        Locations.Clear();
        foreach (var loc in await rooms.GetLocationsAsync())
            Locations.Add(loc);
    }

    [RelayCommand]
    private async Task OpenAsync(OfficeLocation? location)
    {
        if (location is null) return;
        await Shell.Current.GoToAsync($"LocationDetailPage?locationId={location.Id}");
    }
}
