using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// One clickable node on a room map — the photo (or title) is the hit target, exactly
/// like picking a seat on a cinema seating chart.
/// </summary>
public class RoomMapTile
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string ImageKey { get; init; }
    public Color RingColor { get; init; } = Colors.Gray;
    public ICommand? Command { get; init; }
    public object? CommandParameter { get; init; }
}

/// <summary>
/// Builds a stylised "directory map" in place of a plain list — a marquee header per
/// location (standing in for the cinema screen) with rooms fanned out beneath it in a
/// gentle curved row, echoing tiered cinema seating. We don't have real floor plans for
/// any of the buildings, so this gives people a spatial, playful way to browse and pick a
/// room by tapping its photo or name, rather than scrolling a flat list.
/// </summary>
public static class RoomMapBuilder
{
    public static View BuildGroup(string groupTitle, string groupSubtitle, IReadOnlyList<RoomMapTile> tiles)
    {
        var res = Application.Current!.Resources;
        Color Brand(string key) => (Color)res[key];

        var root = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 0, 0, 26) };

        var header = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Brand("BrandBlack"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            Padding = new Thickness(20, 16),
            Margin = new Thickness(4, 0, 4, 8),
            Shadow = new Shadow { Brush = new SolidColorBrush(Brand("BrandInk")), Opacity = 0.22f, Radius = 14, Offset = new Point(0, 6) }
        };
        var headerStack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        headerStack.Children.Add(new Label
        {
            FontFamily = "MaterialIcons",
            Text = "\ue51e",
            TextColor = Brand("BrandGold"),
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center
        });
        headerStack.Children.Add(new Label
        {
            Text = groupTitle.ToUpperInvariant(),
            TextColor = Brand("BrandGold"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            CharacterSpacing = 1.6,
            HorizontalTextAlignment = TextAlignment.Center
        });
        headerStack.Children.Add(new Label
        {
            Text = groupSubtitle,
            TextColor = Brand("BrandGoldSoft"),
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        });
        header.Content = headerStack;
        root.Children.Add(header);

        root.Children.Add(new BoxView
        {
            HeightRequest = 3,
            WidthRequest = 220,
            Margin = new Thickness(0, 0, 0, 22),
            CornerRadius = 2,
            Color = Brand("BrandGold"),
            Opacity = 0.5,
            HorizontalOptions = LayoutOptions.Center
        });

        const int perRow = 4;
        var rows = Chunk(tiles, perRow);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var rowLayout = new HorizontalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.Center };
            var n = row.Count;
            for (var i = 0; i < n; i++)
            {
                var centerOffset = i - (n - 1) / 2.0;
                // Parabolic "grin" curve, like a raked cinema row bowing away from the screen at the edges.
                var curve = Math.Pow(centerOffset, 2) * 7.0;
                var tileView = CreateTile(row[i], res);
                tileView.TranslationY = curve;
                rowLayout.Children.Add(tileView);
            }
            root.Children.Add(rowLayout);

            if (r < rows.Count - 1)
                root.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Margin = new Thickness(50, 26),
                    Color = Brand("BrandLine")
                });
        }

        return root;
    }

    private static View CreateTile(RoomMapTile tile, ResourceDictionary res)
    {
        Color Brand(string key) => (Color)res[key];

        var photo = new Border
        {
            WidthRequest = 78,
            HeightRequest = 78,
            Padding = 0,
            StrokeThickness = 3,
            Stroke = tile.RingColor,
            StrokeShape = new RoundRectangle { CornerRadius = 39 },
            BackgroundColor = Colors.White,
            Content = new Image { Source = tile.ImageKey, Aspect = Aspect.AspectFill }
        };
        photo.Shadow = new Shadow { Brush = new SolidColorBrush(Brand("BrandInk")), Opacity = 0.18f, Radius = 8, Offset = new Point(0, 3) };

        var dot = new Border
        {
            WidthRequest = 18,
            HeightRequest = 18,
            Padding = 0,
            StrokeThickness = 2,
            Stroke = Colors.White,
            BackgroundColor = tile.RingColor,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End
        };

        var photoLayer = new Grid { WidthRequest = 78, HeightRequest = 88 };
        photoLayer.Children.Add(photo);
        photoLayer.Children.Add(dot);

        var stack = new VerticalStackLayout
        {
            Spacing = 3,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 96
        };
        stack.Children.Add(photoLayer);
        stack.Children.Add(new Label
        {
            Text = tile.Title,
            TextColor = Brand("BrandInk"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        });
        if (!string.IsNullOrEmpty(tile.Subtitle))
        {
            stack.Children.Add(new Label
            {
                Text = tile.Subtitle,
                TextColor = Brand("BrandMuted"),
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            try
            {
                await stack.ScaleToAsync(1.12, 80, Easing.CubicOut);
                await stack.ScaleToAsync(1.0, 110, Easing.SpringOut);
            }
            catch { }

            if (tile.Command?.CanExecute(tile.CommandParameter) == true)
                tile.Command.Execute(tile.CommandParameter);
        };
        stack.GestureRecognizers.Add(tap);

        return stack;
    }

    private static List<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        var result = new List<List<T>>();
        for (var i = 0; i < source.Count; i += size)
            result.Add(source.Skip(i).Take(size).ToList());
        return result;
    }
}
