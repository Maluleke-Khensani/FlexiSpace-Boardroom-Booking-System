using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _vm;
    private bool _entrancePlayed;

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        _ = PlayEntranceAsync();
    }

    private async Task PlayEntranceAsync()
    {
        if (_entrancePlayed || HeroHud is null || ArenaContent is null)
            return;

        _entrancePlayed = true;
        try
        {
            HeroHud.Opacity = 0;
            HeroHud.TranslationY = -18;
            HeroHud.Scale = 0.96;
            ArenaContent.Opacity = 0.35;

            await Task.WhenAll(
                HeroHud.FadeToAsync(1, 320, Easing.CubicOut),
                HeroHud.TranslateToAsync(0, 0, 360, Easing.SpringOut),
                HeroHud.ScaleToAsync(1.0, 360, Easing.SpringOut),
                ArenaContent.FadeToAsync(1, 420, Easing.CubicOut));

            await HeroHud.ScaleToAsync(1.02, 120, Easing.CubicOut);
            await HeroHud.ScaleToAsync(1.0, 140, Easing.SpringOut);
        }
        catch
        {
            // Page may navigate away mid-animation.
        }
    }
}
