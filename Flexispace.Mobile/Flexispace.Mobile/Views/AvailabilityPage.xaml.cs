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

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildMap();
        RebuildList();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        RebuildMap();
        RebuildList();
        ApplyViewToggle(map: true);
    }

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

    /// <summary>
    /// The list uses the same centre groups as the map. It is drawn in code because a
    /// nested layout inside the scroll view collapsed the room cards into a gold line.
    /// </summary>
    private void RebuildList()
    {
        ListHost.Children.Clear();
        var res = Application.Current!.Resources;
        Color Brand(string key) => (Color)res[key];

        if (_vm.Items.Count == 0)
        {
            ListHost.Children.Add(new Border
            {
                Style = (Style)res["ElevatedCard"],
                Padding = new Thickness(22, 20),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = "No rooms for this filter",
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 16,
                            TextColor = Brand("BrandInk"),
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = "Try another location or pull to refresh.",
                            FontSize = 13,
                            TextColor = Brand("BrandMuted"),
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            });
            return;
        }

        foreach (var group in _vm.Items.GroupBy(i => i.LocationName).OrderBy(g => g.Key))
        {
            var rooms = group.ToList();
            var section = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Fill };
            section.Children.Add(new Label
            {
                Text = "CENTRE",
                Style = (Style)res["EyebrowLabel"],
                FontSize = 10
            });
            section.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(group.Key) ? "Centre" : group.Key,
                TextColor = Brand("BrandInk"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 18
            });
            section.Children.Add(new Label
            {
                Text = rooms.Count == 1 ? "1 room" : $"{rooms.Count} rooms",
                TextColor = Brand("BrandMuted"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });

            foreach (var item in rooms)
                section.Children.Add(BuildRoomCard(item, res, Brand));

            ListHost.Children.Add(section);
        }
    }

    private View BuildRoomCard(AvailabilityItem item, ResourceDictionary res, Func<string, Color> brand)
    {
        var status = new Border
        {
            Style = (Style)res["StatusPillBorder"],
            BackgroundColor = item.StatusColor,
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = item.StatusText,
                Style = (Style)res["StatusPillText"]
            }
        };

        var details = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = item.Room.Name,
                    TextColor = brand("BrandInk"),
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 16
                },
                new Label
                {
                    Text = $"Seats {item.Room.Capacity}",
                    TextColor = brand("BrandGoldDeep"),
                    FontSize = 12
                }
            }
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Padding = new Thickness(12, 10),
            Children = { details, status }
        };
        body.SetColumn(status, 1);

        if (_vm.CanBook)
        {
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            body.SetRow(details, 0);
            var book = new Button
            {
                Text = "Book",
                Style = (Style)res["GoldButton"],
                Padding = new Thickness(12, 6),
                FontSize = 12,
                MinimumHeightRequest = 36,
                HorizontalOptions = LayoutOptions.End,
                Command = _vm.BookRoomCommand,
                CommandParameter = item
            };
            body.Add(book, 1, 1);
        }

        var card = new Border
        {
            Style = (Style)res["ElevatedCard"],
            Padding = 0,
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(new GridLength(88)),
                    new ColumnDefinition(GridLength.Star)
                },
                Children =
                {
                    new Image
                    {
                        Source = item.Room.ImageKey,
                        Aspect = Aspect.AspectFill,
                        HeightRequest = 96
                    },
                    body
                }
            }
        };
        ((Grid)card.Content).SetColumn(body, 1);

        var tap = new TapGestureRecognizer
        {
            Command = _vm.OpenRoomCommand,
            CommandParameter = item
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }
}
