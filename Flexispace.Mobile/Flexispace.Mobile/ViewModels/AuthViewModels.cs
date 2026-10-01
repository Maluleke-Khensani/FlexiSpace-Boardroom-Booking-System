using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    [RelayCommand]
    private async Task GetStartedAsync() =>
        await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");
}

public partial class LoginViewModel(IAuthService auth) : ObservableObject
{
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await auth.SignInAsync();
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            // Rebuild the Shell so the tab bar reflects this user's role from the start.
            await ShellReloader.ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");
}
