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
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildMap();

    private void OnMapViewClicked(object? sender, EventArgs e)
    {
        MapHost.IsVisible = true;
        ListHost.IsVisible = false;
    }

    private void OnListViewClicked(object? sender, EventArgs e)
    {
        MapHost.IsVisible = false;
        ListHost.IsVisible = true;
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
            MapHost.Children.Add(new Label
            {
                Text = "No rooms found for this filter.",
                TextColor = (Color)Application.Current!.Resources["BrandMuted"],
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 24, 0, 0)
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
