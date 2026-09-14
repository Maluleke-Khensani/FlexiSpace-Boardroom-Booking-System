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
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string suggestedSlotsText = string.Empty;
    [ObservableProperty] private bool canBook = true;
    [ObservableProperty] private bool showClientPaymentNote;

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
        ShowClientPaymentNote = user?.Role == UserRole.Client;
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
            foreach (var e in SeedData.EquipmentOptions)
                EquipmentOptions.Add(new SelectableOption { Label = e });
            foreach (var c in SeedData.CateringOptions)
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
    }

    [RelayCommand]
    private async Task SelectLocationAsync(OfficeLocation? location)
    {
        SelectedLocation = location;
        SelectedRoom = null;
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
    private void SelectRoom(Boardroom? room) => SelectedRoom = room;

    [RelayCommand]
    private void ContinueToSchedule()
    {
        if (SelectedRoom is not null)
            Step = 3;
    }

    [RelayCommand]
    private void NextToDetails() => Step = 4;

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
