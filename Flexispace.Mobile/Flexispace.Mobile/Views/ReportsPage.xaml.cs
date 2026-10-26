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
        vm.ApplySavedMode();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        _ = PlayEntranceAsync();
    }

    private async Task PlayEntranceAsync()
    {
        if (_entrancePlayed || ArenaContent is null)
            return;

        _entrancePlayed = true;
        var header = _vm.IsGamified ? HeroHud : ProHeader;
        try
        {
            ArenaContent.Opacity = 0.35;
            if (header is not null)
            {
                header.Opacity = 0;
                header.TranslationY = -18;
                header.Scale = 0.96;
            }

            await Task.WhenAll(
                ArenaContent.FadeToAsync(1, 420, Easing.CubicOut),
                header?.FadeToAsync(1, 320, Easing.CubicOut) ?? Task.CompletedTask,
                header?.TranslateToAsync(0, 0, 360, Easing.SpringOut) ?? Task.CompletedTask,
                header?.ScaleToAsync(1.0, 360, Easing.SpringOut) ?? Task.CompletedTask);

            if (header is not null && _vm.IsGamified)
            {
                await header.ScaleToAsync(1.02, 120, Easing.CubicOut);
                await header.ScaleToAsync(1.0, 140, Easing.SpringOut);
            }
        }
        catch
        {
            // Page may navigate away mid-animation.
        }
    }
}
