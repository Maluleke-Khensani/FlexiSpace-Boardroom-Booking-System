using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _vm;

    public NotificationsPage(NotificationsViewModel vm)
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
        AlertsHero.Opacity = 0;
        AlertsHero.TranslationY = -16;
        await Task.WhenAll(
            AlertsHero.FadeToAsync(1, 220),
            AlertsHero.TranslateToAsync(0, 0, 280, Easing.CubicOut));
        await AlertsHero.ScaleToAsync(1.03, 90, Easing.CubicOut);
        await AlertsHero.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }
}
