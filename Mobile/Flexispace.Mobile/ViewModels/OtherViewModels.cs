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

    public ObservableCollection<AppNotification> Items { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var n in await notifications.GetNotificationsAsync())
                Items.Add(n);
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await notifications.MarkAllAsReadAsync();
        await AppearingAsync();
    }

    /// <summary>
    /// Tapping the card opens whatever it's about (its booking, if any) and marks it read —
    /// swiping the card reveals the per-item Read/Unread and Delete actions instead.
    /// </summary>
    [RelayCommand]
    private async Task OpenNotificationAsync(AppNotification? notification)
    {
        if (notification is null) return;

        if (!notification.IsRead)
        {
            await notifications.MarkAsReadAsync(notification.Id);
            notification.IsRead = true;
            OnPropertyChanged(nameof(Items));
        }

        if (Shell.Current is null) return;

        if (notification.BookingId.HasValue)
            await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={notification.BookingId}");
        else
            await Shell.Current.DisplayAlertAsync(notification.Title, notification.Message, "OK");
    }

    [RelayCommand]
    private async Task ToggleReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        if (notification.IsRead)
            await notifications.MarkAsUnreadAsync(notification.Id);
        else
            await notifications.MarkAsReadAsync(notification.Id);
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(AppNotification? notification)
    {
        if (notification is null) return;
        await notifications.DeleteAsync(notification.Id);
        await AppearingAsync();
    }
}

public partial class ProfileViewModel(IAuthService auth) : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string roleDescription = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool showPayPlaceholder;

    public ObservableCollection<User> DemoUsers { get; } = [];

    [RelayCommand]
    private void Appearing()
    {
        var user = auth.CurrentUser;
        Name = user?.Name ?? "Guest";
        Email = user?.Email ?? string.Empty;
        Role = user is null ? "—" : RolePermissions.DisplayName(user.Role);
        RoleDescription = user is null ? string.Empty : RolePermissions.Describe(user.Role);
        Location = user?.LocationId ?? "All locations";
        CanAccessManage = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        ShowPayPlaceholder = user is not null && RolePermissions.CanSeePayPlaceholder(user.Role);

        DemoUsers.Clear();
        foreach (var u in auth.GetDemoUsers())
            DemoUsers.Add(u);
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
