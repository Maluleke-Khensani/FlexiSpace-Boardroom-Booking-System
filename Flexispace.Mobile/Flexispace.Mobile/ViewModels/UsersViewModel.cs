using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class UsersViewModel(IAuthService auth, IAdminService admin, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? message;
    [ObservableProperty] private LocationOption? selectedLocation;
    [ObservableProperty] private RoleFilterOption? selectedRoleFilter;
    [ObservableProperty] private LocationFilterOption? selectedLocationFilter;
    [ObservableProperty] private User? selectedUser;
    [ObservableProperty] private bool isProvisioning;
    [ObservableProperty] private bool isEditingLocation;
    [ObservableProperty] private bool isProvisionedTab = true;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string emptyProvisionedMessage = "No users have been provisioned yet.";
    [ObservableProperty] private string emptyUnprovisionedMessage = "No Entra users are waiting to be provisioned.";
    [ObservableProperty] private int provisionedTotalCount;
    [ObservableProperty] private int unprovisionedTotalCount;

    private readonly List<User> _provisioned = [];
    private readonly List<User> _unprovisioned = [];

    public ObservableCollection<User> ProvisionedUsers { get; } = [];
    public ObservableCollection<User> UnprovisionedUsers { get; } = [];
    public ObservableCollection<LocationOption> Locations { get; } = [];
    public ObservableCollection<RoleFilterOption> RoleFilters { get; } = [];
    public ObservableCollection<LocationFilterOption> LocationFilters { get; } = [];
    public bool HasProvisionedUsers => ProvisionedUsers.Count > 0;
    public bool HasUnprovisionedUsers => UnprovisionedUsers.Count > 0;
    public bool CanProvisionSelected => SelectedUser is { IsActive: true, HasAssignedFlexiSpaceRole: true };
    public bool IsEditingForm => IsProvisioning || IsEditingLocation;
    public bool IsUnprovisionedTab => !IsProvisionedTab;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedRoleFilterChanged(RoleFilterOption? value) => ApplyFilter();
    partial void OnSelectedLocationFilterChanged(LocationFilterOption? value) => ApplyFilter();
    partial void OnSelectedUserChanged(User? value) { OnPropertyChanged(nameof(CanProvisionSelected)); }
    partial void OnIsProvisioningChanged(bool value) => OnPropertyChanged(nameof(IsEditingForm));
    partial void OnIsEditingLocationChanged(bool value) => OnPropertyChanged(nameof(IsEditingForm));
    partial void OnIsProvisionedTabChanged(bool value) => OnPropertyChanged(nameof(IsUnprovisionedTab));

    [RelayCommand]
    private void ShowProvisionedTab() => IsProvisionedTab = true;

    [RelayCommand]
    private void ShowUnprovisionedTab() => IsProvisionedTab = false;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        // OnAppearing and pull-to-refresh can request a reload close together.
        // Do not let overlapping requests mutate the same backing collections.
        if (IsBusy) return;

        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageUsers(user.Role);
        if (!HasAccess)
        {
            Message = "Only Administrators can manage users.";
            ProvisionedUsers.Clear();
            UnprovisionedUsers.Clear();
            _provisioned.Clear();
            _unprovisioned.Clear();
            ProvisionedTotalCount = 0;
            UnprovisionedTotalCount = 0;
            return;
        }

        IsBusy = true;
        Message = null;
        try
        {
            await LoadUsersAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        Locations.Clear();
        foreach (var loc in await rooms.GetLocationsAsync())
            Locations.Add(new LocationOption { Id = loc.Id, Name = loc.Name });

        RoleFilters.Clear();
        RoleFilters.Add(new RoleFilterOption { Name = "All roles" });
        foreach (var role in Enum.GetValues<UserRole>())
            RoleFilters.Add(new RoleFilterOption
            {
                Role = role,
                Name = role == UserRole.CentreManager ? "Centre Manager" : role.ToString()
            });
        SelectedRoleFilter = RoleFilters[0];

        LocationFilters.Clear();
        LocationFilters.Add(new LocationFilterOption { Name = "All locations" });
        foreach (var loc in Locations)
            LocationFilters.Add(new LocationFilterOption { Id = loc.Id, Name = loc.Name });
        SelectedLocationFilter = LocationFilters[0];

        _provisioned.Clear();
        var seenProvisionedIds = new HashSet<Guid>();
        var seenProvisionedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in await admin.GetUsersAsync())
        {
            // The API normally returns one row per FlexiSpace account. Guard the
            // mobile directory against repeated rows as well, matching users by
            // their Entra identity first and their normalized email as fallback.
            if (person.EntraObjectId != Guid.Empty && !seenProvisionedIds.Add(person.EntraObjectId))
                continue;
            var normalizedEmail = NormalizeEmail(person.Email);
            if (normalizedEmail.Length > 0 && !seenProvisionedEmails.Add(normalizedEmail))
                continue;

            person.LocationLabel = string.IsNullOrWhiteSpace(person.LocationId)
                ? "All locations"
                : Locations.FirstOrDefault(l => l.Id == person.LocationId)?.Name ?? person.LocationId;
            _provisioned.Add(person);
        }

        var provisionedIds = _provisioned.Where(u => u.EntraObjectId != Guid.Empty).Select(u => u.EntraObjectId).ToHashSet();
        var provisionedEmails = _provisioned.Select(u => NormalizeEmail(u.Email)).Where(e => e.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenEntraIds = new HashSet<Guid>();
        var seenEntraEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _unprovisioned.Clear();
        IReadOnlyList<User> entraUsers;
        try
        {
            entraUsers = await admin.GetEntraDirectoryAsync();
        }
        catch (Exception ex)
        {
            entraUsers = [];
            Message = $"Could not load Microsoft Entra users. {ex.Message}";
        }

        foreach (var person in entraUsers)
        {
            if (person.EntraObjectId != Guid.Empty && provisionedIds.Contains(person.EntraObjectId)) continue;
            if (person.EntraObjectId != Guid.Empty && !seenEntraIds.Add(person.EntraObjectId)) continue;
            var normalizedEmail = NormalizeEmail(person.Email);
            if (normalizedEmail.Length > 0 && (provisionedEmails.Contains(normalizedEmail) || !seenEntraEmails.Add(normalizedEmail))) continue;
            person.LocationLabel = "Not provisioned";
            _unprovisioned.Add(person);
        }
        ProvisionedTotalCount = _provisioned.Count;
        UnprovisionedTotalCount = _unprovisioned.Count;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        bool Matches(User u)
        {
            if (SelectedRoleFilter?.Role is UserRole role && u.Role != role) return false;
            if (!string.IsNullOrWhiteSpace(SelectedLocationFilter?.Id) &&
                !string.Equals(u.LocationId, SelectedLocationFilter.Id, StringComparison.OrdinalIgnoreCase)) return false;
            return query.Length == 0 || Contains(u.Name, query) || Contains(u.Email, query) ||
                Contains(u.EntraRoleLabel, query) || Contains(u.LocationLabel, query) || Contains(u.ReferenceLabel, query);
        }

        ProvisionedUsers.Clear();
        foreach (var person in _provisioned.Where(Matches).OrderBy(u => u.Name)) ProvisionedUsers.Add(person);
        UnprovisionedUsers.Clear();
        foreach (var person in _unprovisioned.Where(Matches).OrderBy(u => u.Name)) UnprovisionedUsers.Add(person);
        OnPropertyChanged(nameof(HasProvisionedUsers));
        OnPropertyChanged(nameof(HasUnprovisionedUsers));
        EmptyProvisionedMessage = _provisioned.Count == 0 ? "No users have been provisioned yet." : "No provisioned users match this search.";
        EmptyUnprovisionedMessage = _unprovisioned.Count == 0 ? "No Entra users are waiting to be provisioned." : "No unprovisioned users match this search.";
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEmail(string? email) => email?.Trim() ?? string.Empty;

    [RelayCommand]
    private void ProvisionUser(User? user)
    {
        if (user is null || !HasAccess) return;
        SelectedUser = user;
        IsProvisioning = true;
        IsEditingLocation = false;
        IsProvisionedTab = false;
        SelectedLocation = null;
        Message = null;
    }

    [RelayCommand]
    private void EditLocation(User? user)
    {
        if (user is null || !HasAccess) return;
        SelectedUser = user;
        IsProvisioning = false;
        IsEditingLocation = true;
        IsProvisionedTab = true;
        SelectedLocation = Locations.FirstOrDefault(l => l.Id == user.LocationId);
        Message = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        SelectedUser = null;
        IsProvisioning = IsEditingLocation = false;
        SelectedLocation = null;
        Message = null;
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        if (!HasAccess || IsBusy || SelectedUser is null) return;
        if (IsProvisioning && !CanProvisionSelected)
        {
            Message = SelectedUser.IsActive
                ? "This identity has no FlexiSpace app role assigned in Microsoft Entra. Assign a role there, refresh this page, then provision the user."
                : "This account is inactive in Microsoft Entra and cannot be provisioned.";
            await ActionFeedback.FailAsync(Message, "Users");
            return;
        }
        if (SelectedLocation is null)
        {
            Message = "Select a location before continuing.";
            await ActionFeedback.FailAsync(Message, "Users");
            return;
        }

        IsBusy = true;
        try
        {
            var provisioning = IsProvisioning;
            var result = provisioning
                ? await admin.ProvisionUserAsync(SelectedUser.EntraObjectId, SelectedLocation.Id)
                : await admin.UpdateUserLocationAsync(SelectedUser, SelectedLocation.Id);
            Message = result.Message;
            if (result.Success)
            {
                CancelEdit();
                if (provisioning) IsProvisionedTab = true;
                await ActionFeedback.SuccessAsync(result.Message, provisioning ? "User provisioned" : "Location updated");
                await AppearingAsync();
            }
            else
            {
                await ActionFeedback.FailAsync(result.Message, provisioning ? "Could not provision" : "Could not update location");
            }
        }
        finally { IsBusy = false; }
    }

    public sealed class LocationOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class RoleFilterOption
    {
        public UserRole? Role { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class LocationFilterOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

public partial class UserFilterChip : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool isSelected;
}
