using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoginContent.Opacity = 0;
        LoginContent.TranslationY = 12;
        await Task.WhenAll(
            LoginContent.FadeToAsync(1, 240),
            LoginContent.TranslateToAsync(0, 0, 280, Easing.CubicOut));
    }
}
