using Flexispace.Mobile.Models;
using Flexispace.Mobile.ViewModels;
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#endif

namespace Flexispace.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private int _homeIndex;
    private CancellationTokenSource? _homeHoverCts;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        HandlerChanged += OnHandlerChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        _homeIndex = Math.Clamp(_homeIndex, 0, Math.Max(0, _vm.Locations.Count - 1));
        RefreshHomeCarousel();
        _ = PlayHeroEntranceAsync();
    }

    private async Task PlayHeroEntranceAsync()
    {
        HeroBanner.Opacity = 0;
        HeroBanner.TranslationY = -18;
        await Task.WhenAll(
            HeroBanner.FadeToAsync(1, 220),
            HeroBanner.TranslateToAsync(0, 0, 280, Easing.CubicOut));
        await HeroBanner.ScaleToAsync(1.03, 90, Easing.CubicOut);
        await HeroBanner.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _homeHoverCts?.Cancel();
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (Handler?.PlatformView is FrameworkElement fe)
        {
            fe.KeyDown -= OnPlatformKeyDown;
            fe.PointerWheelChanged -= OnPointerWheelChanged;
            fe.KeyDown += OnPlatformKeyDown;
            fe.PointerWheelChanged += OnPointerWheelChanged;
            fe.IsTabStop = true;
        }
#endif
    }

#if WINDOWS
    private void OnPlatformKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Left)
        {
            MoveHome(-1);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            MoveHome(1);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        MoveHome(delta < 0 ? 1 : -1);
        e.Handled = true;
    }
#endif

    private void MoveHome(int direction)
    {
        if (_vm.Locations.Count == 0) return;
        _homeIndex = (_homeIndex + direction + _vm.Locations.Count) % _vm.Locations.Count;
        RefreshHomeCarousel();
    }

    private void RefreshHomeCarousel()
    {
        if (_vm.Locations.Count == 0) return;

        var left = _vm.Locations[(_homeIndex - 1 + _vm.Locations.Count) % _vm.Locations.Count];
        var center = _vm.Locations[_homeIndex];
        var right = _vm.Locations[(_homeIndex + 1) % _vm.Locations.Count];

        ApplyCard(HomeLeftCard, HomeLeftImage, HomeLeftTitle, HomeLeftSubtitle, left, -48, 0.88, -18, 0.72);
        ApplyCard(HomeCenterCard, HomeCenterImage, HomeCenterTitle, HomeCenterSubtitle, center, 0, 1.0, 0, 1.0);
        ApplyCard(HomeRightCard, HomeRightImage, HomeRightTitle, HomeRightSubtitle, right, 48, 0.88, 18, 0.72);

        _homeHoverCts?.Cancel();
        _homeHoverCts = new CancellationTokenSource();
        _ = AnimateHoverLoopAsync(HomeCenterCard, _homeHoverCts.Token);
    }

    private static void ApplyCard(Border card, Image image, Label title, Label subtitle, OfficeLocation loc, double x, double scale, double yRot, double opacity)
    {
        image.Source = loc.ImageKey;
        title.Text = loc.Name;
        subtitle.Text = loc.Tagline;
        card.TranslationX = x;
        card.Scale = scale;
        card.RotationY = yRot;
        card.RotationX = 0;
        card.Opacity = opacity;
    }

    private static async Task AnimateHoverLoopAsync(VisualElement view, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.WhenAll(
                    view.TranslateToAsync(0, -5, 700, Easing.SinInOut),
                    view.RotateYToAsync(5, 700, Easing.SinInOut),
                    view.RotateXToAsync(2, 700, Easing.SinInOut));
                await Task.WhenAll(
                    view.TranslateToAsync(0, 4, 900, Easing.SinInOut),
                    view.RotateYToAsync(-5, 900, Easing.SinInOut),
                    view.RotateXToAsync(-2, 900, Easing.SinInOut));
                await Task.WhenAll(
                    view.TranslateToAsync(0, 0, 700, Easing.SinInOut),
                    view.RotateYToAsync(0, 700, Easing.SinInOut),
                    view.RotateXToAsync(0, 700, Easing.SinInOut));
            }
        }
        catch { }
    }

    private void OnHomePreviousClicked(object? sender, EventArgs e) => MoveHome(-1);
    private void OnHomeNextClicked(object? sender, EventArgs e) => MoveHome(1);
    private void OnHomePreviousClicked(object? sender, TappedEventArgs e) => MoveHome(-1);
    private void OnHomeNextClicked(object? sender, TappedEventArgs e) => MoveHome(1);
    private void OnHomeSwipedLeft(object? sender, SwipedEventArgs e) => MoveHome(1);
    private void OnHomeSwipedRight(object? sender, SwipedEventArgs e) => MoveHome(-1);

    private async void OnHomeCenterTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.Locations.Count == 0) return;
        await _vm.OpenLocationCommand.ExecuteAsync(_vm.Locations[_homeIndex]);
    }

    private async void OnMyBookingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(MyBookingsPage));
}
