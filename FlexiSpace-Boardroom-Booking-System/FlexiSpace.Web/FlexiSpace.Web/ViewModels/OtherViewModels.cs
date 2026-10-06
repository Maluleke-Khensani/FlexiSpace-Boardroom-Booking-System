using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Manage console for Centre Managers and Administrators: in-scope bookings.
public partial class ManageViewModel(IAuthService auth, IBookingService bookings, INavigationService nav, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private string title = "Manage";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private bool isCentreManager;
    [ObservableProperty] private bool isAdministrator;
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? message;

    public ObservableCollection<Booking> PendingBookings { get; } = [];
    public ObservableCollection<Booking> LocationBookings { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        if (!HasAccess)
        {
            Title = "Manage";
            Subtitle = "Only Centre Managers and Administrators can open this console.";
            IsCentreManager = IsAdministrator = false;
            return;
        }

        IsCentreManager = user!.Role == UserRole.CentreManager;
        IsAdministrator = user.Role == UserRole.Administrator;
        Title = IsAdministrator ? "Admin console" : "Centre management";
        var locationName = user.LocationId is null
            ? null
            : (await rooms.GetLocationAsync(user.LocationId))?.Name;
        Subtitle = IsAdministrator
            ? "Review bookings across every location."
            : $"Review and manage bookings for {locationName ?? "your centre"}.";

        IsBusy = true;
        Message = null;
        try
        {
            LocationBookings.Clear();
            PendingBookings.Clear();
            foreach (var b in await bookings.GetBookingsForCurrentUserScopeAsync())
            {
                LocationBookings.Add(b);
                if (b.Status == BookingStatus.Pending)
                    PendingBookings.Add(b);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApproveAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.ApproveBookingAsync(booking.Id);
        Message = ok ? "Booking approved." : "Could not approve (check permissions / status).";
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeclineAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.DeclineBookingAsync(booking.Id);
        Message = ok ? "Booking declined." : "Could not decline booking.";
        await AppearingAsync();
    }

    [RelayCommand]
    private void OpenBooking(Booking? booking)
    {
        if (booking is null) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }
}

//Administrator directory of FlexiSpace users, split into provisioned and unprovisioned.
public partial class UsersViewModel(IAuthService auth, IAdminService admin, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isFormOpen;
    [ObservableProperty] private bool isProvisioning;
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string formFirstName = string.Empty;
    [ObservableProperty] private string formLastName = string.Empty;
    [ObservableProperty] private string formEmail = string.Empty;
    [ObservableProperty] private string formLocationId = string.Empty;
    [ObservableProperty] private UserRole formRole = UserRole.Staff;
    [ObservableProperty] private bool formIsActive = true;
    [ObservableProperty] private string filterRole = string.Empty;
    [ObservableProperty] private string filterLocationId = string.Empty;

    private int _editingApiId;
    private Guid _editingId;
    private Guid _editingEntraObjectId;
    private readonly List<UserDirectoryRow> _provisionedAll = [];
    private readonly List<UserDirectoryRow> _unprovisionedAll = [];

    public ObservableCollection<UserDirectoryRow> ProvisionedUsers { get; } = [];
    public ObservableCollection<UserDirectoryRow> UnprovisionedUsers { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public UserRole[] Roles { get; } = Enum.GetValues<UserRole>();

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageUsers(user.Role);
        if (!HasAccess)
        {
            Subtitle = "Only Administrators can open user control.";
            ProvisionedUsers.Clear();
            UnprovisionedUsers.Clear();
            IsEmpty = true;
            return;
        }

        Subtitle = "Provisioned users already have a FlexiSpace account. Unprovisioned identities are in Microsoft Entra but not yet in FlexiSpace.";
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Locations.Clear();
            foreach (var location in await rooms.GetLocationsAsync())
                Locations.Add(location);

            var locationNames = Locations.ToDictionary(l => l.Id, l => l.Name, StringComparer.OrdinalIgnoreCase);
            var flexiUsers = await admin.GetUsersAsync();
            IReadOnlyList<User> entraUsers = [];
            try
            {
                entraUsers = await admin.GetEntraDirectoryAsync();
            }
            catch
            {
                entraUsers = [];
            }

            _provisionedAll.Clear();
            _unprovisionedAll.Clear();

            foreach (var account in flexiUsers)
                _provisionedAll.Add(ToRow(account, locationNames, canManage: true, source: "FlexiSpace"));

            var provisionedEmails = flexiUsers
                .Select(u => u.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var provisionedOids = flexiUsers
                .Select(u => u.EntraObjectId)
                .Where(oid => oid != Guid.Empty)
                .ToHashSet();

            foreach (var account in entraUsers)
            {
                if (provisionedOids.Contains(account.EntraObjectId))
                    continue;
                if (!string.IsNullOrWhiteSpace(account.Email) && provisionedEmails.Contains(account.Email))
                    continue;

                _unprovisionedAll.Add(ToRow(account, locationNames, canManage: false, source: "Microsoft Entra"));
            }

            ApplyFilters();
            IsEmpty = _provisionedAll.Count == 0 && _unprovisionedAll.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StartEdit(UserDirectoryRow? row)
    {
        if (row is null || !row.CanManage) return;
        IsProvisioning = false;
        IsFormOpen = true;
        _editingApiId = row.ApiId;
        _editingId = row.Id;
        _editingEntraObjectId = row.EntraObjectId;
        FormFirstName = row.FirstName;
        FormLastName = row.LastName;
        FormEmail = row.Email;
        FormRole = row.Role;
        FormLocationId = row.LocationId;
        FormIsActive = row.IsActive;
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void StartProvision(UserDirectoryRow? row)
    {
        if (row is null || row.EntraObjectId == Guid.Empty) return;
        IsProvisioning = true;
        IsFormOpen = true;
        _editingApiId = 0;
        _editingId = row.Id;
        _editingEntraObjectId = row.EntraObjectId;
        FormFirstName = row.FirstName;
        FormLastName = row.LastName;
        FormEmail = row.Email;
        FormRole = row.Role == default ? UserRole.Staff : row.Role;
        FormLocationId = row.LocationId;
        FormIsActive = true;
        ErrorMessage = null;
        Message = null;
    }

    [RelayCommand]
    private void CancelForm()
    {
        IsFormOpen = false;
        IsProvisioning = false;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!HasAccess) return;
        ErrorMessage = null;
        Message = null;

        var draft = new User
        {
            Id = _editingId,
            ApiId = _editingApiId,
            EntraObjectId = _editingEntraObjectId,
            FirstName = FormFirstName.Trim(),
            LastName = FormLastName.Trim(),
            Name = $"{FormFirstName.Trim()} {FormLastName.Trim()}".Trim(),
            Email = FormEmail.Trim(),
            Role = FormRole,
            LocationId = string.IsNullOrWhiteSpace(FormLocationId) ? null : FormLocationId
        };

        var result = IsProvisioning
            ? await admin.ProvisionUserAsync(draft)
            : await admin.UpdateUserAsync(draft);

        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            return;
        }

        Message = result.Message;
        IsFormOpen = false;
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (!HasAccess || IsProvisioning || _editingApiId <= 0) return;
        ErrorMessage = null;
        Message = null;

        var result = await admin.SetUserActiveAsync(_editingApiId, true);
        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            return;
        }

        FormIsActive = true;
        Message = result.Message;
        var apiId = _editingApiId;
        await AppearingAsync();
        var row = _provisionedAll.FirstOrDefault(u => u.ApiId == apiId);
        if (row is not null)
            StartEdit(row);
        Message = result.Message;
    }

    partial void OnFilterRoleChanged(string value) => ApplyFilters();
    partial void OnFilterLocationIdChanged(string value) => ApplyFilters();

    public async Task DeleteUserAsync(UserDirectoryRow? row)
    {
        if (row is null || !HasAccess || !row.CanManage) return;
        if (row.Id == auth.CurrentUser?.Id || row.ApiId == auth.CurrentUser?.ApiId)
        {
            ErrorMessage = "You cannot remove your own account.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        Message = null;
        try
        {
            var result = await admin.RemoveUserAsync(row.ApiId);
            if (!result.Ok)
            {
                ErrorMessage = result.Message;
                return;
            }

            Message = result.Message;
            if (IsFormOpen && _editingApiId == row.ApiId)
                IsFormOpen = false;
            await AppearingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove this user. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        ProvisionedUsers.Clear();
        UnprovisionedUsers.Clear();
        foreach (var row in _provisionedAll.Where(MatchesFilter))
            ProvisionedUsers.Add(row);
        foreach (var row in _unprovisionedAll.Where(MatchesFilter))
            UnprovisionedUsers.Add(row);
    }

    private bool MatchesFilter(UserDirectoryRow row)
    {
        if (!string.IsNullOrEmpty(FilterRole) && row.Role.ToString() != FilterRole)
            return false;
        if (!string.IsNullOrEmpty(FilterLocationId) && row.LocationId != FilterLocationId)
            return false;
        return true;
    }

    private static UserDirectoryRow ToRow(
        User account,
        IReadOnlyDictionary<string, string> locationNames,
        bool canManage,
        string source)
    {
        var split = SplitName(account.Name);
        return new UserDirectoryRow
        {
            ApiId = account.ApiId,
            Id = account.Id,
            FirstName = string.IsNullOrWhiteSpace(account.FirstName) ? split.First : account.FirstName,
            LastName = string.IsNullOrWhiteSpace(account.LastName) ? split.Last : account.LastName,
            Name = string.IsNullOrWhiteSpace(account.Name) ? "Unnamed user" : account.Name,
            Email = account.Email,
            Role = account.Role,
            RoleLabel = RolePermissions.DisplayName(account.Role),
            LocationId = account.LocationId ?? string.Empty,
            Location = string.IsNullOrEmpty(account.LocationId)
                ? "All locations"
                : locationNames.GetValueOrDefault(account.LocationId, account.LocationId),
            Status = account.IsActive ? "Active" : "Inactive",
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt == default
                ? "—"
                : account.CreatedAt.ToLocalTime().ToString("g"),
            CanManage = canManage,
            Source = source,
            EntraObjectId = account.EntraObjectId
        };
    }

    private static (string First, string Last) SplitName(string name)
    {
        var parts = (name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts.ElementAtOrDefault(0) ?? string.Empty, parts.ElementAtOrDefault(1) ?? string.Empty);
    }
}

public sealed class UserDirectoryRow
{
    public int ApiId { get; init; }
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public string RoleLabel { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = string.Empty;
    public bool CanManage { get; init; }
    public string Source { get; init; } = string.Empty;
    public Guid EntraObjectId { get; init; }
}

//Alerts inbox: lists notifications and supports read/unread, delete, and booking deep-links.
public partial class NotificationsViewModel(INotificationService notifications, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private string? alertMessage;

    public ObservableCollection<AppNotification> Items { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var n in await notifications.GetNotificationsAsync())
                Items.Add(n);
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await notifications.MarkAllAsReadAsync();
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task OpenNotificationAsync(AppNotification? notification)
    {
        if (notification is null) return;

        if (!notification.IsRead)
        {
            await notifications.MarkAsReadAsync(notification.Id);
            notification.IsRead = true;
            OnPropertyChanged(nameof(Items));
        }

        if (notification.BookingId.HasValue)
            nav.NavigateTo($"/bookings/{notification.BookingId}");
        else
            AlertMessage = $"{notification.Title}\n\n{notification.Message}";
    }

    [RelayCommand]
    private async Task ToggleReadAsync(AppNotification? notification)
    {
        if (notification is null) return;
        if (notification.IsRead)
            await notifications.MarkAsUnreadAsync(notification.Id);
        else
            await notifications.MarkAsReadAsync(notification.Id);
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(AppNotification? notification)
    {
        if (notification is null) return;
        await notifications.DeleteAsync(notification.Id);
        await AppearingAsync();
    }
}

//User profile: current account details, role info, and sign-out.
public partial class ProfileViewModel(IAuthService auth, INavigationService nav, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string roleDescription = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private bool canAccessManage;
    [ObservableProperty] private bool canManageUsers;
    [ObservableProperty] private bool canViewReports;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        Name = user?.Name ?? "Guest";
        Email = user?.Email ?? string.Empty;
        Role = user is null ? "—" : RolePermissions.DisplayName(user.Role);
        RoleDescription = user is null ? string.Empty : RolePermissions.Describe(user.Role);
        CanAccessManage = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        CanManageUsers = user is not null && RolePermissions.CanManageUsers(user.Role);
        CanViewReports = user is not null && RolePermissions.CanViewReports(user.Role);

        if (string.IsNullOrEmpty(user?.LocationId))
        {
            Location = "All locations";
        }
        else
        {
            var loc = await rooms.GetLocationAsync(user.LocationId);
            Location = loc?.Name ?? user.LocationId;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await auth.LogoutAsync();
        nav.NavigateTo("/", forceLoad: true);
    }

    [RelayCommand]
    private void OpenManage()
    {
        if (!CanAccessManage) return;
        nav.NavigateTo("/manage");
    }
}

//Single-location detail: loads a location and its boardrooms; links into the booking wizard.
public partial class LocationDetailViewModel(IAuthService auth, IRoomService rooms, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private string locationId = string.Empty;
    [ObservableProperty] private OfficeLocation? location;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool canBook;

    public ObservableCollection<Boardroom> Rooms { get; } = [];

    public void SetLocationId(string id)
    {
        LocationId = id;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        CanBook = auth.CurrentUser is not null && RolePermissions.CanBookRooms(auth.CurrentUser.Role);
        Location = await rooms.GetLocationAsync(LocationId);
        Rooms.Clear();
        foreach (var room in await rooms.GetRoomsAsync(LocationId))
            Rooms.Add(room);
        IsEmpty = Rooms.Count == 0;
    }

    [RelayCommand]
    private void BookRoom(Boardroom? room)
    {
        if (room is null || !CanBook) return;
        nav.NavigateTo($"/book?roomId={room.Id}&locationId={room.LocationId}");
    }
}

//Location gallery: lists all Flexispace sites and navigates to each location detail page.
public partial class LocationsViewModel(IRoomService rooms, INavigationService nav) : ObservableObject
{
    public ObservableCollection<OfficeLocation> Locations { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        Locations.Clear();
        foreach (var loc in await rooms.GetLocationsAsync())
            Locations.Add(loc);
    }

    [RelayCommand]
    private void Open(OfficeLocation? location)
    {
        if (location is null) return;
        nav.NavigateTo($"/locations/{location.Id}");
    }
}
