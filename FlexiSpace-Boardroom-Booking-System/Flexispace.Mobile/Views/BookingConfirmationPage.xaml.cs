using Flexispace.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace Flexispace.Mobile.Views;

public partial class BookingConfirmationPage : ContentPage
{
    private readonly BookingConfirmationViewModel _vm;
    private bool _fireworksPlayed;

    public BookingConfirmationPage(BookingConfirmationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PlayContentEntranceAsync();
        TryPlayFireworks();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BookingConfirmationViewModel.IsConfirmed)
            or nameof(BookingConfirmationViewModel.Booking)
            or nameof(BookingConfirmationViewModel.IsPending))
        {
            TryPlayFireworks();
        }
    }

    private async Task PlayContentEntranceAsync()
    {
        ConfirmContent.Opacity = 0;
        ConfirmContent.TranslationY = 18;
        await Task.WhenAll(
            ConfirmContent.FadeToAsync(1, 240),
            ConfirmContent.TranslateToAsync(0, 0, 280, Easing.CubicOut));

        if (!_vm.IsConfirmed) return;
        CelebrateBadge.Scale = 0.4;
        await CelebrateBadge.ScaleToAsync(1.18, 220, Easing.CubicOut);
        await CelebrateBadge.ScaleToAsync(1.0, 160, Easing.SpringOut);
    }

    private void TryPlayFireworks()
    {
        if (_fireworksPlayed || !_vm.IsConfirmed) return;
        _fireworksPlayed = true;
        _ = PlayFireworksAsync();
    }

    private async Task PlayFireworksAsync()
    {
        await Task.Delay(180);
        FireworksLayer.IsVisible = true;
        FireworksLayer.Children.Clear();

        CelebrateBadge.Scale = 0.5;
        _ = BounceBadgeAsync();

        var gold = Color.FromArgb("#C4A035");
        var goldSoft = Color.FromArgb("#D4B45A");
        var goldDeep = Color.FromArgb("#9A7A1F");
        var teal = Color.FromArgb("#1F6F78");
        var ivory = Color.FromArgb("#FAF8F4");
        var success = Color.FromArgb("#1E8E5A");
        Color[] palette = [gold, goldSoft, goldDeep, teal, ivory, success];

        var originX = Width > 0 ? Width / 2 : 200;
        var originY = Height > 0 ? Height * 0.28 : 180;
        var rnd = Random.Shared;
        var tasks = new List<Task>();

        for (var burst = 0; burst < 3; burst++)
        {
            var delay = burst * 220;
            for (var i = 0; i < 22; i++)
            {
                var size = rnd.Next(5, 12);
                var particle = new Border
                {
                    WidthRequest = size,
                    HeightRequest = size,
                    StrokeThickness = 0,
                    BackgroundColor = palette[rnd.Next(palette.Length)],
                    StrokeShape = new RoundRectangle { CornerRadius = size / 2.0 },
                    InputTransparent = true
                };
                AbsoluteLayout.SetLayoutBounds(particle, new Rect(originX - size / 2.0, originY - size / 2.0, size, size));
                FireworksLayer.Children.Add(particle);

                var angle = rnd.NextDouble() * Math.PI * 2;
                var dist = rnd.Next(70, 240);
                tasks.Add(LaunchParticleAsync(particle, Math.Cos(angle) * dist, Math.Sin(angle) * dist, (uint)rnd.Next(520, 980), delay));
            }
        }

        await Task.WhenAll(tasks);
        FireworksLayer.Children.Clear();
        FireworksLayer.IsVisible = false;
    }

    private async Task BounceBadgeAsync()
    {
        try
        {
            await CelebrateBadge.ScaleToAsync(1.2, 220, Easing.CubicOut);
            await CelebrateBadge.ScaleToAsync(1.0, 160, Easing.SpringOut);
        }
        catch { }
    }

    private static async Task LaunchParticleAsync(VisualElement particle, double dx, double dy, uint duration, int delayMs)
    {
        if (delayMs > 0)
            await Task.Delay(delayMs);
        try
        {
            await Task.WhenAll(
                particle.TranslateToAsync(dx, dy, duration, Easing.CubicOut),
                particle.FadeToAsync(0, duration, Easing.SinIn),
                particle.ScaleToAsync(0.25, duration, Easing.CubicIn));
        }
        catch
        {
            // Page may have navigated away mid-burst.
        }
    }
}
