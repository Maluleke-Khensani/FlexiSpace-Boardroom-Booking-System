using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

//Manage console for Centre Managers and Administrators: in-scope bookings and room blocks.
public partial class ManageViewModel(IAuthService auth, IBookingService bookings, INavigationService nav, IRoomService rooms, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private string title = "Manage";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private bool isCentreManager;
    [ObservableProperty] private bool isAdministrator;
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool canBlockRooms;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? errorAnchor;
    [ObservableProperty] private string filterQuery = string.Empty;
    [ObservableProperty] private string locationFilterId = string.Empty;
    [ObservableProperty] private string roomFilterId = string.Empty;
    [ObservableProperty] private bool lockLocationFilter;
    [ObservableProperty] private string blockRoomId = string.Empty;
    [ObservableProperty] private DateTime blockStartDate = DateTime.Today;
    [ObservableProperty] private TimeSpan blockStartTime = new(8, 0, 0);
    [ObservableProperty] private DateTime blockEndDate = DateTime.Today;
    [ObservableProperty] private TimeSpan blockEndTime = new(18, 0, 0);
    [ObservableProperty] private string blockReasonKind = "Cleaning";
    [ObservableProperty] private string blockReasonOther = string.Empty;

    private readonly List<Booking> _allBookings = [];

    public ObservableCollection<Booking> PendingBookings { get; } = [];
    public ObservableCollection<Booking> LocationBookings { get; } = [];
    public ObservableCollection<OfficeLocation> FilterLocations { get; } = [];
    public ObservableCollection<Boardroom> FilterRooms { get; } = [];
    public ObservableCollection<Boardroom> BlockableRooms { get; } = [];
    public ObservableCollection<BlockedPeriod> BlockedPeriods { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && !auth.IsGuest && RolePermissions.CanOpenManage(user.Role);
        if (!HasAccess)
        {
            Title = "Manage";
            Subtitle = "Sign in with a Flexispace account to manage your bookings.";
            IsCentreManager = IsAdministrator = CanBlockRooms = false;
            return;
        }

        IsCentreManager = user!.Role == UserRole.CentreManager;
        IsAdministrator = user.Role == UserRole.Administrator;
        CanBlockRooms = RolePermissions.CanBlockRooms(user.Role);
        Title = IsAdministrator
            ? "Admin console"
            : IsCentreManager
                ? "Centre management"
                : "My bookings";
        var locationName = user.LocationId is null
            ? null
            : (await rooms.GetLocationAsync(user.LocationId))?.Name;
        Subtitle = IsAdministrator
            ? "Review bookings, search the diary, and block rooms across every location."
            : IsCentreManager
                ? $"Review bookings, search the diary, and block rooms for {locationName ?? "your centre"}."
                : "View, edit, or cancel the rooms you have booked.";

        IsBusy = true;
        Message = null;
        ErrorMessage = null;
        try
        {
            _allBookings.Clear();
            PendingBookings.Clear();
            foreach (var b in await bookings.GetBookingsForCurrentUserScopeAsync())
            {
                _allBookings.Add(b);
                if (b.Status == BookingStatus.Pending)
                    PendingBookings.Add(b);
            }

            FilterLocations.Clear();
            LockLocationFilter = IsCentreManager && !string.IsNullOrEmpty(user.LocationId);
            if (CanBlockRooms)
            {
                if (!LockLocationFilter)
                    FilterLocations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });

                foreach (var loc in await rooms.GetLocationsAsync())
                {
                    if (LockLocationFilter && loc.Id != user.LocationId) continue;
                    FilterLocations.Add(loc);
                }

                if (LockLocationFilter)
                    LocationFilterId = user.LocationId!;
            }
            else
            {
                FilterLocations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });
                foreach (var loc in _allBookings
                    .GroupBy(b => b.LocationId)
                    .Select(g => new OfficeLocation { Id = g.Key, Name = g.First().LocationName })
                    .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                {
                    FilterLocations.Add(loc);
                }
            }

            if (CanBlockRooms)
                await LoadBlocksAsync();
            RebuildRoomFilter();
            ApplyBookingFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFilterQueryChanged(string value) => ApplyBookingFilter();
    partial void OnRoomFilterIdChanged(string value) => ApplyBookingFilter();
    partial void OnLocationFilterIdChanged(string value)
    {
        RebuildRoomFilter();
        ApplyBookingFilter();
    }

    [RelayCommand]
    private void FilterLocation(OfficeLocation? location)
    {
        if (location is null || LockLocationFilter) return;
        LocationFilterId = location.Id;
    }

    [RelayCommand]
    private void FilterRoom(Boardroom? room)
    {
        if (room is null) return;
        RoomFilterId = room.Id;
    }

    private void RebuildRoomFilter()
    {
        FilterRooms.Clear();
        if (string.IsNullOrEmpty(LocationFilterId))
        {
            RoomFilterId = string.Empty;
            return;
        }

        FilterRooms.Add(new Boardroom { Id = string.Empty, Name = "All rooms" });

        IEnumerable<Boardroom> rooms = BlockableRooms.Count > 0
            ? BlockableRooms.Where(r => r.LocationId == LocationFilterId)
            : _allBookings
                .Where(b => b.LocationId == LocationFilterId)
                .GroupBy(b => b.RoomId)
                .Select(g => new Boardroom
                {
                    Id = g.Key,
                    Name = g.First().RoomName,
                    LocationId = g.First().LocationId
                });

        foreach (var room in rooms.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            FilterRooms.Add(room);

        if (!string.IsNullOrEmpty(RoomFilterId) && FilterRooms.All(r => r.Id != RoomFilterId))
            RoomFilterId = string.Empty;
    }

    private void ApplyBookingFilter()
    {
        LocationBookings.Clear();
        foreach (var booking in BookingListQuery.Apply(_allBookings, FilterQuery, LocationFilterId, RoomFilterId))
            LocationBookings.Add(booking);
    }

    private async Task LoadBlocksAsync()
    {
        BlockableRooms.Clear();
        BlockedPeriods.Clear();

        var user = auth.CurrentUser;
        var locationId = user?.Role == UserRole.CentreManager ? user.LocationId : null;
        foreach (var room in await rooms.GetRoomsAsync(locationId))
        {
            if (!room.IsCombined)
                BlockableRooms.Add(room);
        }

        if (string.IsNullOrEmpty(BlockRoomId) || BlockableRooms.All(r => r.Id != BlockRoomId))
            BlockRoomId = BlockableRooms.FirstOrDefault()?.Id ?? string.Empty;

        foreach (var block in await bookings.GetBlockedPeriodsAsync())
            BlockedPeriods.Add(block);

        RebuildRoomFilter();
    }

    [RelayCommand]
    private async Task BlockRoomAsync()
    {
        if (!CanBlockRooms) return;
        ErrorMessage = null;
        ErrorAnchor = null;
        Message = null;

        if (string.IsNullOrEmpty(BlockRoomId))
        {
            ErrorMessage = "Choose a room to block.";
            ErrorAnchor = "fs-error-banner";
            return;
        }

        var start = BlockStartDate.Date + BlockStartTime;
        var end = BlockEndDate.Date + BlockEndTime;
        if (end <= start)
        {
            ErrorMessage = "The block must end after it starts.";
            ErrorAnchor = "fs-error-banner";
            return;
        }

        var reason = BlockReasonKind == "Other" ? BlockReasonOther.Trim() : BlockReasonKind;
        if (BlockReasonKind == "Other" && string.IsNullOrWhiteSpace(BlockReasonOther))
        {
            ErrorMessage = "Describe why the room is unavailable.";
            ErrorAnchor = "block-reason";
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            ErrorMessage = "Choose a reason so guests understand why the room is unavailable.";
            ErrorAnchor = "block-reason";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await bookings.BlockRoomAsync(BlockRoomId, start, end, reason);
            if (!result.Success)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "The room could not be blocked. Check the times and try again."
                    : result.Message;
                ErrorAnchor = "fs-error-banner";
                return;
            }

            Message = string.IsNullOrWhiteSpace(result.Message)
                ? "The room is now blocked for that period. Anyone with an overlapping booking has been notified."
                : result.Message;
            var room = BlockableRooms.FirstOrDefault(r => r.Id == BlockRoomId);
            await notifications.AddAsync(new AppNotification
            {
                Title = "Room blocked",
                Message = $"{room?.Name ?? "The room"} is unavailable {start:g}–{end:t}. {reason}",
                Type = "Blocked"
            });
            BlockReasonOther = string.Empty;
            BlockReasonKind = "Cleaning";
            await LoadBlocksAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveBlockAsync(BlockedPeriod? block)
    {
        if (block is null || !CanBlockRooms) return;
        var ok = await bookings.RemoveBlockedPeriodAsync(block.Id);
        Message = ok ? "The room is available again for that period." : "Could not remove this block.";
        await LoadBlocksAsync();
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

    public bool CanEditBooking(Booking booking)
    {
        var user = auth.CurrentUser;
        if (user is null || booking.Status == BookingStatus.Cancelled)
            return false;
        return booking.BookerId == user.Id || RolePermissions.CanEditBookings(user.Role);
    }

    public bool CanCancelBooking(Booking booking)
    {
        var user = auth.CurrentUser;
        if (user is null || booking.Status is not (BookingStatus.Confirmed or BookingStatus.Pending))
            return false;
        return (booking.BookerId == user.Id && RolePermissions.CanCancelOwnBookings(user.Role))
               || RolePermissions.CanCancelAnyBooking(user.Role);
    }

    [RelayCommand]
    private void EditBooking(Booking? booking)
    {
        if (booking is null || !CanEditBooking(booking)) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }

    [RelayCommand]
    private async Task CancelBookingAsync(Booking? booking)
    {
        if (booking is null || !CanCancelBooking(booking)) return;
        var result = await bookings.CancelBookingAsync(booking.Id);
        if (!result.Success)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Could not cancel this booking."
                : result.Message;
            ErrorAnchor = "fs-error-banner";
            return;
        }

        try
        {
            await notifications.AddAsync(new AppNotification
            {
                Title = "Booking cancelled",
                Message = $"{booking.BookerName}'s booking for {booking.RoomName} on {booking.Start:g} has been cancelled.",
                Type = "Cancellation",
                BookingId = booking.Id
            });
        }
        catch
        {
        }
        Message = "Booking cancelled.";
        await AppearingAsync();
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
    [ObservableProperty] private string formRoleLabel = string.Empty;
    [ObservableProperty] private string formLocationId = string.Empty;
    [ObservableProperty] private UserRole formRole = UserRole.Staff;
    [ObservableProperty] private bool formIsActive = true;
    [ObservableProperty] private string filterRole = string.Empty;
    [ObservableProperty] private string filterLocationId = string.Empty;
    [ObservableProperty] private string filterQuery = string.Empty;
    [ObservableProperty] private string? errorAnchor;
    [ObservableProperty] private bool isProvisionedTab = true;
    [ObservableProperty] private int provisionedTotalCount;
    [ObservableProperty] private int unprovisionedTotalCount;

    private int _editingApiId;
    private Guid _editingId;
    private Guid _editingEntraObjectId;
    private readonly List<UserDirectoryRow> _provisionedAll = [];
    private readonly List<UserDirectoryRow> _unprovisionedAll = [];

    public ObservableCollection<UserDirectoryRow> ProvisionedUsers { get; } = [];
    public ObservableCollection<UserDirectoryRow> UnprovisionedUsers { get; } = [];
    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public UserRole[] Roles { get; } = Enum.GetValues<UserRole>();
    public bool IsUnprovisionedTab => !IsProvisionedTab;

    partial void OnIsProvisionedTabChanged(bool value) => OnPropertyChanged(nameof(IsUnprovisionedTab));

    [RelayCommand]
    private void ShowProvisionedTab() => IsProvisionedTab = true;

    [RelayCommand]
    private void ShowUnprovisionedTab() => IsProvisionedTab = false;

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
            _provisionedAll.Clear();
            _unprovisionedAll.Clear();
            ProvisionedTotalCount = 0;
            UnprovisionedTotalCount = 0;
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
            string? entraLoadError = null;
            try
            {
                entraUsers = await admin.GetEntraDirectoryAsync();
            }
            catch (Exception ex)
            {
                entraUsers = [];
                entraLoadError = ex.Message;
            }

            _provisionedAll.Clear();
            _unprovisionedAll.Clear();

            foreach (var account in flexiUsers)
                _provisionedAll.Add(ToRow(account, locationNames, canManage: true, source: "FlexiSpace"));

            var provisionedEmails = flexiUsers
                .Select(u => u.Email)
                .Select(NormalizeEmail)
                .Where(email => email.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var provisionedOids = flexiUsers
                .Select(u => u.EntraObjectId)
                .Where(oid => oid != Guid.Empty)
                .ToHashSet();
            var seenEntraOids = new HashSet<Guid>();
            var seenEntraEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var account in entraUsers)
            {
                if (provisionedOids.Contains(account.EntraObjectId))
                    continue;
                if (account.EntraObjectId != Guid.Empty && !seenEntraOids.Add(account.EntraObjectId))
                    continue;
                var normalizedEmail = NormalizeEmail(account.Email);
                if (normalizedEmail.Length > 0 && (provisionedEmails.Contains(normalizedEmail) || !seenEntraEmails.Add(normalizedEmail)))
                    continue;

                _unprovisionedAll.Add(ToRow(account, locationNames, canManage: false, source: "Microsoft Entra"));
            }

            ApplyFilters();
            ProvisionedTotalCount = _provisionedAll.Count;
            UnprovisionedTotalCount = _unprovisionedAll.Count;
            IsEmpty = _provisionedAll.Count == 0 && _unprovisionedAll.Count == 0;
            ErrorMessage = entraLoadError is null
                ? null
                : $"Could not load Microsoft Entra users. {entraLoadError}";
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
        IsProvisionedTab = true;
        IsFormOpen = true;
        _editingApiId = row.ApiId;
        _editingId = row.Id;
        _editingEntraObjectId = row.EntraObjectId;
        FormFirstName = row.FirstName;
        FormLastName = row.LastName;
        FormEmail = row.Email;
        FormRole = row.Role;
        FormRoleLabel = row.RoleLabel;
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
        IsProvisionedTab = false;
        IsFormOpen = true;
        _editingApiId = 0;
        _editingId = row.Id;
        _editingEntraObjectId = row.EntraObjectId;
        FormFirstName = row.FirstName;
        FormLastName = row.LastName;
        FormEmail = row.Email;
        FormRole = row.Role == default ? UserRole.Staff : row.Role;
        FormRoleLabel = row.RoleLabel;
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
        ErrorAnchor = null;
        Message = null;

        if (IsProvisioning && string.IsNullOrWhiteSpace(FormFirstName))
        {
            ErrorMessage = "First name is required.";
            ErrorAnchor = "user-first";
            return;
        }

        if (IsProvisioning && string.IsNullOrWhiteSpace(FormLastName))
        {
            ErrorMessage = "Last name is required.";
            ErrorAnchor = "user-last";
            return;
        }

        if (IsProvisioning && (string.IsNullOrWhiteSpace(FormEmail) || !FormEmail.Contains('@')))
        {
            ErrorMessage = "Enter a valid email address.";
            ErrorAnchor = "user-email";
            return;
        }

        if (IsProvisioning && string.IsNullOrWhiteSpace(FormLocationId))
        {
            ErrorMessage = "Choose a location before provisioning this user.";
            ErrorAnchor = "fs-error-banner";
            return;
        }

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
            ErrorAnchor = "fs-error-banner";
            return;
        }

        Message = result.Message;
        IsFormOpen = false;
        IsProvisioning = false;
        IsProvisionedTab = true;
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
            ErrorAnchor = "fs-error-banner";
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
    partial void OnFilterQueryChanged(string value) => ApplyFilters();

    public async Task DeleteUserAsync(UserDirectoryRow? row)
    {
        if (row is null || !HasAccess || !row.CanManage) return;
        if (row.Id == auth.CurrentUser?.Id || row.ApiId == auth.CurrentUser?.ApiId)
        {
            ErrorMessage = "You cannot remove your own account.";
            ErrorAnchor = "fs-error-banner";
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
                ErrorAnchor = "fs-error-banner";
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
            ErrorAnchor = "fs-error-banner";
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
        if (string.IsNullOrWhiteSpace(FilterQuery))
            return true;

        var query = FilterQuery.Trim().TrimStart('#');
        return MatchesUserId(row, query) ||
               row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               row.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               row.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               row.Email.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string? email) => email?.Trim() ?? string.Empty;

    private static bool MatchesUserId(UserDirectoryRow row, string query)
    {
        if (row.ApiId <= 0)
            return false;

        return row.ApiId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               int.TryParse(query, out var apiId) && row.ApiId == apiId;
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
            RoleLabel = account.HasAssignedFlexiSpaceRole
                ? RolePermissions.DisplayName(account.Role)
                : "No FlexiSpace app role assigned in Entra",
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
        {
            notification.IsRead = false;
            await notifications.MarkAsUnreadAsync(notification.Id);
        }
        else
        {
            notification.IsRead = true;
            await notifications.MarkAsReadAsync(notification.Id);
        }

        var index = Items.IndexOf(notification);
        if (index >= 0)
            Items[index] = notification;
        OnPropertyChanged(nameof(Items));
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
        CanAccessManage = user is not null && !auth.IsGuest && RolePermissions.CanOpenManage(user.Role);
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
    private Task LogoutAsync() => auth.LogoutAsync();

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
