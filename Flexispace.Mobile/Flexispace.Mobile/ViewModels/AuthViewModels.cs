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

public partial class LoginViewModel(
    IAuthService auth,
    INotificationService notifications,
    IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isGuest = auth.IsGuest;

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

            await FinishSignInAsync(result.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ContinueAsGuestAsync()
    {
        if (IsBusy) return;
        auth.ContinueAsGuest();
        await ShellReloader.ReloadAsync();
    }

    [RelayCommand]
    private async Task BackToBrowsingAsync() =>
        await Shell.Current.GoToAsync("//HomePage");

    private async Task FinishSignInAsync(string? welcome)
    {
        await ShellReloader.ReloadAsync();
        var showedBriefing = await SignInBriefing.ShowAsync(notifications, bookings, auth);
        if (!showedBriefing && !string.IsNullOrWhiteSpace(welcome))
            await ActionFeedback.SuccessAsync(welcome, "Signed in");
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");
}
