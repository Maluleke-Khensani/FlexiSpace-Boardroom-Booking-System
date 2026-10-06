using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.ViewModels;
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
#endif

namespace Flexispace.Mobile.Views;

public partial class BookingPage : ContentPage
{
    private static readonly string[] RingColorKeys = ["BrandGold", "BrandTeal", "BrandSuccess", "BrandSteel", "BrandClay", "BrandGoldDeep"];

    private readonly BookingViewModel _vm;
    private int _bookingIndex;
    private CancellationTokenSource? _bookingHoverCts;

    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        HandlerChanged += OnHandlerChanged;
        _vm.Rooms.CollectionChanged += (_, _) => RebuildRoomsMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        _bookingIndex = Math.Clamp(_bookingIndex, 0, Math.Max(0, _vm.Locations.Count - 1));
        RefreshBookingCarousel();
        RebuildRoomsMap();
        ApplyRoomViewToggle(map: true);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bookingHoverCts?.Cancel();
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
        if (_vm.Step != 1) return;
        if (e.Key == VirtualKey.Left)
        {
            MoveBooking(-1);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            MoveBooking(1);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_vm.Step != 1) return;
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        MoveBooking(delta < 0 ? 1 : -1);
        e.Handled = true;
    }
#endif

    private void MoveBooking(int direction)
    {
        if (_vm.Locations.Count == 0 || _vm.Step != 1) return;
        _bookingIndex = (_bookingIndex + direction + _vm.Locations.Count) % _vm.Locations.Count;
        RefreshBookingCarousel();
    }

    private void RefreshBookingCarousel()
    {
        if (_vm.Locations.Count == 0) return;

        var left = _vm.Locations[(_bookingIndex - 1 + _vm.Locations.Count) % _vm.Locations.Count];
        var center = _vm.Locations[_bookingIndex];
        var right = _vm.Locations[(_bookingIndex + 1) % _vm.Locations.Count];

        ApplyCard(BookingLeftCard, BookingLeftImage, BookingLeftTitle, BookingLeftSubtitle, left, -44, 0.86, -18, 0.7);
        ApplyCard(BookingCenterCard, BookingCenterImage, BookingCenterTitle, BookingCenterSubtitle, center, 0, 1.0, 0, 1.0);
        ApplyCard(BookingRightCard, BookingRightImage, BookingRightTitle, BookingRightSubtitle, right, 44, 0.86, 18, 0.7);

        _bookingHoverCts?.Cancel();
        _bookingHoverCts = new CancellationTokenSource();
        _ = AnimateHoverLoopAsync(BookingCenterCard, _bookingHoverCts.Token);
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

    private void OnBookingPreviousClicked(object? sender, EventArgs e) => MoveBooking(-1);
    private void OnBookingNextClicked(object? sender, EventArgs e) => MoveBooking(1);
    private void OnBookingPreviousClicked(object? sender, TappedEventArgs e) => MoveBooking(-1);
    private void OnBookingNextClicked(object? sender, TappedEventArgs e) => MoveBooking(1);
    private void OnBookingSwipedLeft(object? sender, SwipedEventArgs e) => MoveBooking(1);
    private void OnBookingSwipedRight(object? sender, SwipedEventArgs e) => MoveBooking(-1);
    private async void OnBookingCenterTapped(object? sender, TappedEventArgs e) => await ChooseCurrentLocationAsync();
    private async void OnChooseBookingLocationClicked(object? sender, EventArgs e) => await ChooseCurrentLocationAsync();

    private async Task ChooseCurrentLocationAsync()
    {
        if (_vm.Locations.Count == 0 || _vm.Step != 1) return;
        await _vm.SelectLocationCommand.ExecuteAsync(_vm.Locations[_bookingIndex]);
    }

    private void OnRoomsMapViewClicked(object? sender, EventArgs e) => ApplyRoomViewToggle(map: true);

    private void OnRoomsListViewClicked(object? sender, EventArgs e) => ApplyRoomViewToggle(map: false);

    private void ApplyRoomViewToggle(bool map)
    {
        RoomsMapHost.IsVisible = map;
        RoomsListHost.IsVisible = !map;
        PaintToggle(RoomsMapViewButton, map);
        PaintToggle(RoomsListViewButton, !map);
    }

    private static void PaintToggle(Button button, bool on)
    {
        button.BackgroundColor = on ? Color.FromArgb("#C4A035") : Colors.Transparent;
        button.TextColor = on ? Color.FromArgb("#111111") : Color.FromArgb("#1A1A1A");
        button.BorderColor = on ? Color.FromArgb("#C4A035") : Color.FromArgb("#1A1A1A");
    }

    /// <summary>Renders the room picker as a directory-map style board — one marquee for
    /// the chosen location, rooms fanned beneath as tappable photo tiles cycling through
    /// the brand's accent colours since there's no live status to colour-code here.</summary>
    private void RebuildRoomsMap()
    {
        RoomsMapHost.Children.Clear();
        if (_vm.Rooms.Count == 0 || _vm.SelectedLocation is null) return;

        var res = Microsoft.Maui.Controls.Application.Current!.Resources;
        var tiles = _vm.Rooms.Select((room, i) => new RoomMapTile
        {
            Title = room.IsCombined ? "Combined" : room.Name,
            Subtitle = room.IsCombined
                ? $"Seats {room.Capacity}"
                : room.CanCombine
                    ? $"Seats {room.Capacity} · +"
                    : $"Seats {room.Capacity}",
            ImageKey = room.ImageKey,
            RingColor = room.IsCombined
                ? (Color)res["BrandGold"]
                : room.CanCombine
                    ? (Color)res["BrandTeal"]
                    : (Color)res[RingColorKeys[i % RingColorKeys.Length]],
            Command = _vm.SelectRoomCommand,
            CommandParameter = room
        }).ToList();

        var subtitle = _vm.ShowCombineTip
            ? $"{tiles.Count} options · teal rings can combine"
            : $"{tiles.Count} room{(tiles.Count == 1 ? "" : "s")} · tap to select";
        var view = RoomMapBuilder.BuildGroup(_vm.SelectedLocation.Name, subtitle, tiles);
        RoomsMapHost.Children.Add(view);
    }
}
