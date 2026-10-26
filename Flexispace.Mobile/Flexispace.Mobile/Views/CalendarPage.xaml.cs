using Flexispace.Mobile.ViewModels;

namespace Flexispace.Mobile.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.LayoutVersion))
                Paint();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.Watch(true);
        await _vm.AppearingCommand.ExecuteAsync(null);
        Paint();
    }

    protected override void OnDisappearing()
    {
        _vm.Watch(false);
        base.OnDisappearing();
    }

    private void Paint()
    {
        PaintMonth();
        PaintDay();
    }

    private void PaintMonth()
    {
        MonthHost.Children.Clear();
        if (!_vm.ShowMonth || _vm.MonthDays.Count == 0)
            return;

        var header = new Grid { ColumnSpacing = 2 };
        foreach (var name in new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" })
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            header.Add(new Label
            {
                Text = name,
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#8A847A")
            });
        }

        MonthHost.Children.Add(header);

        var grid = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        for (var col = 0; col < 7; col++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var row = 0; row < 6; row++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var i = 0; i < _vm.MonthDays.Count; i++)
        {
            var day = _vm.MonthDays[i];
            var cell = BuildMonthCell(day);
            grid.Add(cell, i % 7, i / 7);
        }

        MonthHost.Children.Add(grid);
    }

    private View BuildMonthCell(MonthDay day)
    {
        var ink = day.IsSelected ? Color.FromArgb("#F3F0E8") : Color.FromArgb("#1A1A1A");
        var muted = day.IsSelected ? Color.FromArgb("#C8C2B8") : Color.FromArgb("#8A847A");
        var background = day.IsSelected
            ? Color.FromArgb("#1A1A1A")
            : day.IsToday ? Color.FromArgb("#F7E7C1") : Color.FromArgb("#FFFFFF");

        var stack = new VerticalStackLayout { Spacing = 2 };
        stack.Add(new Label
        {
            Text = day.DayNumber,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = ink,
            Opacity = day.InMonth ? 1 : 0.35
        });
        if (day.HasChip1)
            stack.Add(Chip(day.Chip1, muted, day.InMonth));
        if (day.HasChip2)
            stack.Add(Chip(day.Chip2, muted, day.InMonth));
        if (day.HasMore)
            stack.Add(new Label
            {
                Text = day.More,
                FontSize = 9,
                TextColor = Color.FromArgb("#9A7A1F"),
                Opacity = day.InMonth ? 1 : 0.35
            });

        var border = new Border
        {
            Padding = new Thickness(4, 6),
            StrokeThickness = day.IsToday && !day.IsSelected ? 1.4 : 0,
            Stroke = Color.FromArgb("#C4A035"),
            BackgroundColor = background,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = stack,
            MinimumHeightRequest = 72
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _vm.SelectDayCommand.Execute(day);
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private static Label Chip(string text, Color color, bool inMonth) => new()
    {
        Text = text,
        FontSize = 8,
        TextColor = color,
        LineBreakMode = LineBreakMode.TailTruncation,
        MaxLines = 1,
        Opacity = inMonth ? 1 : 0.35
    };

    private void PaintDay()
    {
        DayHost.Children.Clear();
        if (!_vm.ShowDay)
            return;

        const int startHour = 7;
        const int endHour = 19;
        const double rowHeight = 32;
        var slots = (endHour - startHour) * 2;
        var windowStart = _vm.Anchor.Date.AddHours(startHour);
        var windowEnd = _vm.Anchor.Date.AddHours(endHour);
        var placed = PlaceTracks(_vm.DayEntries);
        var tracks = Math.Max(1, placed.Count == 0 ? 1 : placed.Max(p => p.Track) + 1);

        var grid = new Grid { ColumnSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        for (var track = 0; track < tracks; track++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var slot = 0; slot < slots; slot++)
            grid.RowDefinitions.Add(new RowDefinition(rowHeight));

        for (var slot = 0; slot < slots; slot += 2)
        {
            var hour = startHour + slot / 2;
            grid.Add(new Label
            {
                Text = $"{hour:00}:00",
                FontSize = 10,
                TextColor = Color.FromArgb("#8A847A"),
                VerticalOptions = LayoutOptions.Start
            }, 0, slot);
            grid.Add(new BoxView
            {
                HeightRequest = 1,
                Color = Color.FromArgb("#E4E0D8"),
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Fill
            }, 1, slot);
            grid.SetColumnSpan(grid.Children[^1], tracks);
        }

        if (_vm.Anchor.Date == DateTime.Today)
        {
            var nowRow = RowFor(DateTime.Now, windowStart, slots);
            if (nowRow >= 0)
            {
                var nowLine = new BoxView
                {
                    HeightRequest = 2,
                    Color = Color.FromArgb("#C4A035"),
                    VerticalOptions = LayoutOptions.Start
                };
                grid.Add(nowLine, 1, nowRow);
                grid.SetColumnSpan(nowLine, tracks);
            }
        }

        foreach (var item in placed)
        {
            var start = item.Entry.Booking.Start < windowStart ? windowStart : item.Entry.Booking.Start;
            var end = item.Entry.Booking.End > windowEnd ? windowEnd : item.Entry.Booking.End;
            var row = RowFor(start, windowStart, slots);
            var endRow = RowFor(end.AddMinutes(-1), windowStart, slots);
            if (row < 0) continue;
            var span = Math.Max(1, endRow - row + 1);

            var card = new Border
            {
                Margin = new Thickness(1, 1),
                Padding = new Thickness(6, 4),
                StrokeThickness = 0,
                BackgroundColor = Tint(item.Entry.Status),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 0,
                    Children =
                    {
                        new Label { Text = item.Entry.TimeLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1A1A") },
                        new Label { Text = item.Entry.Title, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1A1A"), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 },
                        new Label { Text = item.Entry.Person, FontSize = 10, TextColor = Color.FromArgb("#5A554D"), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 },
                        new Label { Text = item.Entry.Place, FontSize = 10, TextColor = Color.FromArgb("#5A554D"), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                    }
                }
            };
            var entry = item.Entry;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await _vm.OpenCommand.ExecuteAsync(entry);
            card.GestureRecognizers.Add(tap);
            grid.Add(card, item.Track + 1, row);
            grid.SetRowSpan(card, span);
        }

        DayHost.Children.Add(grid);
        if (_vm.DayEntries.Count == 0 && !_vm.HasOutsideHours)
        {
            DayHost.Children.Add(new Label
            {
                Text = "No bookings between 07:00 and 19:00.",
                FontSize = 13,
                TextColor = Color.FromArgb("#8A847A"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
    }

    private static int RowFor(DateTime time, DateTime windowStart, int slots)
    {
        var index = (int)Math.Floor((time - windowStart).TotalMinutes / 30);
        if (index < 0 || index >= slots) return -1;
        return index;
    }

    private static List<(CalendarEntry Entry, int Track)> PlaceTracks(IReadOnlyList<CalendarEntry> entries)
    {
        var ordered = entries.OrderBy(e => e.Booking.Start).ThenBy(e => e.Booking.End).ToList();
        var trackEnds = new List<DateTime>();
        var placed = new List<(CalendarEntry, int)>();
        foreach (var entry in ordered)
        {
            var track = trackEnds.FindIndex(end => end <= entry.Booking.Start);
            if (track < 0)
            {
                track = trackEnds.Count;
                trackEnds.Add(entry.Booking.End);
            }
            else
            {
                trackEnds[track] = entry.Booking.End;
            }

            placed.Add((entry, track));
        }

        return placed;
    }

    private static Color Tint(Models.BookingStatus status) => status switch
    {
        Models.BookingStatus.Confirmed => Color.FromArgb("#D9EFE3"),
        Models.BookingStatus.Pending => Color.FromArgb("#F7E7C1"),
        Models.BookingStatus.Cancelled => Color.FromArgb("#F6D6D6"),
        _ => Color.FromArgb("#E7E4DE")
    };
}
