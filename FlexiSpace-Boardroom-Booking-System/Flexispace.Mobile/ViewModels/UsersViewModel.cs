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
    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string lastName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private UserRole selectedRole = UserRole.Client;
    [ObservableProperty] private LocationOption? selectedLocation;
    [ObservableProperty] private bool showLocationPicker;

    public ObservableCollection<User> Users { get; } = [];
    public ObservableCollection<LocationOption> Locations { get; } = [];

    public UserRole[] Roles { get; } =
    [
        UserRole.Client,
        UserRole.Staff,
        UserRole.CentreManager,
        UserRole.Administrator
    ];

    partial void OnSelectedRoleChanged(UserRole value) =>
        ShowLocationPicker = value is UserRole.CentreManager or UserRole.Staff;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanManageUsers(user.Role);
        if (!HasAccess)
        {
            Message = "Only Administrators can manage users.";
            Users.Clear();
            return;
        }

        IsBusy = true;
        Message = null;
        try
        {
            Locations.Clear();
            Locations.Add(new LocationOption { Id = null, Name = "(No location)" });
            foreach (var loc in await rooms.GetLocationsAsync())
                Locations.Add(new LocationOption { Id = loc.Id, Name = loc.Name });
            SelectedLocation ??= Locations.FirstOrDefault();

            Users.Clear();
            foreach (var u in await admin.GetUsersAsync())
                Users.Add(u);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        if (!HasAccess || IsBusy) return;

        if (SelectedRole is UserRole.CentreManager && string.IsNullOrWhiteSpace(SelectedLocation?.Id))
        {
            Message = "Centre Managers need a location.";
            await ActionFeedback.FailAsync(Message, "Users");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await admin.AddUserAsync(new AdminUserCreateRequest
            {
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                Role = SelectedRole,
                LocationId = ShowLocationPicker ? SelectedLocation?.Id : null
            });

            Message = result.Message;
            if (result.Success)
            {
                FirstName = LastName = Email = string.Empty;
                SelectedRole = UserRole.Client;
                await ActionFeedback.SuccessAsync(result.Message, "User added");
                await AppearingAsync();
            }
            else
            {
                await ActionFeedback.FailAsync(result.Message, "Could not add");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveUserAsync(User? user)
    {
        if (user is null || !HasAccess) return;

        var ok = await Shell.Current.DisplayAlertAsync(
            "Remove user",
            $"Deactivate {user.Name} ({user.Email})? They will no longer be able to use FlexiSpace.",
            "Remove",
            "Cancel");
        if (!ok) return;

        var success = await admin.SetUserActiveAsync(user.ApiId, isActive: false);
        Message = success ? $"{user.Name} deactivated." : "Could not remove that user.";
        if (success)
            await AppearingAsync();
        else
            await ActionFeedback.FailAsync(Message, "Users");
    }

    [RelayCommand]
    private async Task RestoreUserAsync(User? user)
    {
        if (user is null || !HasAccess) return;
        var success = await admin.SetUserActiveAsync(user.ApiId, isActive: true);
        Message = success ? $"{user.Name} restored." : "Could not restore that user.";
        if (success)
            await AppearingAsync();
    }

    public sealed class LocationOption
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
