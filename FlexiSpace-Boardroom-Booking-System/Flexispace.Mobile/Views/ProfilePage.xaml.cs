using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        _ = PlayHeroEntranceAsync();
    }

    private async Task PlayHeroEntranceAsync()
    {
        ProfileHero.Opacity = 0;
        ProfileHero.TranslationY = -16;
        await Task.WhenAll(
            ProfileHero.FadeToAsync(1, 220),
            ProfileHero.TranslateToAsync(0, 0, 280, Easing.CubicOut));
        await ProfileHero.ScaleToAsync(1.03, 90, Easing.CubicOut);
        await ProfileHero.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }
}
