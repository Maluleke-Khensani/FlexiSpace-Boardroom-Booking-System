using System.Collections.Specialized;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class AvailabilityPage : ContentPage
{
    private readonly AvailabilityViewModel _vm;

    public AvailabilityPage(AvailabilityViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Items.CollectionChanged += OnItemsChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        RebuildMap();
        ApplyViewToggle(map: true);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildMap();

    private void OnMapViewClicked(object? sender, EventArgs e) => ApplyViewToggle(map: true);

    private void OnListViewClicked(object? sender, EventArgs e) => ApplyViewToggle(map: false);

    private void ApplyViewToggle(bool map)
    {
        MapHost.IsVisible = map;
        ListHost.IsVisible = !map;
        PaintToggle(MapViewButton, map);
        PaintToggle(ListViewButton, !map);
    }

    private static void PaintToggle(Button button, bool on)
    {
        button.BackgroundColor = on ? Color.FromArgb("#C4A035") : Colors.Transparent;
        button.TextColor = on ? Color.FromArgb("#111111") : Color.FromArgb("#1A1A1A");
        button.BorderColor = on ? Color.FromArgb("#C4A035") : Color.FromArgb("#1A1A1A");
    }

    /// <summary>
    /// Rebuilds the cinema-style directory map from the current room list — one marquee
    /// group per location, rooms fanned out beneath as tappable photo tiles.
    /// </summary>
    private void RebuildMap()
    {
        MapHost.Children.Clear();

        if (_vm.Items.Count == 0)
        {
            MapHost.Children.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["ElevatedCard"],
                Padding = new Thickness(22, 20),
                Margin = new Thickness(0, 12, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            FontFamily = "MaterialIcons",
                            Text = "\ue51e",
                            FontSize = 28,
                            TextColor = (Color)Application.Current.Resources["BrandGold"],
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = "No rooms for this filter",
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 16,
                            TextColor = (Color)Application.Current.Resources["BrandInk"],
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = "Try another location or pull to refresh.",
                            FontSize = 13,
                            TextColor = (Color)Application.Current.Resources["BrandMuted"],
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            });
            return;
        }

        var groups = _vm.Items
            .GroupBy(i => i.LocationName)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var tiles = group.Select(item => new RoomMapTile
            {
                Title = item.Room.Name,
                Subtitle = $"Seats {item.Room.Capacity} · {item.StatusText}",
                ImageKey = item.Room.ImageKey,
                RingColor = item.StatusColor,
                Command = _vm.OpenRoomCommand,
                CommandParameter = item
            }).ToList();

            var view = RoomMapBuilder.BuildGroup(group.Key, $"{tiles.Count} room{(tiles.Count == 1 ? "" : "s")} · tap to explore", tiles);
            MapHost.Children.Add(view);
        }
    }
}
