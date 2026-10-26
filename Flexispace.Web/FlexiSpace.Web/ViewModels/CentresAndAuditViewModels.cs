using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.ViewModels;

public partial class CentresViewModel(IAuthService auth, IAdminService admin, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? errorAnchor;
    [ObservableProperty] private bool isLocationFormOpen;
    [ObservableProperty] private bool isRoomFormOpen;
    [ObservableProperty] private bool isEditingLocation;
    [ObservableProperty] private bool isEditingRoom;
    [ObservableProperty] private string locationName = string.Empty;
    [ObservableProperty] private string locationAddress = string.Empty;
    [ObservableProperty] private string roomName = string.Empty;
    [ObservableProperty] private string roomLocationId = string.Empty;
    [ObservableProperty] private string roomCapacityText = "8";

    private string _editingLocationId = string.Empty;
    private string _editingRoomId = string.Empty;
    private RoomStatus _editingRoomStatus = RoomStatus.Available;

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<SelectableOption> EquipmentOptions { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageRooms(user.Role);
        if (!HasAccess)
        {
            Subtitle = "Only Administrators can add or remove locations and boardrooms.";
            return;
        }

        Subtitle = "Add future Flexispace centres and the boardrooms in each one.";
        IsBusy = true;
        try
        {
            Locations.Clear();
            foreach (var loc in await rooms.GetLocationsAsync())
                Locations.Add(loc);

            Rooms.Clear();
            foreach (var room in await rooms.GetRoomsAsync())
                Rooms.Add(room);

            if (EquipmentOptions.Count == 0)
            {
                foreach (var item in await rooms.GetEquipmentCatalogAsync())
                    EquipmentOptions.Add(new SelectableOption { Label = item });
            }

            if (string.IsNullOrEmpty(RoomLocationId) && Locations.Count > 0)
                RoomLocationId = Locations[0].Id;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StartAddLocation()
    {
        IsLocationFormOpen = true;
        IsRoomFormOpen = false;
        IsEditingLocation = false;
        IsEditingRoom = false;
        _editingLocationId = string.Empty;
        LocationName = string.Empty;
        LocationAddress = string.Empty;
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void StartEditLocation(OfficeLocation? location)
    {
        if (location is null) return;
        IsLocationFormOpen = true;
        IsRoomFormOpen = false;
        IsEditingLocation = true;
        IsEditingRoom = false;
        _editingLocationId = location.Id;
        LocationName = location.Name;
        LocationAddress = location.Address;
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void StartAddRoom()
    {
        IsRoomFormOpen = true;
        IsLocationFormOpen = false;
        IsEditingRoom = false;
        _editingRoomId = string.Empty;
        _editingRoomStatus = RoomStatus.Available;
        RoomName = string.Empty;
        RoomCapacityText = "8";
        if (string.IsNullOrEmpty(RoomLocationId) && Locations.Count > 0)
            RoomLocationId = Locations[0].Id;
        foreach (var option in EquipmentOptions)
            option.IsSelected = false;
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void StartEditRoom(Boardroom? room)
    {
        if (room is null) return;
        IsRoomFormOpen = true;
        IsLocationFormOpen = false;
        IsEditingRoom = true;
        _editingRoomId = room.Id;
        _editingRoomStatus = room.Status;
        RoomName = room.Name;
        RoomLocationId = room.LocationId;
        RoomCapacityText = room.Capacity.ToString();
        foreach (var option in EquipmentOptions)
            option.IsSelected = room.Equipment.Contains(option.Label, StringComparer.OrdinalIgnoreCase);
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void CloseForms()
    {
        IsLocationFormOpen = false;
        IsRoomFormOpen = false;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveLocationAsync()
    {
        if (!HasAccess) return;
        if (string.IsNullOrWhiteSpace(LocationName))
        {
            ErrorMessage = "Enter a location name.";
            ErrorAnchor = "centre-name";
            return;
        }

        var draft = new OfficeLocation
        {
            Id = _editingLocationId,
            Name = LocationName.Trim(),
            Address = LocationAddress.Trim()
        };

        var result = IsEditingLocation
            ? await admin.UpdateLocationAsync(draft)
            : await admin.CreateLocationAsync(draft);

        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            ErrorAnchor = "fs-error-banner";
            return;
        }

        Message = result.Message;
        IsLocationFormOpen = false;
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeleteLocationAsync(OfficeLocation? location)
    {
        if (location is null || !HasAccess) return;
        ErrorMessage = null;
        Message = null;
        IsBusy = true;
        try
        {
            var result = await admin.DeleteLocationAsync(location.Id);
            if (!result.Ok)
            {
                ErrorMessage = result.Message;
                ErrorAnchor = "fs-error-banner";
                return;
            }

            Message = result.Message;
            await AppearingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveRoomAsync()
    {
        if (!HasAccess) return;
        if (string.IsNullOrWhiteSpace(RoomName))
        {
            ErrorMessage = "Enter a boardroom name.";
            ErrorAnchor = "room-name";
            return;
        }

        if (string.IsNullOrWhiteSpace(RoomLocationId))
        {
            ErrorMessage = "Choose a location for this boardroom.";
            ErrorAnchor = "room-location";
            return;
        }

        if (!int.TryParse(RoomCapacityText, out var capacity) || capacity < 1)
        {
            ErrorMessage = "Capacity must be at least 1.";
            ErrorAnchor = "room-capacity";
            return;
        }

        var draft = new Boardroom
        {
            Id = _editingRoomId,
            Name = RoomName.Trim(),
            LocationId = RoomLocationId,
            Capacity = capacity,
            Status = _editingRoomStatus,
            Equipment = EquipmentOptions.Where(o => o.IsSelected).Select(o => o.Label).ToList()
        };

        var result = IsEditingRoom
            ? await admin.UpdateRoomAsync(draft)
            : await admin.AddRoomAsync(draft);

        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            ErrorAnchor = "fs-error-banner";
            return;
        }

        Message = result.Message;
        IsRoomFormOpen = false;
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeleteRoomAsync(Boardroom? room)
    {
        if (room is null || !HasAccess) return;
        var result = await admin.RemoveRoomAsync(room.Id);
        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            ErrorAnchor = "fs-error-banner";
            return;
        }

        Message = result.Message;
        await AppearingAsync();
    }

    public IEnumerable<Boardroom> RoomsFor(OfficeLocation location) =>
        Rooms.Where(r => r.LocationId == location.Id);
}

public partial class AuditViewModel(IAuthService auth, IAdminService admin) : ObservableObject
{
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string filter = "All";

    private readonly List<AuditLogEntry> _all = [];
    public ObservableCollection<AuditLogEntry> Items { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageUsers(user.Role);
        if (!HasAccess)
        {
            Subtitle = "Audit history is available to Administrators.";
            return;
        }

        Subtitle = "A record of bookings, Admin changes, deletions, and sign-in activity.";
        IsBusy = true;
        try
        {
            _all.Clear();
            foreach (var entry in await admin.GetRecentAuditAsync())
                _all.Add(entry);
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string? value)
    {
        Filter = string.IsNullOrWhiteSpace(value) ? "All" : value;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<AuditLogEntry> query = _all;
        query = Filter switch
        {
            "Bookings" => _all.Where(e => e.EntityName.Contains("Booking", StringComparison.OrdinalIgnoreCase)
                                          || e.Action.Contains("Booking", StringComparison.OrdinalIgnoreCase)),
            "Logins" => _all.Where(e => e.Action.Contains("Login", StringComparison.OrdinalIgnoreCase)
                                        || e.Action.Contains("Logout", StringComparison.OrdinalIgnoreCase)
                                        || e.EntityName.Contains("Session", StringComparison.OrdinalIgnoreCase)),
            "Admin changes" => _all.Where(e => !e.EntityName.Contains("Booking", StringComparison.OrdinalIgnoreCase)
                                                && !e.EntityName.Contains("Session", StringComparison.OrdinalIgnoreCase)),
            "Deletes" => _all.Where(e => e.Action.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                                         || e.Action.Contains("Removed", StringComparison.OrdinalIgnoreCase)
                                         || e.Action.Contains("Cancel", StringComparison.OrdinalIgnoreCase)),
            _ => _all
        };

        Items.Clear();
        foreach (var entry in query)
            Items.Add(entry);
        IsEmpty = Items.Count == 0;
    }
}
