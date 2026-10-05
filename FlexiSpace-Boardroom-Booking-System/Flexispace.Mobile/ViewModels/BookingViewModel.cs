using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

[QueryProperty(nameof(PrefillRoomId), "roomId")]
[QueryProperty(nameof(PrefillLocationId), "locationId")]
public partial class BookingViewModel(IAuthService auth, IRoomService rooms, IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private int step = 1;
    [ObservableProperty] private string? prefillRoomId;
    [ObservableProperty] private string? prefillLocationId;
    [ObservableProperty] private OfficeLocation? selectedLocation;
    [ObservableProperty] private Boardroom? selectedRoom;
    [ObservableProperty] private DateTime selectedDate = DateTime.Today;
    [ObservableProperty] private TimeSpan startTime = new(9, 0, 0);
    [ObservableProperty] private TimeSpan endTime = new(10, 0, 0);
    [ObservableProperty] private string company = string.Empty;
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string suggestedSlotsText = string.Empty;
    [ObservableProperty] private bool canBook = true;
    [ObservableProperty] private bool showClientPaymentNote;
    [ObservableProperty] private bool showCombineTip;
    [ObservableProperty] private string combineTipText = string.Empty;
    [ObservableProperty] private bool showCapacityCombineOffer;
    [ObservableProperty] private string capacityCombineMessage = string.Empty;

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<SelectableOption> EquipmentOptions { get; } = [];
    public ObservableCollection<SelectableOption> CateringOptions { get; } = [];

    public string StepHeadline => Step switch
    {
        1 => "Choose a location",
        2 => "Pick a room",
        3 => "Pick a time",
        4 => "Booking details",
        _ => "Book a room"
    };

    partial void OnStepChanged(int value) => OnPropertyChanged(nameof(StepHeadline));

    /// <summary>
    /// Shell reuses the Book tab page, so without an explicit reset the wizard can reopen
    /// on step 3/4 from a previous attempt. Always clear back to step 1, then apply any
    /// deep-link prefill (room/location) to the correct step.
    /// </summary>
    private void ResetWizard()
    {
        Step = 1;
        SelectedLocation = null;
        SelectedRoom = null;
        SelectedDate = DateTime.Today;
        StartTime = new TimeSpan(9, 0, 0);
        EndTime = new TimeSpan(10, 0, 0);
        Company = string.Empty;
        AttendeesText = "4";
        Notes = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
        SuggestedSlotsText = string.Empty;
        ShowCombineTip = false;
        CombineTipText = string.Empty;
        ShowCapacityCombineOffer = false;
        CapacityCombineMessage = string.Empty;
        Rooms.Clear();
        foreach (var o in EquipmentOptions) o.IsSelected = false;
        foreach (var o in CateringOptions) o.IsSelected = false;
    }

    partial void OnPrefillLocationIdChanged(string? value)
    {
        // Shell sometimes applies query properties after OnAppearing — apply late if we
        // already loaded locations (e.g. tapping a centre on Home).
        if (!string.IsNullOrEmpty(value) && Locations.Count > 0)
            _ = ApplyLatePrefillAsync(value, PrefillRoomId);
    }

    partial void OnPrefillRoomIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && Locations.Count > 0)
            _ = ApplyLatePrefillAsync(PrefillLocationId, value);
    }

    private async Task ApplyLatePrefillAsync(string? locationId, string? roomId)
    {
        PrefillLocationId = null;
        PrefillRoomId = null;
        await ApplyPrefillAsync(locationId, roomId);
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        CanBook = user is not null && RolePermissions.CanBookRooms(user.Role);
        ShowClientPaymentNote = user?.Role == UserRole.Client;
        if (!CanBook)
        {
            ErrorMessage = "Your role cannot create bookings.";
            return;
        }

        // Capture query params before reset — Shell leaves sticky Prefill* values on the VM
        // even when you later open //BookingPage with no query string.
        var locationId = PrefillLocationId;
        var roomId = PrefillRoomId;
        PrefillLocationId = null;
        PrefillRoomId = null;

        ResetWizard();

        Locations.Clear();
        // GetLocationsAsync already scopes Centre Managers to their assigned centre.
        foreach (var loc in await rooms.GetLocationsAsync())
            Locations.Add(loc);

        if (EquipmentOptions.Count == 0)
        {
            foreach (var e in SeedData.EquipmentOptions)
                EquipmentOptions.Add(new SelectableOption { Label = e });
            foreach (var c in SeedData.CateringOptions)
                CateringOptions.Add(new SelectableOption { Label = c });
        }

        await ApplyPrefillAsync(locationId, roomId);
    }

    private async Task ApplyPrefillAsync(string? locationId, string? roomId)
    {
        if (string.IsNullOrEmpty(locationId) && string.IsNullOrEmpty(roomId))
            return;

        var user = auth.CurrentUser;
        if (!string.IsNullOrEmpty(locationId) && !RolePermissions.CanAccessLocation(user, locationId))
            locationId = RolePermissions.ScopedLocationId(user);

        if (!string.IsNullOrEmpty(locationId))
        {
            SelectedLocation = Locations.FirstOrDefault(l => l.Id == locationId)
                               ?? await rooms.GetLocationAsync(locationId);
            if (SelectedLocation is null)
                return;
            await LoadRoomsAsync();
            Step = 2;
        }

        if (!string.IsNullOrEmpty(roomId))
        {
            SelectedRoom = Rooms.FirstOrDefault(r => r.Id == roomId)
                           ?? await rooms.GetRoomAsync(roomId);
            if (SelectedRoom is not null &&
                !RolePermissions.CanAccessLocation(user, SelectedRoom.LocationId))
            {
                SelectedRoom = null;
                return;
            }

            if (SelectedRoom is not null && SelectedLocation is null)
            {
                SelectedLocation = await rooms.GetLocationAsync(SelectedRoom.LocationId);
                await LoadRoomsAsync();
                SelectedRoom = Rooms.FirstOrDefault(r => r.Id == roomId) ?? SelectedRoom;
            }

            // Room was chosen already (Live map / Location Detail) — skip to schedule.
            if (SelectedRoom is not null)
                Step = 3;
        }
    }

    [RelayCommand]
    private async Task SelectLocationAsync(OfficeLocation? location)
    {
        if (location is null || !RolePermissions.CanAccessLocation(auth.CurrentUser, location.Id))
            return;

        SelectedLocation = location;
        SelectedRoom = null;
        await LoadRoomsAsync();
        Step = 2;
    }

    private async Task LoadRoomsAsync()
    {
        Rooms.Clear();
        if (SelectedLocation is null)
        {
            ShowCombineTip = false;
            CombineTipText = string.Empty;
            return;
        }

        foreach (var room in await rooms.GetRoomsAsync(SelectedLocation.Id))
            Rooms.Add(room);

        ShowCombineTip = Rooms.Any(r => r.IsCombined);
        CombineTipText = ShowCombineTip
            ? "Need a bigger group? Thingamajik and Whachamacallit open into one suite (seats 14). Look for the Combined option."
            : string.Empty;
    }

    [RelayCommand]
    private void SelectRoom(Boardroom? room)
    {
        SelectedRoom = room;
        ShowCapacityCombineOffer = false;
        CapacityCombineMessage = string.Empty;
        ErrorMessage = null;
        Step = 3;
    }

    [RelayCommand]
    private void UseCombinedSuite()
    {
        var combined = Rooms.FirstOrDefault(r => r.IsCombined)
                       ?? RoomCombinations.CreateCombinedOption();
        if (!Rooms.Any(r => r.Id == combined.Id))
            Rooms.Insert(0, combined);

        SelectedRoom = combined;
        ShowCapacityCombineOffer = false;
        CapacityCombineMessage = string.Empty;
        if (ErrorMessage is not null &&
            (ErrorMessage.StartsWith("This room seats", StringComparison.Ordinal) ||
             ErrorMessage.Contains("combine", StringComparison.OrdinalIgnoreCase)))
            ErrorMessage = null;

        // Stay on details so they can keep their headcount; clear capacity error.
        RefreshCapacityGuidance();
    }

    [RelayCommand]
    private void NextToDetails()
    {
        if (!ValidateSchedule())
            return;

        ErrorMessage = null;
        Step = 4;
    }

    [RelayCommand]
    private void Back()
    {
        if (Step > 1) Step--;
    }

    /// <summary>
    /// Bookings cannot start before today. Shown immediately on Continue / Confirm,
    /// without touching DatePicker MinimumDate (that triggers a WinUI calendar-month bug).
    /// </summary>
    private bool ValidateSchedule()
    {
        if (SelectedDate.Date < DateTime.Today)
        {
            ErrorMessage = "Bookings cannot be made for a date before today.";
            return false;
        }

        if (EndTime <= StartTime)
        {
            ErrorMessage = "End time must be after start time.";
            return false;
        }

        return true;
    }

    partial void OnAttendeesTextChanged(string value) => RefreshCapacityGuidance();

    partial void OnSelectedRoomChanged(Boardroom? value) => RefreshCapacityGuidance();

    private void RefreshCapacityGuidance()
    {
        ShowCapacityCombineOffer = false;
        CapacityCombineMessage = string.Empty;

        if (SelectedRoom is null) return;
        if (!int.TryParse(AttendeesText, out var attendees) || attendees < 1) return;

        if (attendees <= SelectedRoom.Capacity)
        {
            if (ErrorMessage is not null &&
                (ErrorMessage.StartsWith("This room seats", StringComparison.Ordinal) ||
                 ErrorMessage.Contains("combine", StringComparison.OrdinalIgnoreCase)))
                ErrorMessage = null;
            return;
        }

        // Over capacity — offer the combined suite when it would solve the problem.
        var combined = Rooms.FirstOrDefault(r => r.IsCombined) ??
                       (RoomCombinations.IsPairMember(SelectedRoom.Id) || SelectedRoom.IsCombined
                           ? RoomCombinations.CreateCombinedOption()
                           : null);

        if (combined is not null &&
            !SelectedRoom.IsCombined &&
            attendees <= combined.Capacity &&
            (RoomCombinations.IsPairMember(SelectedRoom.Id) || SelectedRoom.LocationId == "eagle"))
        {
            ShowCapacityCombineOffer = true;
            CapacityCombineMessage =
                $"{SelectedRoom.Name} seats {SelectedRoom.Capacity}. " +
                $"For {attendees} people, book Thingamajik + Whachamacallit together (seats {combined.Capacity}).";
            ErrorMessage = null;
            return;
        }

        ErrorMessage = $"This room seats {SelectedRoom.Capacity}; reduce attendees or pick a bigger room.";
    }

    /// <summary>
    /// Attendee count must be a positive number that fits the selected room.
    /// </summary>
    private bool ValidateAttendees()
    {
        if (SelectedRoom is null)
        {
            ErrorMessage = "Select a boardroom first.";
            return false;
        }

        if (!int.TryParse(AttendeesText, out var attendees) || attendees < 1)
        {
            ErrorMessage = "Enter a valid number of attendees.";
            return false;
        }

        if (attendees > SelectedRoom.Capacity)
        {
            RefreshCapacityGuidance();
            if (ShowCapacityCombineOffer)
                return false;

            ErrorMessage = $"This room seats {SelectedRoom.Capacity}; reduce attendees or pick a bigger room.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedRoom is null)
        {
            ErrorMessage = "Select a boardroom first.";
            return;
        }

        if (!ValidateSchedule())
            return;

        if (!ValidateAttendees())
            return;

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        SuggestedSlotsText = string.Empty;
        try
        {
            var start = SelectedDate.Date + StartTime;
            var end = SelectedDate.Date + EndTime;
            var result = await bookings.CreateBookingAsync(new BookingRequest
            {
                RoomId = SelectedRoom.Id,
                Start = start,
                End = end,
                Company = Company,
                Attendees = int.TryParse(AttendeesText, out var n) ? Math.Max(1, n) : 1,
                Equipment = EquipmentOptions.Where(x => x.IsSelected).Select(x => x.Label).ToList(),
                Catering = CateringOptions.Where(x => x.IsSelected).Select(x => x.Label).ToList(),
                Notes = Notes
            });

            if (!result.Success)
            {
                ErrorMessage = result.Message;
                if (result.SuggestedSlots.Count > 0)
                {
                    SuggestedSlotsText = "Suggested: " + string.Join(", ",
                        result.SuggestedSlots.Select(s => s.ToString("HH:mm")));
                }

                var detail = result.SuggestedSlots.Count > 0
                    ? $"{result.Message}\n\nTry: {string.Join(", ", result.SuggestedSlots.Select(s => s.ToString("HH:mm")))}"
                    : result.Message;
                await ActionFeedback.FailAsync(detail, "Booking blocked");
                return;
            }

            SuccessMessage = result.Message;
            Step = 5;
            await ActionFeedback.SuccessAsync("Your boardroom is locked in. Opening confirmation…", "Booked");
            if (result.Booking is not null)
                await Shell.Current.GoToAsync($"BookingConfirmationPage?bookingId={result.Booking.Id}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");
}

public partial class SelectableOption : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    [ObservableProperty] private bool isSelected;

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}
