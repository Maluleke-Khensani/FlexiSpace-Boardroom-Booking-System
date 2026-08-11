using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Manage console for Centre Managers and Administrators: pending approvals and in-scope bookings.
public partial class ManageViewModel(IAuthService auth, IBookingService bookings, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string title = "Manage";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private bool isCentreManager;
    [ObservableProperty] private bool isAdministrator;
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? message;

    public ObservableCollection<Booking> PendingBookings { get; } = [];
    public ObservableCollection<Booking> LocationBookings { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        if (!HasAccess)
        {
            Title = "Manage";
            Subtitle = "Only Centre Managers and Administrators can open this console.";
            IsCentreManager = IsAdministrator = false;
            return;
        }

        IsCentreManager = user!.Role == UserRole.CentreManager;
        IsAdministrator = user.Role == UserRole.Administrator;
        Title = IsAdministrator ? "Admin console" : "Centre management";
        Subtitle = IsAdministrator
            ? "Approve or decline bookings across every location."
            : $"Approve or decline bookings for {user.LocationId ?? "your centre"}.";

        IsBusy = true;
        Message = null;
        try
        {
            LocationBookings.Clear();
            PendingBookings.Clear();
            foreach (var b in await bookings.GetBookingsForCurrentUserScopeAsync())
            {
                LocationBookings.Add(b);
                if (b.Status == BookingStatus.Pending)
                    PendingBookings.Add(b);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApproveAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.ApproveBookingAsync(booking.Id);
        Message = ok ? "Booking approved." : "Could not approve (check permissions / status).";
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeclineAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.DeclineBookingAsync(booking.Id);
        Message = ok ? "Booking declined." : "Could not decline booking.";
        await AppearingAsync();
    }

    [RelayCommand]
    private void OpenBooking(Booking? booking)
    {
        if (booking is null) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }
}

//Alerts inbox: lists notifications and supports read/unread, delete, and booking deep-links.
public partial class NotificationsViewModel(INotificationService notifications, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private string? alertMessage;

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

        if (notification.BookingId.HasValue)
            nav.NavigateTo($"/bookings/{notification.BookingId}");
        else
            AlertMessage = $"{notification.Title}\n\n{notification.Message}";
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

//User profile: current account details, role description, demo-user switcher, and sign-out.
public partial class ProfileViewModel(IAuthService auth, INavigationService nav) : ObservableObject
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
        await nav.ReloadAppAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await auth.LogoutAsync();
        nav.NavigateTo("/", forceLoad: true);
    }

    [RelayCommand]
    private void OpenManage()
    {
        if (!CanAccessManage) return;
        nav.NavigateTo("/manage");
    }
}

//Single-location detail: loads a location and its boardrooms; links into the booking wizard.
public partial class LocationDetailViewModel(IAuthService auth, IRoomService rooms, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string locationId = string.Empty;
    [ObservableProperty] private OfficeLocation? location;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool canBook;

    public ObservableCollection<Boardroom> Rooms { get; } = [];

    public void SetLocationId(string id)
    {
        LocationId = id;
        _ = LoadAsync();
    }

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
    private void BookRoom(Boardroom? room)
    {
        if (room is null || !CanBook) return;
        nav.NavigateTo($"/book?roomId={room.Id}&locationId={room.LocationId}");
    }
}

//Location gallery: lists all Flexispace sites and navigates to each location detail page.
public partial class LocationsViewModel(IRoomService rooms, INavigationService nav) : ObservableObject
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
    private void Open(OfficeLocation? location)
    {
        if (location is null) return;
        nav.NavigateTo($"/locations/{location.Id}");
    }
}
