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
    private async Task LoginWithMicrosoftAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await auth.LoginWithMicrosoftAsync();
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                await ActionFeedback.FailAsync(result.Message, "Sign-in blocked");
                return;
            }

            await ActionFeedback.SuccessAsync(result.Message, "Signed in");
            await ShellReloader.ReloadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UseDemoAsync(string email)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await auth.LoginAsync(email, "demo123");
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                await ActionFeedback.FailAsync(result.Message, "Sign-in blocked");
                return;
            }

            await ActionFeedback.SuccessAsync(result.Message, "Signed in");
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
