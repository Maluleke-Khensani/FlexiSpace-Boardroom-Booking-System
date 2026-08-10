using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class HomeViewModel(IAuthService auth, IBookingService bookings, IRoomService rooms, INotificationService notifications) : ObservableObject
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
    private async Task BookRoomAsync()
    {
        if (!CanBook) return;
        await Shell.Current.GoToAsync("//BookingPage");
    }

    [RelayCommand]
    private async Task OpenManageAsync()
    {
        if (!CanAccessManage) return;
        await Shell.Current.GoToAsync("//ManagePage");
    }

    [RelayCommand]
    private async Task OpenLocationAsync(OfficeLocation? location)
    {
        if (location is null) return;
        await Shell.Current.GoToAsync($"LocationDetailPage?locationId={location.Id}");
    }

    [RelayCommand]
    private async Task OpenBookingAsync(Booking? booking)
    {
        if (booking is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={booking.Id}");
    }
}
