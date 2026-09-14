using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Booking history list with optional scope filter (my bookings vs all in role scope).
public partial class MyBookingsViewModel(IAuthService auth, IBookingService bookings, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool showMineOnly = true;
    [ObservableProperty] private bool canToggleScope;
    [ObservableProperty] private string filterLabel = "My bookings";

    public ObservableCollection<Booking> Items { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var role = auth.CurrentUser?.Role;
        CanToggleScope = role is UserRole.Administrator or UserRole.CentreManager;
        if (role is UserRole.Administrator or UserRole.CentreManager)
            ShowMineOnly = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SetFilterAsync(bool mineOnly)
    {
        if (!CanToggleScope || ShowMineOnly == mineOnly) return;
        ShowMineOnly = mineOnly;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            FilterLabel = ShowMineOnly ? "Showing: my bookings" : "Showing: all in scope";
            IReadOnlyList<Booking> list;
            if (ShowMineOnly)
                list = await bookings.GetBookingsAsync(userId: auth.CurrentUser?.Id);
            else
                list = await bookings.GetBookingsForCurrentUserScopeAsync();

            Items.Clear();
            foreach (var b in list)
                Items.Add(b);
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Open(Booking? booking)
    {
        if (booking is null) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }
}

//Single booking detail: view, edit, cancel, approve, or decline based on the signed-in user's role.
public partial class BookingDetailViewModel(IAuthService auth, IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool canCancel;
    [ObservableProperty] private bool canApprove;
    [ObservableProperty] private bool canDecline;
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime editDate = DateTime.Today;
    [ObservableProperty] private TimeSpan editStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan editEnd = new(10, 0, 0);

    public void SetBookingId(string id)
    {
        BookingId = id;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id)) return;
        Booking = await bookings.GetBookingAsync(id);
        OnPropertyChanged(nameof(Booking));

        var user = auth.CurrentUser;
        if (Booking is null || user is null)
        {
            CanCancel = CanApprove = CanDecline = CanEdit = false;
            return;
        }

        var isOwner = Booking.BookerId == user.Id;
        var inScope = user.Role == UserRole.Administrator ||
                      (user.Role == UserRole.CentreManager && user.LocationId == Booking.LocationId);

        CanApprove = Booking.Status == BookingStatus.Pending &&
                     RolePermissions.CanApproveBookings(user.Role) && inScope;
        CanDecline = CanApprove;

        CanCancel = (isOwner && RolePermissions.CanCancelOwnBookings(user.Role) &&
                     Booking.Status is BookingStatus.Confirmed or BookingStatus.Pending) ||
                    (!isOwner && RolePermissions.CanCancelAnyBooking(user.Role) && inScope &&
                     Booking.Status == BookingStatus.Confirmed);

        CanEdit = Booking.Status != BookingStatus.Cancelled &&
                  RolePermissions.CanEditBookings(user.Role) && inScope;

        if (Booking is not null)
        {
            EditDate = Booking.Start.Date;
            EditStart = Booking.Start.TimeOfDay;
            EditEnd = Booking.End.TimeOfDay;
            AttendeesText = Booking.Attendees.ToString();
            Notes = Booking.Notes;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.CancelBookingAsync(Booking.Id);
        Message = ok ? "Booking cancelled." : "You don't have permission to cancel this booking.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.ApproveBookingAsync(Booking.Id);
        Message = ok ? "Booking approved." : "Unable to approve.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeclineAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.DeclineBookingAsync(Booking.Id);
        Message = ok ? "Booking declined." : "Unable to decline.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveEditsAsync()
    {
        if (Booking is null) return;
        _ = int.TryParse(AttendeesText, out var attendees);
        var ok = await bookings.UpdateBookingAsync(
            Booking.Id,
            EditDate.Date + EditStart,
            EditDate.Date + EditEnd,
            attendees,
            Notes);
        Message = ok ? "Booking updated." : "Update failed — check times/permissions.";
        await LoadAsync();
    }
}

//Post-booking confirmation screen shown after a successful reservation is created.
public partial class BookingConfirmationViewModel(IBookingService bookings, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private bool isPending;

    public void SetBookingId(string id)
    {
        BookingId = id;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id)) return;
        Booking = await bookings.GetBookingAsync(id);
        OnPropertyChanged(nameof(Booking));
        IsPending = Booking?.Status == BookingStatus.Pending;
    }

    [RelayCommand]
    private void Done() => nav.NavigateTo("/home");

    [RelayCommand]
    private void ViewBookings() => nav.NavigateTo("/my-bookings");

    [RelayCommand]
    private void ViewAlerts() => nav.NavigateTo("/notifications");
}
