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

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<SelectableOption> EquipmentOptions { get; } = [];
    public ObservableCollection<SelectableOption> CateringOptions { get; } = [];

    partial void OnPrefillLocationIdChanged(string? value)
    {
        _ = InitPrefillAsync();
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
    private void SelectRoom(Boardroom? room)
    {
        SelectedRoom = room;
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
                await Shell.Current.GoToAsync($"BookingConfirmationPage?bookingId={result.Booking.Id}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class SelectableOption : ObservableObject
{
    public string Label { get; set; } = string.Empty;
    [ObservableProperty] private bool isSelected;
}
