using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Five-step booking wizard: location, room, date/time, details, then in-person payment.
public partial class BookingViewModel(IAuthService auth, IRoomService rooms, IBookingService bookings, INavigationService nav, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private int step = 1;
    [ObservableProperty] private string? prefillRoomId;
    [ObservableProperty] private string? prefillLocationId;
    [ObservableProperty] private OfficeLocation? selectedLocation;
    [ObservableProperty] private Boardroom? selectedRoom;
    [ObservableProperty] private DateTime selectedDate = DateTime.Today;
    [ObservableProperty] private TimeSpan? startTime;
    [ObservableProperty] private TimeSpan? endTime;
    [ObservableProperty] private string company = string.Empty;
    [ObservableProperty] private string attendeesText = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? errorAnchor;
    [ObservableProperty] private string? successMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string suggestedSlotsText = string.Empty;
    [ObservableProperty] private bool canBook = true;
    [ObservableProperty] private bool conjoinEnabled;
    [ObservableProperty] private bool canConjoin;
    [ObservableProperty] private string? conjoinPartnerName;
    [ObservableProperty] private int conjoinCapacity;
    [ObservableProperty] private bool showGuestAccountPrompt;

    public List<OccupiedSlot> OccupiedSlots { get; } = [];

    public IReadOnlyList<TimeSpan> UnavailableStartSlots =>
        OccupiedSlots
            .SelectMany(slot => EnumerateHalfHours(slot.Start, slot.End))
            .Distinct()
            .ToList();

    public IReadOnlyList<TimeSpan> UnavailableEndSlots
    {
        get
        {
            if (StartTime is not { } start)
                return [];

            var taken = new List<TimeSpan>();
            for (var t = start.Add(TimeSpan.FromMinutes(30)); t <= new TimeSpan(19, 0, 0); t = t.Add(TimeSpan.FromMinutes(30)))
            {
                // Grey every end that would overlap a booking or block from the chosen start.
                // Ending exactly when a busy period starts is still allowed (exclusive end).
                if (OccupiedSlots.Any(slot => start < slot.End && t > slot.Start))
                    taken.Add(t);
            }

            return taken;
        }
    }

    public string BookingRoomLabel =>
        ConjoinEnabled && CanConjoin && SelectedRoom is not null && !string.IsNullOrEmpty(ConjoinPartnerName)
            ? $"{SelectedRoom.Name} + {ConjoinPartnerName}"
            : SelectedRoom?.Name ?? string.Empty;

    public int BookingCapacity =>
        ConjoinEnabled && CanConjoin ? ConjoinCapacity : SelectedRoom?.Capacity ?? 0;

    public IReadOnlyList<string> IncludedRoomEquipment
    {
        get
        {
            if (SelectedRoom is null)
                return [];

            var roomIds = ConjoinEnabled && CanConjoin
                ? RoomCombinations.GetCombinationFor(SelectedRoom.Id)?.CombinedRoomIds ?? [SelectedRoom.Id]
                : [SelectedRoom.Id];

            return Rooms
                .Where(room => roomIds.Contains(room.Id))
                .SelectMany(room => room.Equipment)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool AttendeesExceedCapacity =>
        BookingCapacity > 0 && int.TryParse(AttendeesText, out var attendees) && attendees > BookingCapacity;

    public string? AttendeeCapacityMessage => AttendeesExceedCapacity
        ? $"This room seats {BookingCapacity}. Reduce the attendee count to continue."
        : null;

    partial void OnAttendeesTextChanged(string value) => NotifyRoomBookingDetailsChanged();

    private void NotifyRoomBookingDetailsChanged()
    {
        OnPropertyChanged(nameof(BookingCapacity));
        OnPropertyChanged(nameof(IncludedRoomEquipment));
        OnPropertyChanged(nameof(AttendeesExceedCapacity));
        OnPropertyChanged(nameof(AttendeeCapacityMessage));
    }

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<SelectableOption> EquipmentOptions { get; } = [];
    public ObservableCollection<SelectableOption> CateringOptions { get; } = [];
    public ObservableCollection<DateTime> SuggestedSlots { get; } = [];

    public void SetPrefill(string? locationId, string? roomId)
    {
        PrefillLocationId = locationId;
        PrefillRoomId = roomId;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        CanBook = user is not null && RolePermissions.CanBookRooms(user.Role);
        if (!CanBook)
        {
            SetError("Your role cannot create bookings.", "fs-error-banner");
            return;
        }

        Locations.Clear();
        var all = await rooms.GetLocationsAsync();
        foreach (var loc in all)
        {
            if (user?.Role == UserRole.CentreManager &&
                !string.IsNullOrEmpty(user.LocationId) &&
                loc.Id != user.LocationId)
                continue;
            Locations.Add(loc);
        }

        EquipmentOptions.Clear();
        foreach (var e in await rooms.GetEquipmentCatalogItemsAsync())
            EquipmentOptions.Add(new SelectableOption { Label = e.Name, Description = e.Description, IsActive = e.IsActive });

        CateringOptions.Clear();
        foreach (var c in await rooms.GetCateringCatalogItemsAsync())
            CateringOptions.Add(new SelectableOption { Label = c.Name, Description = c.Description, IsActive = c.IsActive });

        await InitPrefillAsync();
    }

    private async Task InitPrefillAsync()
    {
        if (!string.IsNullOrEmpty(PrefillLocationId))
        {
            SelectedLocation = Locations.FirstOrDefault(l => l.Id == PrefillLocationId)
                               ?? await rooms.GetLocationAsync(PrefillLocationId);
            await LoadRoomsAsync();
        }

        if (!string.IsNullOrEmpty(PrefillRoomId))
        {
            SelectedRoom = Rooms.FirstOrDefault(r => r.Id == PrefillRoomId)
                           ?? await rooms.GetRoomAsync(PrefillRoomId);
            if (SelectedRoom is not null && SelectedLocation is null)
            {
                SelectedLocation = await rooms.GetLocationAsync(SelectedRoom.LocationId);
                await LoadRoomsAsync();
                SelectedRoom = Rooms.FirstOrDefault(r => r.Id == PrefillRoomId);
            }
            Step = Math.Max(Step, 2);
        }

        if (SelectedRoom?.IsCombined == true)
        {
            var firstId = SelectedRoom.CombinedRoomIds.FirstOrDefault();
            SelectedRoom = Rooms.FirstOrDefault(r => r.Id == firstId) ?? SelectedRoom;
            RefreshConjoinState();
            SetConjoin(true);
        }
        else
        {
            RefreshConjoinState();
        }
    }

    [RelayCommand]
    private async Task SelectLocationAsync(OfficeLocation? location)
    {
        SelectedLocation = location;
        SelectedRoom = null;
        ConjoinEnabled = false;
        ClearError();
        await LoadRoomsAsync();
        Step = 2;
    }

    private async Task LoadRoomsAsync()
    {
        Rooms.Clear();
        if (SelectedLocation is null) return;
        foreach (var room in await rooms.GetRoomsAsync(SelectedLocation.Id))
            Rooms.Add(room);
    }

    [RelayCommand]
    private void SelectRoom(Boardroom? room)
    {
        SelectedRoom = room;
        RefreshConjoinState();
    }

    public void SetConjoin(bool enabled)
    {
        ConjoinEnabled = CanConjoin && enabled;
        OnPropertyChanged(nameof(BookingRoomLabel));
        NotifyRoomBookingDetailsChanged();
    }

    private void RefreshConjoinState()
    {
        var combo = SelectedRoom is null ? null : RoomCombinations.GetCombinationFor(SelectedRoom.Id);
        CanConjoin = combo is not null;
        if (combo is null)
        {
            ConjoinEnabled = false;
            ConjoinPartnerName = null;
            ConjoinCapacity = 0;
            OnPropertyChanged(nameof(BookingRoomLabel));
        NotifyRoomBookingDetailsChanged();
            return;
        }

        var partnerId = combo.CombinedRoomIds.First(id => id != SelectedRoom!.Id);
        ConjoinPartnerName = Rooms.FirstOrDefault(r => r.Id == partnerId)?.Name
                             ?? SeedData.Rooms.FirstOrDefault(r => r.Id == partnerId)?.Name;
        ConjoinCapacity = combo.Capacity;
        OnPropertyChanged(nameof(BookingRoomLabel));
        NotifyRoomBookingDetailsChanged();
    }

    [RelayCommand]
    private async Task ContinueToScheduleAsync()
    {
        if (SelectedRoom is null)
            return;

        await RefreshOccupiedAsync();
        Step = 3;
    }

    [RelayCommand]
    private async Task RefreshOccupiedAsync()
    {
        OccupiedSlots.Clear();
        var roomId = ConjoinEnabled && CanConjoin
            ? RoomCombinations.GetCombinationFor(SelectedRoom?.Id ?? "")?.Id ?? SelectedRoom?.Id
            : SelectedRoom?.Id;
        if (string.IsNullOrEmpty(roomId))
        {
            NotifyUnavailableSlots();
            return;
        }

        foreach (var slot in await bookings.GetOccupiedSlotsAsync(roomId, SelectedDate))
            OccupiedSlots.Add(slot);

        OnPropertyChanged(nameof(OccupiedSlots));
        NotifyUnavailableSlots();
    }

    partial void OnStartTimeChanged(TimeSpan? value) => NotifyUnavailableSlots();

    private static IEnumerable<TimeSpan> EnumerateHalfHours(TimeSpan start, TimeSpan end)
    {
        var minutes = Math.Max(0, (int)start.TotalMinutes);
        var t = TimeSpan.FromMinutes(minutes - minutes % 30);
        for (; t < end; t = t.Add(TimeSpan.FromMinutes(30)))
            yield return t;
    }

    private void NotifyUnavailableSlots()
    {
        OnPropertyChanged(nameof(UnavailableStartSlots));
        OnPropertyChanged(nameof(UnavailableEndSlots));
    }

    [RelayCommand]
    private void NextToDetails()
    {
        if (StartTime is not { } start || EndTime is not { } end)
        {
            SetError("Choose a start time and an end time.", "book-start");
            return;
        }

        if (OccupiedSlots.Any(slot => start < slot.End && end > slot.Start))
        {
            SetError("That time overlaps an existing booking or a blocked period. Try one of the suggested times.", "book-start");
            FillSuggestions(end - start);
            return;
        }

        ClearError();
        SuggestedSlots.Clear();
        SuggestedSlotsText = string.Empty;
        Step = 4;
    }

    [RelayCommand]
    private void NextToPayment()
    {
        if (string.IsNullOrWhiteSpace(Company))
        {
            SetError("Please enter the company this meeting is for.", "book-company");
            return;
        }

        if (!int.TryParse(AttendeesText, out var attendees) || attendees < 1)
        {
            SetError("Enter how many people will attend.", "book-attendees");
            return;
        }

        var capacity = ConjoinEnabled && CanConjoin ? ConjoinCapacity : SelectedRoom?.Capacity ?? 0;
        if (capacity > 0 && attendees > capacity)
        {
            SetError($"{BookingRoomLabel} seats {capacity}. Reduce the attendee count or choose a larger room.", "book-attendees");
            return;
        }

        ClearError();
        Step = 5;
    }

    partial void OnStepChanged(int value)
    {
        if (value != 5)
            ShowGuestAccountPrompt = false;
    }

    [RelayCommand]
    private void Back()
    {
        if (ShowGuestAccountPrompt)
        {
            ShowGuestAccountPrompt = false;
            return;
        }

        if (Step > 1) Step--;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedRoom is null)
        {
            SetError("Select a boardroom first.", "book-room");
            Step = 2;
            return;
        }

        if (StartTime is not { } startTime || EndTime is not { } endTime)
        {
            SetError("Choose a start time and an end time.", "book-start");
            Step = 3;
            return;
        }

        if (auth.IsGuest)
        {
            ShowGuestAccountPrompt = true;
            return;
        }

        IsBusy = true;
        ClearError();
        SuccessMessage = null;
        SuggestedSlotsText = string.Empty;
        try
        {
            var start = SelectedDate.Date + startTime;
            var end = SelectedDate.Date + endTime;
            var roomId = ConjoinEnabled && CanConjoin
                ? RoomCombinations.GetCombinationFor(SelectedRoom.Id)?.Id ?? SelectedRoom.Id
                : SelectedRoom.Id;

            var result = await bookings.CreateBookingAsync(new BookingRequest
            {
                RoomId = roomId,
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
                var anchor = result.Message.Contains("time", StringComparison.OrdinalIgnoreCase) ||
                             result.Message.Contains("booked", StringComparison.OrdinalIgnoreCase) ||
                             result.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase)
                    ? "book-start"
                    : "fs-error-banner";
                if (anchor == "book-start")
                    Step = 3;
                SetError(result.Message, anchor);
                if (result.SuggestedSlots.Count > 0)
                    ApplySuggestedStarts(result.SuggestedSlots);
                else
                    FillSuggestions(end - start);
                return;
            }

            SuccessMessage = result.Message;
            if (result.Booking is not null)
            {
                await notifications.AddAsync(new AppNotification
                {
                    Title = "Booking confirmed",
                    Message = $"Your booking for {result.Booking.RoomName} on {result.Booking.Start:g} is confirmed.",
                    Type = "Confirmation",
                    BookingId = result.Booking.Id
                });
                nav.NavigateTo($"/bookings/{result.Booking.Id}/confirmation");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetError(string message, string anchor)
    {
        ErrorMessage = message;
        ErrorAnchor = anchor;
    }

    private void ClearError()
    {
        ErrorMessage = null;
        ErrorAnchor = null;
    }

    public void ApplySuggestedStart(DateTime start)
    {
        var duration = StartTime is { } s && EndTime is { } e && e > s
            ? e - s
            : TimeSpan.FromHours(1);
        StartTime = start.TimeOfDay;
        EndTime = start.TimeOfDay.Add(duration);
        SuggestedSlots.Clear();
        SuggestedSlotsText = string.Empty;
        ClearError();
    }

    private void FillSuggestions(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            duration = TimeSpan.FromHours(1);

        var found = new List<DateTime>();
        for (var t = new TimeSpan(8, 0, 0);
             t.Add(duration) <= new TimeSpan(19, 0, 0) && found.Count < 3;
             t = t.Add(TimeSpan.FromMinutes(30)))
        {
            var end = t.Add(duration);
            if (OccupiedSlots.Any(slot => t < slot.End && end > slot.Start))
                continue;
            found.Add(SelectedDate.Date + t);
        }

        ApplySuggestedStarts(found);
    }

    private void ApplySuggestedStarts(IEnumerable<DateTime> starts)
    {
        SuggestedSlots.Clear();
        foreach (var start in starts.Take(3))
            SuggestedSlots.Add(start);
        SuggestedSlotsText = SuggestedSlots.Count == 0
            ? string.Empty
            : "Suggested: " + string.Join(", ", SuggestedSlots.Select(s => s.ToString("HH:mm")));
    }
}

//Checkbox option used in the booking wizard for equipment and catering add-ons.
public partial class SelectableOption : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    [ObservableProperty] private bool isSelected;
}

//Row model for live availability: a room plus its location name and status colour.
public partial class AvailabilityItem : ObservableObject
{
    public Boardroom Room { get; init; } = null!;
    public string LocationName { get; init; } = string.Empty;
    public string StatusText => Room.Status.ToString();
    public string StatusColor => RoomStatusPalette.GetColor(StatusText);
}

//Live room availability: location filter, map/list views, and quick book shortcuts.
public partial class AvailabilityViewModel(IAuthService auth, IRoomService rooms, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string selectedLocationId = string.Empty;
    [ObservableProperty] private string selectedLocationName = "All locations";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool canBook;
    [ObservableProperty] private bool canViewAvailability;
    [ObservableProperty] private bool lockLocationFilter;
    [ObservableProperty] private string? accessMessage;
    [ObservableProperty] private int availableCount;
    [ObservableProperty] private int reservedCount;
    [ObservableProperty] private int occupiedCount;
    [ObservableProperty] private int cleaningCount;
    [ObservableProperty] private int maintenanceCount;
    [ObservableProperty] private int blockedCount;

    public ObservableCollection<AvailabilityItem> Items { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        CanBook = user is not null && RolePermissions.CanBookRooms(user.Role);
        CanViewAvailability = user is not null && !auth.IsGuest && RolePermissions.CanViewAvailability(user.Role);
        LockLocationFilter = user?.Role == UserRole.CentreManager && !string.IsNullOrEmpty(user.LocationId);

        if (!CanViewAvailability)
        {
            AccessMessage = "Live availability is for Staff, Centre Managers, and Administrators.";
            Items.Clear();
            IsEmpty = true;
            AvailableCount = ReservedCount = OccupiedCount = CleaningCount = MaintenanceCount = BlockedCount = 0;
            return;
        }

        AccessMessage = null;
        Locations.Clear();
        if (!LockLocationFilter)
            Locations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });

        foreach (var loc in await rooms.GetLocationsAsync())
        {
            if (LockLocationFilter && loc.Id != user!.LocationId) continue;
            Locations.Add(loc);
        }

        if (LockLocationFilter)
        {
            SelectedLocationId = user!.LocationId!;
            SelectedLocationName = Locations.FirstOrDefault()?.Name ?? user.LocationId!;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task FilterAsync(OfficeLocation? location)
    {
        if (location is null || LockLocationFilter) return;
        SelectedLocationId = location.Id;
        SelectedLocationName = location.Name;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var locMap = (await rooms.GetLocationsAsync()).ToDictionary(l => l.Id, l => l.Name);
            var roomsList = await rooms.GetAvailabilityAsync(
                string.IsNullOrEmpty(SelectedLocationId) ? null : SelectedLocationId,
                DateTime.Today);

            Items.Clear();
            foreach (var room in roomsList)
            {
                Items.Add(new AvailabilityItem
                {
                    Room = room,
                    LocationName = locMap.GetValueOrDefault(room.LocationId, room.LocationId)
                });
            }
            IsEmpty = Items.Count == 0;
            AvailableCount = Items.Count(i => i.Room.Status == RoomStatus.Available);
            ReservedCount = Items.Count(i => i.Room.Status == RoomStatus.Reserved);
            OccupiedCount = Items.Count(i => i.Room.Status == RoomStatus.Occupied);
            CleaningCount = Items.Count(i => i.Room.Status == RoomStatus.Cleaning);
            MaintenanceCount = Items.Count(i => i.Room.Status == RoomStatus.Maintenance);
            BlockedCount = Items.Count(i => i.Room.Status == RoomStatus.Blocked);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BookRoom(AvailabilityItem? item)
    {
        if (item is null || !CanBook) return;
        nav.NavigateTo($"/book?roomId={item.Room.Id}&locationId={item.Room.LocationId}");
    }
}
