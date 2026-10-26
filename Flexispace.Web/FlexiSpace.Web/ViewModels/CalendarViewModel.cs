using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

public enum CalendarViewKind
{
    Day,
    Week,
    Month,
    Timeline
}

public sealed class CalendarEvent
{
    public Guid? BookingId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string RoomId { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string Kind { get; init; } = "Booking";
    public string Color { get; init; } = "#1E8E5A";
    public string Subtitle => $"{Start:HH:mm}–{End:HH:mm} · {RoomName}";
}

public partial class CalendarViewModel(
    IAuthService auth,
    IBookingService bookings,
    IRoomService rooms,
    INavigationService nav) : ObservableObject
{
    private const int DayStartHour = 7;
    private const int DayEndHour = 19;

    [ObservableProperty] private CalendarViewKind view = CalendarViewKind.Week;
    [ObservableProperty] private DateTime anchorDate = DateTime.Today;
    [ObservableProperty] private string locationFilterId = string.Empty;
    [ObservableProperty] private string roomFilterId = string.Empty;
    [ObservableProperty] private int minCapacity;
    [ObservableProperty] private string equipmentFilter = string.Empty;
    [ObservableProperty] private bool lockLocationFilter;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasAccess = true;

    private readonly List<Booking> _bookings = [];
    private readonly List<BlockedPeriod> _blocks = [];
    private readonly List<Boardroom> _allRooms = [];

    public ObservableCollection<OfficeLocation> Locations { get; } = [];
    public ObservableCollection<Boardroom> FilterRooms { get; } = [];
    public ObservableCollection<string> EquipmentOptions { get; } = [];
    public ObservableCollection<CalendarEvent> Events { get; } = [];

    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(DayStartHour, DayEndHour - DayStartHour).ToList();

    public string RangeLabel => View switch
    {
        CalendarViewKind.Day => AnchorDate.ToString("dddd d MMMM yyyy"),
        CalendarViewKind.Week => $"{WeekStart:d MMM} – {WeekStart.AddDays(6):d MMM yyyy}",
        CalendarViewKind.Month => AnchorDate.ToString("MMMM yyyy"),
        _ => AnchorDate.ToString("dddd d MMMM yyyy")
    };

    public DateTime WeekStart
    {
        get
        {
            var delta = ((int)AnchorDate.DayOfWeek + 6) % 7;
            return AnchorDate.Date.AddDays(-delta);
        }
    }

    public IReadOnlyList<DateTime> WeekDays =>
        Enumerable.Range(0, 7).Select(i => WeekStart.AddDays(i)).ToList();

    public IReadOnlyList<DateTime> MonthCells
    {
        get
        {
            var first = new DateTime(AnchorDate.Year, AnchorDate.Month, 1);
            var start = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
            return Enumerable.Range(0, 42).Select(i => start.AddDays(i)).ToList();
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && !auth.IsGuest;
        if (!HasAccess)
            return;

        LockLocationFilter = user!.Role == UserRole.CentreManager && !string.IsNullOrEmpty(user.LocationId);

        Locations.Clear();
        if (!LockLocationFilter)
            Locations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });
        foreach (var loc in await rooms.GetLocationsAsync())
        {
            if (LockLocationFilter && loc.Id != user.LocationId) continue;
            Locations.Add(loc);
        }

        if (LockLocationFilter)
            LocationFilterId = user.LocationId!;

        _allRooms.Clear();
        _allRooms.AddRange(await rooms.GetRoomsAsync(LockLocationFilter ? user.LocationId : null));

        EquipmentOptions.Clear();
        EquipmentOptions.Add("Any equipment");
        foreach (var item in await rooms.GetEquipmentCatalogAsync())
            EquipmentOptions.Add(item);

        IsBusy = true;
        try
        {
            _bookings.Clear();
            _bookings.AddRange(await bookings.GetBookingsForCurrentUserScopeAsync());
            _blocks.Clear();
            if (RolePermissions.CanBlockRooms(user.Role))
                _blocks.AddRange(await bookings.GetBlockedPeriodsAsync());
        }
        finally
        {
            IsBusy = false;
        }

        RebuildRoomFilter();
        ApplyFilters();
    }

    partial void OnLocationFilterIdChanged(string value)
    {
        RoomFilterId = string.Empty;
        RebuildRoomFilter();
        ApplyFilters();
    }

    partial void OnRoomFilterIdChanged(string value) => ApplyFilters();
    partial void OnMinCapacityChanged(int value) => ApplyFilters();
    partial void OnEquipmentFilterChanged(string value) => ApplyFilters();
    partial void OnAnchorDateChanged(DateTime value) => ApplyFilters();
    partial void OnViewChanged(CalendarViewKind value) => ApplyFilters();

    [RelayCommand]
    private void SetView(CalendarViewKind kind) => View = kind;

    [RelayCommand]
    private void Today() => AnchorDate = DateTime.Today;

    [RelayCommand]
    private void Previous() => AnchorDate = View switch
    {
        CalendarViewKind.Month => AnchorDate.AddMonths(-1),
        CalendarViewKind.Week => AnchorDate.AddDays(-7),
        _ => AnchorDate.AddDays(-1)
    };

    [RelayCommand]
    private void Next() => AnchorDate = View switch
    {
        CalendarViewKind.Month => AnchorDate.AddMonths(1),
        CalendarViewKind.Week => AnchorDate.AddDays(7),
        _ => AnchorDate.AddDays(1)
    };

    [RelayCommand]
    private void OpenEvent(CalendarEvent? item)
    {
        if (item?.BookingId is { } id)
            nav.NavigateTo($"/bookings/{id}");
    }

    public IReadOnlyList<CalendarEvent> EventsOn(DateTime day) =>
        Events.Where(e => e.Start.Date == day.Date || (e.Start < day.Date.AddDays(1) && e.End > day.Date)).ToList();

    public IReadOnlyList<CalendarEvent> EventsForRoom(string roomId) =>
        Events.Where(e => e.RoomId == roomId && e.Start.Date == AnchorDate.Date).ToList();

    public static double TopPercent(DateTime start)
    {
        var minutes = ClampMinutes(start.TimeOfDay.TotalMinutes) - DayStartHour * 60;
        return minutes / ((DayEndHour - DayStartHour) * 60d) * 100;
    }

    public static double HeightPercent(DateTime start, DateTime end)
    {
        var from = ClampMinutes(start.TimeOfDay.TotalMinutes);
        var to = ClampMinutes(end.Date > start.Date ? DayEndHour * 60 : end.TimeOfDay.TotalMinutes);
        var span = Math.Max(30, to - from);
        return span / ((DayEndHour - DayStartHour) * 60d) * 100;
    }

    public bool IsInMonth(DateTime day) =>
        day.Month == AnchorDate.Month && day.Year == AnchorDate.Year;

    private static double ClampMinutes(double minutes) =>
        Math.Clamp(minutes, DayStartHour * 60, DayEndHour * 60);

    private void RebuildRoomFilter()
    {
        FilterRooms.Clear();
        FilterRooms.Add(new Boardroom { Id = string.Empty, Name = "All boardrooms" });
        foreach (var room in MatchingRooms())
            FilterRooms.Add(room);

        if (!string.IsNullOrEmpty(RoomFilterId) && FilterRooms.All(r => r.Id != RoomFilterId))
            RoomFilterId = string.Empty;
    }

    private IEnumerable<Boardroom> MatchingRooms()
    {
        IEnumerable<Boardroom> query = _allRooms.Where(r => !r.IsCombined);
        if (!string.IsNullOrEmpty(LocationFilterId))
            query = query.Where(r => r.LocationId == LocationFilterId);
        if (MinCapacity > 0)
            query = query.Where(r => r.Capacity >= MinCapacity);
        if (!string.IsNullOrEmpty(EquipmentFilter) && EquipmentFilter != "Any equipment")
            query = query.Where(r => r.Equipment.Contains(EquipmentFilter, StringComparer.OrdinalIgnoreCase));
        return query.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
    }

    private void ApplyFilters()
    {
        RebuildRoomFilter();
        var allowedRooms = MatchingRooms().Select(r => r.Id).ToHashSet();
        var (rangeStart, rangeEnd) = VisibleRange();

        Events.Clear();
        foreach (var booking in _bookings)
        {
            if (booking.Status == BookingStatus.Cancelled)
                continue;
            if (booking.End <= rangeStart || booking.Start >= rangeEnd)
                continue;
            if (!PassesFilters(booking.LocationId, booking.RoomId, allowedRooms))
                continue;

            Events.Add(new CalendarEvent
            {
                BookingId = booking.Id,
                Title = string.IsNullOrWhiteSpace(booking.Company) ? booking.RoomName : booking.Company,
                RoomId = booking.RoomId,
                RoomName = booking.RoomName,
                LocationName = booking.LocationName,
                Start = booking.Start,
                End = booking.End,
                Kind = "Booking",
                Color = StatusAccent.GetColor(booking.Status)
            });
        }

        foreach (var block in _blocks)
        {
            if (block.End <= rangeStart || block.Start >= rangeEnd)
                continue;
            if (!PassesFilters(block.LocationId, block.RoomId, allowedRooms))
                continue;

            Events.Add(new CalendarEvent
            {
                Title = string.IsNullOrWhiteSpace(block.Reason) ? "Blocked" : block.Reason,
                RoomId = block.RoomId,
                RoomName = block.RoomName,
                LocationName = block.LocationName,
                Start = block.Start,
                End = block.End,
                Kind = "Block",
                Color = RoomStatusPalette.GetColor("Blocked")
            });
        }
    }

    private bool PassesFilters(string locationId, string roomId, HashSet<string> allowedRooms)
    {
        if (!string.IsNullOrEmpty(LocationFilterId) && locationId != LocationFilterId)
            return false;
        if (!string.IsNullOrEmpty(RoomFilterId) && roomId != RoomFilterId)
            return false;
        if (allowedRooms.Count > 0 && !allowedRooms.Contains(roomId) &&
            (MinCapacity > 0 || (!string.IsNullOrEmpty(EquipmentFilter) && EquipmentFilter != "Any equipment")))
            return false;
        if (!string.IsNullOrEmpty(RoomFilterId))
            return roomId == RoomFilterId;
        return true;
    }

    private (DateTime Start, DateTime End) VisibleRange() => View switch
    {
        CalendarViewKind.Week => (WeekStart, WeekStart.AddDays(7)),
        CalendarViewKind.Month => (MonthCells[0], MonthCells[0].AddDays(42)),
        _ => (AnchorDate.Date, AnchorDate.Date.AddDays(1))
    };
}
