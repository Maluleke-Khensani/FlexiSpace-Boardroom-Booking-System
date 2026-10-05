using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;
using Microsoft.Maui.Graphics;

namespace Flexispace.Mobile.ViewModels;

public partial class AvailabilityItem : ObservableObject
{
    public Boardroom Room { get; init; } = null!;
    public string LocationName { get; init; } = string.Empty;
    public string StatusText => Room.Status.ToString();
    public Color StatusColor => RoomStatusPalette.GetColor(StatusText);
}

public partial class AvailabilityViewModel(IAuthService auth, IRoomService rooms) : ObservableObject
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
        // Room service already returns only the CM's centre; Staff/Admin still get every site.
        if (!LockLocationFilter)
            Locations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });

        foreach (var loc in await rooms.GetLocationsAsync())
            Locations.Add(loc);

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
            OccupiedCount = Items.Count(i => i.Room.Status is RoomStatus.Occupied or RoomStatus.Blocked or RoomStatus.Maintenance or RoomStatus.Cleaning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BookRoomAsync(AvailabilityItem? item)
    {
        if (item is null || !CanBook) return;
        await Shell.Current.GoToAsync($"//BookingPage?roomId={item.Room.Id}&locationId={item.Room.LocationId}");
    }

    /// <summary>Tapping a room's tile on the directory map — jumps straight into booking it
    /// when the user is allowed to, otherwise shows a quick look at its details instead.</summary>
    [RelayCommand]
    private async Task OpenRoomAsync(AvailabilityItem? item)
    {
        if (item is null) return;

        if (CanBook)
        {
            await Shell.Current.GoToAsync($"//BookingPage?roomId={item.Room.Id}&locationId={item.Room.LocationId}");
            return;
        }

        if (Shell.Current is null) return;
        var equipment = item.Room.Equipment.Count > 0 ? string.Join(", ", item.Room.Equipment) : "—";
        await Shell.Current.DisplayAlertAsync(
            item.Room.Name,
            $"{item.LocationName} · Seats {item.Room.Capacity}\nStatus: {item.StatusText}\nEquipment: {equipment}",
            "OK");
    }
}
