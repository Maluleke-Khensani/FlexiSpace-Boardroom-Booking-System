using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class MyBookingsViewModel(IAuthService auth, IBookingService bookings) : ObservableObject
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
    private async Task ToggleFilterAsync()
    {
        if (!CanToggleScope) return;
        ShowMineOnly = !ShowMineOnly;
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
    private async Task OpenAsync(Booking? booking)
    {
        if (booking is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={booking.Id}");
    }
}

[QueryProperty(nameof(BookingId), "bookingId")]
public partial class BookingDetailViewModel(IAuthService auth, IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool canCancel;
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime editDate = DateTime.Today;
    [ObservableProperty] private TimeSpan editStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan editEnd = new(10, 0, 0);

    public string DurationLabel => Booking is null
        ? string.Empty
        : $"{Math.Max(0, (int)(Booking.End - Booking.Start).TotalMinutes)} min";

    public string WhenRange => Booking is null
        ? string.Empty
        : $"{Booking.Start:HH:mm}  –  {Booking.End:HH:mm}  ·  {DurationLabel}";

    public bool HasNotes => !string.IsNullOrWhiteSpace(Booking?.Notes);
    public bool HasOutlook => !string.IsNullOrWhiteSpace(Booking?.OutlookEventId);
    public bool HasExtraDetails => HasNotes || HasOutlook;
    public bool HasEquipment => Booking?.Equipment.Count > 0;
    public bool HasCatering => Booking?.Catering.Count > 0;

    partial void OnBookingIdChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id)) return;
        Booking = await bookings.GetBookingAsync(id);

        // The store mutates the same Booking instance in place (cancel/edit),
        // so the generated setter's reference-equality check would otherwise skip
        // raising PropertyChanged and the on-screen Status/Start/End labels would
        // silently go stale. Force a refresh every time we reload.
        OnPropertyChanged(nameof(Booking));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(WhenRange));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasOutlook));
        OnPropertyChanged(nameof(HasExtraDetails));
        OnPropertyChanged(nameof(HasEquipment));
        OnPropertyChanged(nameof(HasCatering));

        var user = auth.CurrentUser;
        if (Booking is null || user is null)
        {
            CanCancel = CanEdit = false;
            return;
        }

        var isOwner = Booking.BookerId == user.Id;
        var inScope = user.Role == UserRole.Administrator ||
                      (user.Role == UserRole.CentreManager && user.LocationId == Booking.LocationId);

        CanCancel = (isOwner && RolePermissions.CanCancelOwnBookings(user.Role) &&
                     Booking.Status == BookingStatus.Confirmed) ||
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
    private async Task SaveEditsAsync()
    {
        if (Booking is null) return;
        if (EditDate.Date < DateTime.Today)
        {
            Message = "Bookings cannot be moved to a date before today.";
            return;
        }

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

[QueryProperty(nameof(BookingId), "bookingId")]
public partial class BookingConfirmationViewModel(IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;

    public bool IsConfirmed => Booking is not null && Booking.Status == BookingStatus.Confirmed;

    partial void OnBookingIdChanged(string value) => _ = LoadAsync();

    partial void OnBookingChanged(Booking? value) => OnPropertyChanged(nameof(IsConfirmed));

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id)) return;
        Booking = await bookings.GetBookingAsync(id);
        OnPropertyChanged(nameof(Booking));
        OnPropertyChanged(nameof(IsConfirmed));
    }

    [RelayCommand]
    private async Task DoneAsync() => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    private async Task ViewBookingsAsync() => await Shell.Current.GoToAsync("MyBookingsPage");

    [RelayCommand]
    private async Task ViewAlertsAsync() => await Shell.Current.GoToAsync("//NotificationsPage");
}
