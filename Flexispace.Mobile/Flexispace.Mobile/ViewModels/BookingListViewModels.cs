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
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private bool canEditStatus;
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime editDate = DateTime.Today;
    [ObservableProperty] private TimeSpan editStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan editEnd = new(10, 0, 0);
    [ObservableProperty] private BookingStatus selectedStatus = BookingStatus.Confirmed;

    /// <summary>Statuses a Centre Manager / Admin may set. Pending is not used.</summary>
    public BookingStatus[] EditableStatuses { get; } =
    [
        BookingStatus.Confirmed,
        BookingStatus.Cancelled,
        BookingStatus.Completed
    ];

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
            CanEdit = CanEditStatus = false;
            return;
        }

        var inScope = user.Role == UserRole.Administrator ||
                      (user.Role == UserRole.CentreManager && user.LocationId == Booking.LocationId);

        CanEditStatus = RolePermissions.CanEditBookings(user.Role) && inScope;
        CanEdit = CanEditStatus && Booking.Status == BookingStatus.Confirmed;

        ResetEditFields();
    }

    /// <summary>
    /// WinUI DatePicker/TimePicker/Entry keep values until blur. Call this from the
    /// page before Save so the VM has what is on screen.
    /// </summary>
    public void ApplyScheduleDraft(DateTime date, TimeSpan start, TimeSpan end, string attendees, string notes)
    {
        EditDate = date;
        EditStart = start;
        EditEnd = end;
        AttendeesText = attendees;
        Notes = notes;
    }

    private void ResetEditFields()
    {
        if (Booking is null) return;

        EditDate = Booking.Start.Date;
        EditStart = Booking.Start.TimeOfDay;
        EditEnd = Booking.End.TimeOfDay;
        AttendeesText = Booking.Attendees.ToString();
        Notes = Booking.Notes;
        SelectedStatus = Booking.Status is BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed
            ? Booking.Status
            : BookingStatus.Confirmed;
    }

    [RelayCommand]
    private async Task CancelEditsAsync()
    {
        if (Booking is null) return;
        ResetEditFields();
        Message = null;
        await ActionFeedback.InfoAsync("Your unsaved edits were discarded. The booking is unchanged.", "Edits cancelled");
    }

    [RelayCommand]
    private async Task SaveEditsAsync()
    {
        if (Booking is null) return;
        if (EditDate.Date < DateTime.Today)
        {
            await ActionFeedback.FailAsync("Bookings cannot be moved to a date before today.");
            return;
        }

        if (EditEnd <= EditStart)
        {
            await ActionFeedback.FailAsync("End time must be after start time.");
            return;
        }

        if (!int.TryParse(AttendeesText, out var attendees) || attendees < 1)
        {
            await ActionFeedback.FailAsync("Enter a valid number of attendees.");
            return;
        }

        var ok = await bookings.UpdateBookingAsync(
            Booking.Id,
            EditDate.Date + EditStart,
            EditDate.Date + EditEnd,
            attendees,
            Notes);
        await LoadAsync();
        Message = null;
        if (ok)
            await ActionFeedback.SuccessAsync("Schedule, headcount, and notes are locked in.", "Booking updated");
        else
            await ActionFeedback.FailAsync("That time overlaps another booking, or you do not have permission.");
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.UpdateBookingStatusAsync(Booking.Id, SelectedStatus);
        await LoadAsync();
        Message = null;
        if (ok)
            await ActionFeedback.SuccessAsync($"Status is now {SelectedStatus}.", "Status saved");
        else
            await ActionFeedback.FailAsync("Could not update the status. Check your permissions and try again.");
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
