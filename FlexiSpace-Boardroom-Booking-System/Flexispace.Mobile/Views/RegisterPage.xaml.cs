using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RegisterContent.Opacity = 0;
        RegisterContent.TranslationY = 12;
        await Task.WhenAll(
            RegisterContent.FadeToAsync(1, 240),
            RegisterContent.TranslateToAsync(0, 0, 280, Easing.CubicOut));
    }
}
