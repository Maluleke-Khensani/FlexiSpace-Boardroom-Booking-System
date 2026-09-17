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
    [ObservableProperty] private string questLine = "You're all caught up.";
    [ObservableProperty] private string emptyTitle = "Inbox zero";
    [ObservableProperty] private string emptySubtitle = "You're clear. New alerts will land here.";

    public bool HasUnread => UnreadCount > 0;

    public ObservableCollection<AppNotification> Items { get; } = [];
    public ObservableCollection<AppNotification> VisibleItems { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var n in await notifications.GetNotificationsAsync())
                Items.Add(n);
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Filter = value;
        ApplyFilter();
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        QuestLine = value > 0
            ? $"Clear {value} to hit inbox zero."
            : "Inbox zero — you're in the clear.";
    }

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
        QuestLine = UnreadCount > 0
            ? $"Clear {UnreadCount} to hit inbox zero."
            : "Inbox zero — you're in the clear.";
        EmptyTitle = Filter == "All" ? "Inbox zero" : "Nothing in this lane";
        EmptySubtitle = Filter == "All"
            ? "You're clear. New alerts will land here."
            : "Try All, or swipe a card to change its read state.";
    }

    private bool MatchesFilter(AppNotification n) => Filter switch
    {
        "Unread" => !n.IsRead,
        "Reminder" => n.Type == "Reminder",
        "Booking" => n.Type is "Confirmation" or "Cancellation",
        _ => true
    };

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await notifications.MarkAllAsReadAsync();
        ApplyFilter();
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
            await Shell.Current.DisplayAlertAsync(notification.Title, notification.Message, "OK");
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
        QuestLine = UnreadCount > 0
            ? $"Clear {UnreadCount} to hit inbox zero."
            : "Inbox zero — you're in the clear.";
    }

    [RelayCommand]
    private async Task ToggleReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        if (notification.IsRead)
            await notifications.MarkAsUnreadAsync(notification.Id);
        else
            await notifications.MarkAsReadAsync(notification.Id);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task DeleteAsync(AppNotification? notification)
    {
        if (notification is null) return;
        await notifications.DeleteAsync(notification.Id);
        await AppearingAsync();
    }
}

public partial class ProfileViewModel(IAuthService auth, IBookingService bookings, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string currentEmail = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string rankTitle = string.Empty;
    [ObservableProperty] private string rankLine = string.Empty;
    [ObservableProperty] private int accessLevel;
    [ObservableProperty] private string roleDescription = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool canViewAvailability;
    [ObservableProperty] private bool showPayPlaceholder;
    [ObservableProperty] private int todayCount;
    [ObservableProperty] private int upcomingCount;
    [ObservableProperty] private int unreadAlerts;

    public ObservableCollection<User> DemoUsers { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        Name = user?.Name ?? "Guest";
        FirstName = string.IsNullOrWhiteSpace(Name) ? "there" : Name.Split(' ')[0];
        Email = user?.Email ?? string.Empty;
        CurrentEmail = Email;
        Role = user is null ? "—" : RolePermissions.DisplayName(user.Role);
        RankTitle = user is null ? "Guest" : RolePermissions.RankTitle(user.Role);
        AccessLevel = user is null ? 0 : RolePermissions.AccessLevel(user.Role);
        RankLine = $"Level {AccessLevel} · {RankTitle}";
        RoleDescription = user is null ? string.Empty : RolePermissions.Describe(user.Role);
        Location = user?.LocationId ?? "All locations";
        CanAccessManage = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        CanViewAvailability = user is not null && RolePermissions.CanViewAvailability(user.Role);
        ShowPayPlaceholder = user is not null && RolePermissions.CanSeePayPlaceholder(user.Role);

        DemoUsers.Clear();
        foreach (var u in auth.GetDemoUsers())
            DemoUsers.Add(u);

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
    private async Task SwitchUserAsync(User? user)
    {
        if (user is null) return;
        await auth.SwitchDemoUserAsync(user.Email);
        // Rebuild the Shell so the tab bar matches the new role's permissions exactly.
        await ShellReloader.ReloadAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await auth.LogoutAsync();
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
        await Shell.Current.GoToAsync("MyBookingsPage");

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
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");
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
