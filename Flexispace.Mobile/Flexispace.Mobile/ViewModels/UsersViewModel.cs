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
    [ObservableProperty] private FilterOption? selectedRoleFilter;
    [ObservableProperty] private FilterOption? selectedLocationFilter;
    [ObservableProperty] private LocationOption? selectedFormLocation;

    private int _editingApiId;
    private Guid _editingId;
    private Guid _editingEntraObjectId;
    private readonly List<UserDirectoryRow> _provisionedAll = [];
    private readonly List<UserDirectoryRow> _unprovisionedAll = [];

    public ObservableCollection<UserDirectoryRow> ProvisionedUsers { get; } = [];
    public ObservableCollection<UserDirectoryRow> UnprovisionedUsers { get; } = [];
    public ObservableCollection<LocationOption> FormLocations { get; } = [];
    public ObservableCollection<FilterOption> RoleFilters { get; } = [];
    public ObservableCollection<FilterOption> LocationFilters { get; } = [];

    public UserRole[] Roles { get; } =
    [
        UserRole.Staff,
        UserRole.CentreManager,
        UserRole.Administrator,
        UserRole.Client
    ];

    public List<FilterOption> FormRoleOptions { get; } =
    [
        new() { Label = "Staff", Value = nameof(UserRole.Staff) },
        new() { Label = "Centre Manager", Value = nameof(UserRole.CentreManager) },
        new() { Label = "Administrator", Value = nameof(UserRole.Administrator) },
        new() { Label = "Client", Value = nameof(UserRole.Client) }
    ];

    [ObservableProperty] private FilterOption? selectedFormRole;

    public string FormTitle => IsProvisioning ? "Provision user" : "Edit user";
    public string SaveButtonText => IsProvisioning ? "Provision" : "Save";
    public bool HasProvisionedUsers => ProvisionedUsers.Count > 0;
    public bool HasUnprovisionedUsers => UnprovisionedUsers.Count > 0;
    public bool ShowActivateButton => IsFormOpen && !IsProvisioning && !FormIsActive;
    public string FormStatusLabel => FormIsActive ? "Status · Active" : "Status · Inactive";
    public int ProvisionedCount => ProvisionedUsers.Count;
    public int UnprovisionedCount => UnprovisionedUsers.Count;
    public bool HasFilters =>
        !string.IsNullOrEmpty(SelectedRoleFilter?.Value) || !string.IsNullOrEmpty(SelectedLocationFilter?.Value);
    public string ProvisionedEmptyText =>
        HasFilters ? "No provisioned users match these filters." : "No FlexiSpace accounts yet.";
    public string UnprovisionedEmptyText =>
        HasFilters
            ? "No unprovisioned users match these filters."
            : "Every Microsoft Entra identity already has a FlexiSpace account.";

    partial void OnIsFormOpenChanged(bool value) => NotifyFormState();
    partial void OnIsProvisioningChanged(bool value) => NotifyFormState();
    partial void OnFormIsActiveChanged(bool value) => NotifyFormState();

    partial void OnSelectedRoleFilterChanged(FilterOption? value) => ApplyFilters();
    partial void OnSelectedLocationFilterChanged(FilterOption? value) => ApplyFilters();

    partial void OnSelectedFormRoleChanged(FilterOption? value)
    {
        if (value is not null && Enum.TryParse<UserRole>(value.Value, out var role))
            FormRole = role;
    }

    partial void OnSelectedFormLocationChanged(LocationOption? value) =>
        FormLocationId = value?.Id ?? string.Empty;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageUsers(user.Role);
        if (!HasAccess)
        {
            Subtitle = "Only Administrators can manage users.";
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
            var locations = DistinctLocations(await rooms.GetLocationsAsync());
            RebuildLookups(locations);

            var locationNames = locations.ToDictionary(l => l.Id, l => l.Name, StringComparer.OrdinalIgnoreCase);
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
                _provisionedAll.Add(ToRow(account, locationNames, canManage: true));

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

                _unprovisionedAll.Add(ToRow(account, locationNames, canManage: false));
            }

            ApplyFilters();
            IsEmpty = _provisionedAll.Count == 0 && _unprovisionedAll.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load users. {ex.Message}";
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
        SelectedFormRole = FormRoleOptions.FirstOrDefault(r => r.Value == row.Role.ToString())
            ?? FormRoleOptions[0];
        FormIsActive = row.IsActive;
        SelectedFormLocation = FormLocations.FirstOrDefault(l => l.Id == row.LocationId)
            ?? FormLocations.FirstOrDefault();
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
        SelectedFormRole = FormRoleOptions.FirstOrDefault(r => r.Value == FormRole.ToString())
            ?? FormRoleOptions[0];
        FormIsActive = true;
        SelectedFormLocation = FormLocations.FirstOrDefault(l => l.Id == row.LocationId)
            ?? FormLocations.FirstOrDefault();
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

        if (FormRole is UserRole.CentreManager && string.IsNullOrWhiteSpace(SelectedFormLocation?.Id))
        {
            ErrorMessage = "Centre Managers need a location.";
            await ActionFeedback.FailAsync(ErrorMessage, "Users");
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
            LocationId = string.IsNullOrWhiteSpace(SelectedFormLocation?.Id) ? null : SelectedFormLocation.Id
        };

        var result = IsProvisioning
            ? await admin.ProvisionUserAsync(draft)
            : await admin.UpdateUserAsync(draft);

        if (!result.Ok)
        {
            ErrorMessage = result.Message;
            await ActionFeedback.FailAsync(result.Message, "Users");
            return;
        }

        Message = result.Message;
        IsFormOpen = false;
        await ActionFeedback.SuccessAsync(result.Message, IsProvisioning ? "Provisioned" : "User updated");
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
            await ActionFeedback.FailAsync(result.Message, "Users");
            return;
        }

        FormIsActive = true;
        var apiId = _editingApiId;
        await AppearingAsync();
        var row = _provisionedAll.FirstOrDefault(u => u.ApiId == apiId);
        if (row is not null)
            StartEdit(row);
        Message = result.Message;
        await ActionFeedback.SuccessAsync(result.Message, "User activated");
    }

    [RelayCommand]
    private async Task RemoveUserAsync(UserDirectoryRow? row)
    {
        if (row is null || !HasAccess || !row.CanManage) return;
        if (row.Id == auth.CurrentUser?.Id || row.ApiId == auth.CurrentUser?.ApiId)
        {
            ErrorMessage = "You cannot remove your own account.";
            await ActionFeedback.FailAsync(ErrorMessage, "Users");
            return;
        }

        var ok = await Shell.Current.DisplayAlertAsync(
            "Remove user",
            $"Deactivate {row.Name} ({row.Email})? They will no longer be able to use FlexiSpace. Open Edit to activate them again.",
            "Remove",
            "Cancel");
        if (!ok) return;

        IsBusy = true;
        ErrorMessage = null;
        Message = null;
        try
        {
            var result = await admin.RemoveUserAsync(row.ApiId);
            if (!result.Ok)
            {
                ErrorMessage = result.Message;
                await ActionFeedback.FailAsync(result.Message, "Users");
                return;
            }

            Message = result.Message;
            if (IsFormOpen && _editingApiId == row.ApiId)
                IsFormOpen = false;
            await ActionFeedback.SuccessAsync(result.Message, "User removed");
            await AppearingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildLookups(IReadOnlyList<OfficeLocation> locations)
    {
        FormLocations.Clear();
        FormLocations.Add(new LocationOption { Id = null, Name = "All locations" });
        foreach (var location in locations)
            FormLocations.Add(new LocationOption { Id = location.Id, Name = location.Name });
        SelectedFormLocation ??= FormLocations.FirstOrDefault();

        if (RoleFilters.Count == 0)
        {
            RoleFilters.Add(new FilterOption { Label = "All roles", Value = string.Empty });
            foreach (var role in Roles)
                RoleFilters.Add(new FilterOption { Label = RolePermissions.DisplayName(role), Value = role.ToString() });
            SelectedRoleFilter = RoleFilters[0];
        }

        var selectedLocationValue = SelectedLocationFilter?.Value ?? string.Empty;
        LocationFilters.Clear();
        LocationFilters.Add(new FilterOption { Label = "All locations", Value = string.Empty });
        foreach (var location in locations)
            LocationFilters.Add(new FilterOption { Label = location.Name, Value = location.Id });
        SelectedLocationFilter = LocationFilters.FirstOrDefault(l => l.Value == selectedLocationValue)
            ?? LocationFilters[0];
    }

    private void ApplyFilters()
    {
        ProvisionedUsers.Clear();
        UnprovisionedUsers.Clear();
        foreach (var row in _provisionedAll.Where(MatchesFilter))
            ProvisionedUsers.Add(row);
        foreach (var row in _unprovisionedAll.Where(MatchesFilter))
            UnprovisionedUsers.Add(row);
        OnPropertyChanged(nameof(HasFilters));
        OnPropertyChanged(nameof(HasProvisionedUsers));
        OnPropertyChanged(nameof(HasUnprovisionedUsers));
        OnPropertyChanged(nameof(ProvisionedCount));
        OnPropertyChanged(nameof(UnprovisionedCount));
        OnPropertyChanged(nameof(ProvisionedEmptyText));
        OnPropertyChanged(nameof(UnprovisionedEmptyText));
    }

    private bool MatchesFilter(UserDirectoryRow row)
    {
        if (!string.IsNullOrEmpty(SelectedRoleFilter?.Value) && row.Role.ToString() != SelectedRoleFilter.Value)
            return false;
        if (!string.IsNullOrEmpty(SelectedLocationFilter?.Value) && row.LocationId != SelectedLocationFilter.Value)
            return false;
        return true;
    }

    private void NotifyFormState()
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(ShowActivateButton));
        OnPropertyChanged(nameof(FormStatusLabel));
    }

    private static UserDirectoryRow ToRow(
        User account,
        IReadOnlyDictionary<string, string> locationNames,
        bool canManage)
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
            EntraObjectId = account.EntraObjectId
        };
    }

    private static List<OfficeLocation> DistinctLocations(IEnumerable<OfficeLocation> locations) =>
        locations
            .Where(l => !string.IsNullOrWhiteSpace(l.Id))
            .GroupBy(l => l.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    private static (string First, string Last) SplitName(string name)
    {
        var parts = (name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts.ElementAtOrDefault(0) ?? string.Empty, parts.ElementAtOrDefault(1) ?? string.Empty);
    }

    public sealed class LocationOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public sealed class FilterOption
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string ToString() => Label;
    }
}
