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

//Sign-in form state and login error handling.
public partial class LoginViewModel(IAuthService auth, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
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
                ErrorMessage = "Use Sign in with Microsoft. Email and password are no longer used.";
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
    private Task MicrosoftSignInAsync() => auth.MicrosoftSignInAsync();

}

//Dashboard state: role-aware greeting, today's bookings, location carousel, and quick actions.
public partial class HomeViewModel(IAuthService auth, IBookingService bookings, IRoomService rooms, INotificationService notifications, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string greeting = "Welcome";
    [ObservableProperty] private string subtitle = "Book and manage your meeting rooms.";
    [ObservableProperty] private int unreadCount;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canBook;
    [ObservableProperty] private bool canViewAvailability;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool canViewReports;

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
                CanViewReports = RolePermissions.CanViewReports(user.Role);
                var locationName = user.LocationId is null
                    ? null
                    : (await rooms.GetLocationAsync(user.LocationId))?.Name;
                Subtitle = user.Role switch
                {
                    UserRole.Administrator => "All Flexispace locations",
                    UserRole.CentreManager => $"Managing {locationName ?? "your centre"}",
                    UserRole.Client => "Book a room at any Flexispace location",
                    _ => "Book and manage your meeting rooms"
                };
            }
            else
            {
                CanBook = CanViewAvailability = CanAccessManage = CanViewReports = false;
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
        catch
        {
            Subtitle = "Could not refresh the dashboard.";
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
    private void OpenReports()
    {
        if (!CanViewReports) return;
        nav.NavigateTo("/reports");
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
