using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class CalendarViewModel(IAuthService auth, IBookingService bookings, INotificationService notifications) : ObservableObject
{
    private readonly List<Booking> _scope = [];
    private bool _isLoading;
    private bool _watching;
    private bool _pending;
    private int _seenUnread = -1;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool showDay;
    [ObservableProperty] private bool showWeek;
    [ObservableProperty] private bool showMonth = true;
    [ObservableProperty] private bool showTimeline;
    [ObservableProperty] private bool hasLocationFilters;
    [ObservableProperty] private bool hasVisible;
    [ObservableProperty] private bool hasAgenda;
    [ObservableProperty] private bool hasOutsideHours;
    [ObservableProperty] private string mode = "Month";
    [ObservableProperty] private string rangeTitle = string.Empty;
    [ObservableProperty] private string scopeLabel = string.Empty;
    [ObservableProperty] private string countLabel = string.Empty;
    [ObservableProperty] private string emptyMessage = "Nothing in this range.";
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string agendaTitle = string.Empty;
    [ObservableProperty] private int layoutVersion;
    [ObservableProperty] private DateTime anchor = DateTime.Today;

    public ObservableCollection<CalendarFilterChip> ModeFilters { get; } = [];
    public ObservableCollection<CalendarFilterChip> LocationFilters { get; } = [];
    public ObservableCollection<CalendarWeekColumn> WeekColumns { get; } = [];
    public ObservableCollection<CalendarTimelineRow> TimelineRows { get; } = [];
    public ObservableCollection<CalendarEntry> AgendaEntries { get; } = [];
    public ObservableCollection<CalendarEntry> DayOutside { get; } = [];

    public IReadOnlyList<MonthDay> MonthDays { get; private set; } = [];
    public IReadOnlyList<CalendarEntry> DayEntries { get; private set; } = [];

    public void Watch(bool watch)
    {
        if (watch == _watching)
            return;

        if (watch)
        {
            BookingFeed.Changed += OnBookingsChanged;
            notifications.UnreadCountChanged += OnUnreadChanged;
        }
        else
        {
            BookingFeed.Changed -= OnBookingsChanged;
            notifications.UnreadCountChanged -= OnUnreadChanged;
        }

        _watching = watch;
    }

    private void OnBookingsChanged()
    {
        if (_watching)
            _ = ReloadAsync(showBusy: false);
    }

    private void OnUnreadChanged(int count)
    {
        if (!_watching || count == _seenUnread)
            return;

        _seenUnread = count;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_watching)
                _ = ReloadAsync(showBusy: false);
        });
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        EnsureModes();
        await ReloadAsync(showBusy: true);
    }

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync(showBusy: true);

    partial void OnSearchTextChanged(string value)
    {
        if (!_isLoading)
            Apply();
    }

    [RelayCommand]
    private void SelectMode(CalendarFilterChip? chip)
    {
        if (chip is null) return;
        foreach (var item in ModeFilters)
            item.IsSelected = item == chip;
        Mode = chip.Key;
        ShowDay = Mode == "Day";
        ShowWeek = Mode == "Week";
        ShowMonth = Mode == "Month";
        ShowTimeline = Mode == "Timeline";
        Apply();
    }

    [RelayCommand]
    private void SelectLocation(CalendarFilterChip? chip)
    {
        if (chip is null) return;
        foreach (var item in LocationFilters)
            item.IsSelected = item == chip;
        Apply();
    }

    [RelayCommand]
    private void Previous()
    {
        Anchor = Mode switch
        {
            "Day" => Anchor.AddDays(-1),
            "Week" => Anchor.AddDays(-7),
            _ => Anchor.AddMonths(-1)
        };
        Apply();
    }

    [RelayCommand]
    private void Next()
    {
        Anchor = Mode switch
        {
            "Day" => Anchor.AddDays(1),
            "Week" => Anchor.AddDays(7),
            _ => Anchor.AddMonths(1)
        };
        Apply();
    }

    [RelayCommand]
    private void Today()
    {
        Anchor = DateTime.Today;
        Apply();
    }

    [RelayCommand]
    private void SelectDay(MonthDay? day)
    {
        if (day is null) return;
        Anchor = day.Date.Date;
        Apply();
    }

    [RelayCommand]
    private async Task OpenAsync(CalendarEntry? entry)
    {
        if (entry is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={entry.Booking.Id}");
    }

    private async Task ReloadAsync(bool showBusy)
    {
        if (_isLoading)
        {
            _pending = true;
            return;
        }

        if (showBusy)
            IsBusy = true;
        _isLoading = true;
        try
        {
            var user = auth.CurrentUser;
            HasAccess = user is not null && !user.IsGuest;
            _scope.Clear();
            if (!HasAccess)
            {
                ScopeLabel = "Sign in to see bookings on the calendar.";
                Apply();
                return;
            }

            IReadOnlyList<Booking> list;
            if (user!.Role == UserRole.Administrator)
            {
                ScopeLabel = "All bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }
            else if (user.Role == UserRole.CentreManager)
            {
                ScopeLabel = "Staff and client bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }
            else
            {
                ScopeLabel = "Your bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }

            _scope.AddRange(list);
            RebuildLocationFilters();
            Apply();
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }

        if (_pending && _watching)
        {
            _pending = false;
            await ReloadAsync(showBusy: false);
        }
    }

    private void EnsureModes()
    {
        if (ModeFilters.Count > 0) return;
        ModeFilters.Add(new CalendarFilterChip { Key = "Day", Name = "Day" });
        ModeFilters.Add(new CalendarFilterChip { Key = "Week", Name = "Week" });
        ModeFilters.Add(new CalendarFilterChip { Key = "Month", Name = "Month", IsSelected = true });
        ModeFilters.Add(new CalendarFilterChip { Key = "Timeline", Name = "Timeline" });
    }

    private void RebuildLocationFilters()
    {
        var selected = LocationFilters.FirstOrDefault(c => c.IsSelected)?.Key ?? string.Empty;
        LocationFilters.Clear();
        var locations = _scope
            .Select(b => (Id: b.LocationId, Name: string.IsNullOrWhiteSpace(b.LocationName) ? b.LocationId : b.LocationName))
            .Where(l => !string.IsNullOrWhiteSpace(l.Id))
            .GroupBy(l => l.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(l => l.Name)
            .ToList();

        HasLocationFilters = locations.Count > 1;
        if (!HasLocationFilters)
            return;

        LocationFilters.Add(new CalendarFilterChip { Key = string.Empty, Name = "All", IsSelected = string.IsNullOrEmpty(selected) });
        foreach (var location in locations)
        {
            LocationFilters.Add(new CalendarFilterChip
            {
                Key = location.Id,
                Name = location.Name,
                IsSelected = string.Equals(location.Id, selected, StringComparison.OrdinalIgnoreCase)
            });
        }

        if (LocationFilters.All(c => !c.IsSelected))
            LocationFilters[0].IsSelected = true;
    }

    private void Apply()
    {
        var filtered = Filter(_scope).ToList();
        var day = Anchor.Date;
        var weekStart = StartOfWeek(day);
        var monthStart = new DateTime(day.Year, day.Month, 1);

        RangeTitle = Mode switch
        {
            "Day" => day.ToString("dddd d MMMM yyyy"),
            "Week" => $"{weekStart:d MMM} – {weekStart.AddDays(6):d MMM yyyy}",
            _ => monthStart.ToString("MMMM yyyy")
        };

        var inRange = filtered.Where(b => ModeRange(b, day, weekStart, monthStart)).OrderBy(b => b.Start).ToList();
        CountLabel = inRange.Count == 1 ? "1 booking" : $"{inRange.Count} bookings";
        HasVisible = inRange.Count > 0;
        EmptyMessage = _scope.Count == 0
            ? "No bookings in this account yet."
            : "Nothing in this range matches the search or centre.";

        BuildMonth(filtered, monthStart, day);
        BuildWeek(filtered, weekStart, day);
        BuildDay(filtered, day);
        BuildTimeline(inRange);
        if (!ShowDay)
            HasOutsideHours = false;
        LayoutVersion++;
    }

    private IEnumerable<Booking> Filter(IEnumerable<Booking> source)
    {
        var query = SearchText.Trim();
        var location = LocationFilters.FirstOrDefault(c => c.IsSelected)?.Key ?? string.Empty;
        var filtered = source;
        if (!string.IsNullOrEmpty(location))
            filtered = filtered.Where(b => string.Equals(b.LocationId, location, StringComparison.OrdinalIgnoreCase));
        if (query.Length == 0)
            return filtered;

        return filtered.Where(b =>
            Contains(b.RoomName, query) ||
            Contains(b.LocationName, query) ||
            Contains(b.BookerName, query) ||
            Contains(b.Company, query) ||
            Contains(b.Status.ToString(), query) ||
            b.Equipment.Any(e => Contains(e, query)) ||
            Contains(b.Notes, query));
    }

    private bool ModeRange(Booking booking, DateTime day, DateTime weekStart, DateTime monthStart)
    {
        var start = Mode switch
        {
            "Day" => day,
            "Week" => weekStart,
            _ => monthStart
        };
        var end = Mode switch
        {
            "Day" => day.AddDays(1),
            "Week" => weekStart.AddDays(7),
            _ => monthStart.AddMonths(1)
        };
        return booking.Start < end && booking.End > start;
    }

    private void BuildMonth(IReadOnlyList<Booking> filtered, DateTime monthStart, DateTime selected)
    {
        var offset = ((int)monthStart.DayOfWeek + 6) % 7;
        var cursor = monthStart.AddDays(-offset);
        var days = new List<MonthDay>(42);
        for (var i = 0; i < 42; i++)
        {
            var date = cursor.AddDays(i);
            var entries = EntriesOn(filtered, date);
            days.Add(new MonthDay
            {
                Date = date,
                DayNumber = date.Day.ToString(),
                InMonth = date.Month == monthStart.Month,
                IsToday = date.Date == DateTime.Today,
                IsSelected = date.Date == selected,
                Chip1 = entries.Count > 0 ? entries[0].Chip : string.Empty,
                Chip2 = entries.Count > 1 ? entries[1].Chip : string.Empty,
                More = entries.Count > 2 ? $"+{entries.Count - 2}" : string.Empty
            });
        }

        MonthDays = days;
        var agenda = EntriesOn(filtered, selected);
        AgendaEntries.Clear();
        foreach (var entry in agenda)
            AgendaEntries.Add(entry);
        HasAgenda = agenda.Count > 0;
        AgendaTitle = agenda.Count == 1
            ? $"1 booking on {selected:ddd d MMM}"
            : $"{agenda.Count} bookings on {selected:ddd d MMM}";
    }

    private void BuildWeek(IReadOnlyList<Booking> filtered, DateTime weekStart, DateTime selected)
    {
        WeekColumns.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var entries = EntriesOn(filtered, date);
            WeekColumns.Add(new CalendarWeekColumn
            {
                Date = date,
                Weekday = date.ToString("ddd"),
                DayNumber = date.Day.ToString(),
                IsToday = date.Date == DateTime.Today,
                IsAnchor = date.Date == selected,
                HasEntries = entries.Count > 0,
                Entries = entries
            });
        }
    }

    private void BuildDay(IReadOnlyList<Booking> filtered, DateTime day)
    {
        var windowStart = day.AddHours(7);
        var windowEnd = day.AddHours(19);
        var entries = EntriesOn(filtered, day);
        DayEntries = entries.Where(e => e.Booking.Start < windowEnd && e.Booking.End > windowStart).ToList();
        DayOutside.Clear();
        foreach (var entry in entries.Where(e => e.Booking.End <= windowStart || e.Booking.Start >= windowEnd))
            DayOutside.Add(entry);
        HasOutsideHours = DayOutside.Count > 0;
    }

    private void BuildTimeline(IReadOnlyList<Booking> inRange)
    {
        TimelineRows.Clear();
        DateTime? last = null;
        foreach (var booking in inRange)
        {
            var date = booking.Start.Date;
            TimelineRows.Add(new CalendarTimelineRow
            {
                ShowDate = last != date,
                DateLabel = date.ToString("dddd d MMMM"),
                Entry = new CalendarEntry(booking)
            });
            last = date;
        }
    }

    private static List<CalendarEntry> EntriesOn(IReadOnlyList<Booking> filtered, DateTime day) =>
        filtered
            .Where(b => b.Start < day.Date.AddDays(1) && b.End > day.Date)
            .OrderBy(b => b.Start)
            .Select(b => new CalendarEntry(b))
            .ToList();

    private static DateTime StartOfWeek(DateTime day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.Date.AddDays(-offset);
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}

public partial class CalendarFilterChip : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool isSelected;
}

public sealed class MonthDay
{
    public DateTime Date { get; init; }
    public string DayNumber { get; init; } = string.Empty;
    public bool InMonth { get; init; }
    public bool IsToday { get; init; }
    public bool IsSelected { get; init; }
    public string Chip1 { get; init; } = string.Empty;
    public string Chip2 { get; init; } = string.Empty;
    public string More { get; init; } = string.Empty;
    public bool HasChip1 => Chip1.Length > 0;
    public bool HasChip2 => Chip2.Length > 0;
    public bool HasMore => More.Length > 0;
}

public sealed class CalendarWeekColumn
{
    public DateTime Date { get; init; }
    public string Weekday { get; init; } = string.Empty;
    public string DayNumber { get; init; } = string.Empty;
    public bool IsToday { get; init; }
    public bool IsAnchor { get; init; }
    public bool HasEntries { get; init; }
    public IReadOnlyList<CalendarEntry> Entries { get; init; } = [];
}

public sealed class CalendarTimelineRow
{
    public bool ShowDate { get; init; }
    public string DateLabel { get; init; } = string.Empty;
    public CalendarEntry Entry { get; init; } = null!;
}

public sealed class CalendarEntry(Booking booking)
{
    public Booking Booking { get; } = booking;
    public string TimeLabel => $"{Booking.Start:HH:mm}–{Booking.End:HH:mm}";
    public string Title => string.IsNullOrWhiteSpace(Booking.RoomName) ? "Room" : Booking.RoomName;
    public string Person => string.IsNullOrWhiteSpace(Booking.BookerName) ? "Booker" : Booking.BookerName;
    public string Place => string.IsNullOrWhiteSpace(Booking.LocationName) ? Booking.LocationId : Booking.LocationName;
    public string Company => string.IsNullOrWhiteSpace(Booking.Company) ? string.Empty : Booking.Company;
    public string AttendeesLabel => Booking.Attendees > 0 ? $"{Booking.Attendees} people" : string.Empty;
    public string EquipmentLabel => Booking.Equipment.Count == 0 ? string.Empty : string.Join(", ", Booking.Equipment);
    public string NotesLabel => string.IsNullOrWhiteSpace(Booking.Notes) ? string.Empty : Booking.Notes.Trim();
    public string StatusLabel => Booking.Status.ToString();
    public BookingStatus Status => Booking.Status;

    public string DurationLabel
    {
        get
        {
            var mins = (int)Math.Max(0, (Booking.End - Booking.Start).TotalMinutes);
            if (mins >= 60 && mins % 60 == 0)
                return $"{mins / 60} h";
            return $"{mins} min";
        }
    }

    public string Chip
    {
        get
        {
            var room = Title.Length > 14 ? Title[..14] : Title;
            return $"{Booking.Start:HH:mm} {room}";
        }
    }

    public string Facts
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Place)) parts.Add(Place);
            if (!string.IsNullOrWhiteSpace(Person)) parts.Add(Person);
            if (!string.IsNullOrWhiteSpace(Company)) parts.Add(Company);
            if (!string.IsNullOrWhiteSpace(AttendeesLabel)) parts.Add(AttendeesLabel);
            parts.Add(DurationLabel);
            return string.Join(" · ", parts);
        }
    }
}
