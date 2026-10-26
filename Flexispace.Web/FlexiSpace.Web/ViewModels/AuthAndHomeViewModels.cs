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
    [ObservableProperty] private int availableCount;
    [ObservableProperty] private int occupiedCount;
    [ObservableProperty] private int reservedCount;
    [ObservableProperty] private int blockedCount;
    [ObservableProperty] private int roomsInScope;
    [ObservableProperty] private double occupancyPercent;

    public string OccupancyCaption
    {
        get
        {
            var parts = new List<string> { $"{OccupiedCount} in use now" };
            if (ReservedCount > 0)
                parts.Add($"{ReservedCount} reserved");
            if (BlockedCount > 0)
                parts.Add($"{BlockedCount} blocked");
            parts.Add($"{RoomsInScope} rooms in scope");
            return string.Join(" · ", parts);
        }
    }

    public ObservableCollection<Booking> TodaysBookings { get; } = [];
    public ObservableCollection<Booking> UpcomingBookings { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsBusy = true;
        try
        {
            var user = auth.CurrentUser;
            Greeting = user is null ? "Welcome" : auth.IsGuest ? "Hello, guest" : $"Hello, {user.Name.Split(' ')[0]}";
            if (user is not null)
            {
                CanBook = RolePermissions.CanBookRooms(user.Role);
                CanViewAvailability = RolePermissions.CanViewAvailability(user.Role) && !auth.IsGuest;
                CanAccessManage = RolePermissions.CanOpenManage(user.Role) && !auth.IsGuest;
                CanViewReports = RolePermissions.CanViewReports(user.Role) && !auth.IsGuest;
                var locationName = user.LocationId is null
                    ? null
                    : (await rooms.GetLocationAsync(user.LocationId))?.Name;
                Subtitle = auth.IsGuest
                    ? "Browse Flexispace centres and book a room. Client accounts are coming soon."
                    : user.Role switch
                {
                    UserRole.Administrator => "All Flexispace locations",
                    UserRole.CentreManager => $"Managing {locationName ?? "your centre"}",
                    UserRole.Client => "Book a room and manage your bookings",
                    _ => "Book and manage your meeting rooms"
                };
            }
            else
            {
                CanBook = CanViewAvailability = CanAccessManage = CanViewReports = false;
            }

            if (auth.IsGuest)
            {
                UnreadCount = 0;
                TodaysBookings.Clear();
                UpcomingBookings.Clear();
                IsEmpty = true;
            }
            else
            {
                UnreadCount = await notifications.GetUnreadCountAsync();

                TodaysBookings.Clear();
                foreach (var b in await bookings.GetTodaysBookingsAsync())
                {
                    if (b.Status != BookingStatus.Cancelled)
                        TodaysBookings.Add(b);
                }
                IsEmpty = TodaysBookings.Count == 0;

                UpcomingBookings.Clear();
                var upcoming = (await bookings.GetBookingsForCurrentUserScopeAsync())
                    .Where(b => b.Status != BookingStatus.Cancelled && b.Start.Date > DateTime.Today)
                    .OrderBy(b => b.Start)
                    .Take(5);
                foreach (var booking in upcoming)
                    UpcomingBookings.Add(booking);
            }

            if (CanViewAvailability)
            {
                var locationId = user?.Role == UserRole.CentreManager ? user.LocationId : null;
                var live = await rooms.GetAvailabilityAsync(locationId, DateTime.Today);
                RoomsInScope = live.Count;
                AvailableCount = live.Count(r => r.Status == RoomStatus.Available);
                OccupiedCount = live.Count(r => r.Status == RoomStatus.Occupied);
                ReservedCount = live.Count(r => r.Status == RoomStatus.Reserved);
                BlockedCount = live.Count(r => r.Status is RoomStatus.Blocked or RoomStatus.Cleaning or RoomStatus.Maintenance);
                OccupancyPercent = RoomsInScope == 0
                    ? 0
                    : Math.Round(100.0 * (OccupiedCount + ReservedCount + BlockedCount) / RoomsInScope, 0);
            }
            else
            {
                RoomsInScope = AvailableCount = OccupiedCount = ReservedCount = BlockedCount = 0;
                OccupancyPercent = 0;
            }

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
