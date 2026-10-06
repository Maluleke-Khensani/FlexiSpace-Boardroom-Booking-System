using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Four-step booking wizard: location, room, date/time, then company and catering details.
public partial class BookingViewModel(IAuthService auth, IRoomService rooms, IBookingService bookings, INavigationService nav) : ObservableObject
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
    [ObservableProperty] private string attendeesText = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string suggestedSlotsText = string.Empty;
    [ObservableProperty] private bool canBook = true;
    [ObservableProperty] private bool conjoinEnabled;
    [ObservableProperty] private bool canConjoin;
    [ObservableProperty] private string? conjoinPartnerName;
    [ObservableProperty] private int conjoinCapacity;

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
            var taken = new List<TimeSpan>();
            for (var t = StartTime.Add(TimeSpan.FromMinutes(30)); t <= new TimeSpan(19, 0, 0); t = t.Add(TimeSpan.FromMinutes(30)))
            {
                if (OccupiedSlots.Any(slot => StartTime < slot.End && t > slot.Start))
                    taken.Add(t);
            }

            return taken;
        }
    }

    public string BookingRoomLabel =>
        ConjoinEnabled && CanConjoin && SelectedRoom is not null && !string.IsNullOrEmpty(ConjoinPartnerName)
            ? $"{SelectedRoom.Name} + {ConjoinPartnerName}"
            : SelectedRoom?.Name ?? string.Empty;

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<SelectableOption> EquipmentOptions { get; } = [];
    public ObservableCollection<SelectableOption> CateringOptions { get; } = [];

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
            ErrorMessage = "Your role cannot create bookings.";
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

        if (EquipmentOptions.Count == 0)
        {
            foreach (var e in await rooms.GetEquipmentCatalogAsync())
                EquipmentOptions.Add(new SelectableOption { Label = e });
            foreach (var c in await rooms.GetCateringCatalogAsync())
                CateringOptions.Add(new SelectableOption { Label = c });
        }

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
            return;
        }

        var partnerId = combo.CombinedRoomIds.First(id => id != SelectedRoom!.Id);
        ConjoinPartnerName = Rooms.FirstOrDefault(r => r.Id == partnerId)?.Name
                             ?? SeedData.Rooms.FirstOrDefault(r => r.Id == partnerId)?.Name;
        ConjoinCapacity = combo.Capacity;
        OnPropertyChanged(nameof(BookingRoomLabel));
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

        if (UnavailableStartSlots.Contains(StartTime))
        {
            var next = FirstAvailableStart();
            if (next.HasValue)
                StartTime = next.Value;
        }

        if (EndTime <= StartTime || UnavailableEndSlots.Contains(EndTime))
            EndTime = StartTime.Add(TimeSpan.FromHours(1));

        NotifyUnavailableSlots();
    }

    private TimeSpan? FirstAvailableStart()
    {
        for (var t = new TimeSpan(7, 0, 0); t <= new TimeSpan(18, 0, 0); t = t.Add(TimeSpan.FromMinutes(30)))
        {
            if (!UnavailableStartSlots.Contains(t))
                return t;
        }

        return null;
    }

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
        if (OccupiedSlots.Any(slot => StartTime < slot.End && EndTime > slot.Start))
        {
            ErrorMessage = "That time overlaps an existing booking. Choose a free slot.";
            return;
        }

        ErrorMessage = null;
        Step = 4;
    }

    [RelayCommand]
    private void Back()
    {
        if (Step > 1) Step--;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedRoom is null)
        {
            ErrorMessage = "Select a boardroom first.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        SuggestedSlotsText = string.Empty;
        try
        {
            var start = SelectedDate.Date + StartTime;
            var end = SelectedDate.Date + EndTime;
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
                ErrorMessage = result.Message;
                if (result.SuggestedSlots.Count > 0)
                {
                    SuggestedSlotsText = "Suggested: " + string.Join(", ",
                        result.SuggestedSlots.Select(s => s.ToString("HH:mm")));
                }
                return;
            }

            SuccessMessage = result.Message;
            Step = 5;
            if (result.Booking is not null)
                nav.NavigateTo($"/bookings/{result.Booking.Id}/confirmation");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

//Checkbox option used in the booking wizard for equipment and catering add-ons.
public partial class SelectableOption : ObservableObject
{
    public string Label { get; set; } = string.Empty;
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

    public ObservableCollection<AvailabilityItem> Items { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        CanBook = user is not null && RolePermissions.CanBookRooms(user.Role);
        CanViewAvailability = user is not null && RolePermissions.CanViewAvailability(user.Role);
        LockLocationFilter = user?.Role == UserRole.CentreManager && !string.IsNullOrEmpty(user.LocationId);

        if (!CanViewAvailability)
        {
            AccessMessage = "Live availability is for Staff, Centre Managers, and Administrators.";
            Items.Clear();
            IsEmpty = true;
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
