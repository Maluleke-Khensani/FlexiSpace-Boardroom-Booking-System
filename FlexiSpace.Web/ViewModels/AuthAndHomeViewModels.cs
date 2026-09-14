using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Landing page state. Sends unauthenticated visitors to the login flow.
public partial class WelcomeViewModel(INavigationService nav) : ObservableObject
{
    [RelayCommand]
    private void GetStarted() => nav.NavigateTo("/login");
}

//Sign-in form state: credentials, demo-account shortcuts, and login error handling.
public partial class LoginViewModel(IAuthService auth, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string email = "staff@flexispace.net.za";
    [ObservableProperty] private string password = "demo123";
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var ok = await auth.LoginAsync(Email, Password);
            if (!ok)
            {
                ErrorMessage = "Invalid email or password. Try a demo account below.";
                return;
            }

            await nav.ReloadAppAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UseDemoAsync(string email)
    {
        Email = email;
        Password = "demo123";
        await LoginAsync();
    }
}

//Dashboard state: role-aware greeting, today's bookings, location carousel, and quick actions.
public partial class HomeViewModel(IAuthService auth, IBookingService bookings, IRoomService rooms, INotificationService notifications, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string greeting = "Welcome";
    [ObservableProperty] private string subtitle = "You run your business — we run the rest.";
    [ObservableProperty] private string roleBanner = string.Empty;
    [ObservableProperty] private int unreadCount;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canBook;
    [ObservableProperty] private bool canViewAvailability;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool showPayPlaceholder;

    public ObservableCollection<Booking> TodaysBookings { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsBusy = true;
        try
        {
            var user = auth.CurrentUser;
            Greeting = user is null ? "Welcome" : $"Hello, {user.Name.Split(' ')[0]}";
            if (user is not null)
            {
                CanBook = RolePermissions.CanBookRooms(user.Role);
                CanViewAvailability = RolePermissions.CanViewAvailability(user.Role);
                CanAccessManage = RolePermissions.CanAccessManageHub(user.Role);
                ShowPayPlaceholder = RolePermissions.CanSeePayPlaceholder(user.Role);
                RoleBanner = $"{RolePermissions.DisplayName(user.Role)} · {RolePermissions.Describe(user.Role)}";
                Subtitle = user.Role switch
                {
                    UserRole.Administrator => "All locations · rooms, users & reports",
                    UserRole.CentreManager => $"Managing {user.LocationId ?? "your centre"}",
                    UserRole.Client => "Self-service booking across Flexispace",
                    _ => "You run your business — we run the rest."
                };
            }
            else
            {
                CanBook = CanViewAvailability = CanAccessManage = ShowPayPlaceholder = false;
                RoleBanner = string.Empty;
            }

            UnreadCount = await notifications.GetUnreadCountAsync();

            TodaysBookings.Clear();
            foreach (var b in await bookings.GetTodaysBookingsAsync())
            {
                if (b.Status != BookingStatus.Cancelled)
                    TodaysBookings.Add(b);
            }
            IsEmpty = TodaysBookings.Count == 0;

            Locations.Clear();
            foreach (var loc in await rooms.GetLocationsAsync())
                Locations.Add(loc);
        }
        catch (Exception ex)
        {
            RoleBanner = $"Could not refresh home: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BookRoom()
    {
        if (!CanBook) return;
        nav.NavigateTo("/book");
    }

    [RelayCommand]
    private void OpenManage()
    {
        if (!CanAccessManage) return;
        nav.NavigateTo("/manage");
    }

    [RelayCommand]
    private void OpenLocation(OfficeLocation? location)
    {
        if (location is null) return;
        nav.NavigateTo($"/locations/{location.Id}");
    }

    [RelayCommand]
    private void OpenBooking(Booking? booking)
    {
        if (booking is null) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }
}
