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
}

public partial class LoginViewModel(IAuthService auth) : ObservableObject
{
    [ObservableProperty] private string email = "staff@flexispace.net.za";
    [ObservableProperty] private string password = "demo123";
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var ok = await auth.LoginAsync(Email, Password);
            if (!ok)
            {
                ErrorMessage = "Invalid email or password. Try a demo account below.";
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
    private async Task UseDemoAsync(string email)
    {
        Email = email;
        Password = "demo123";
        await LoginAsync();
    }
}
