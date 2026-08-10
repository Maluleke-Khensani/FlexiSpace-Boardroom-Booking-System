using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

/// <summary>
/// Centre Manager / Administrator booking console — approve, decline, and keep an eye on
/// bookings in scope. Room/user administration and reporting are deliberately not built
/// here: per the team's project plan, those are the React website's admin dashboard, and
/// mobile is scoped to core on-the-go booking features only.
/// </summary>
public partial class ManageViewModel(IAuthService auth, IBookingService bookings) : ObservableObject
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
    private async Task CancelAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.CancelBookingAsync(booking.Id);
        Message = ok ? "Booking cancelled." : "Could not cancel booking.";
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
    private async Task OpenBookingAsync(Booking? booking)
    {
        if (booking is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={booking.Id}");
    }
}
