using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

/// <summary>
/// Centre Manager / Administrator booking console — view and edit bookings in scope,
/// and block rooms. Room/user administration and reporting are deliberately not built
/// here: per the team's project plan, those are the React website's admin dashboard, and
/// mobile is scoped to core on-the-go booking features only.
/// </summary>
public partial class ManageViewModel(IAuthService auth, IBookingService bookings, IRoomService rooms) : ObservableObject
{
    private bool _isLoading;
    private bool _syncingMeetings;
    private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<Booking> _roomBookings = [];
    private List<BlockedPeriod> _roomBlocks = [];

    [ObservableProperty] private string title = "Manage";
    [ObservableProperty] private string subtitle = string.Empty;
    [ObservableProperty] private bool isCentreManager;
    [ObservableProperty] private bool isAdministrator;
    [ObservableProperty] private bool hasAccess;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? message;
    [ObservableProperty] private Boardroom? selectedRoomToBlock;
    [ObservableProperty] private MeetingOption? selectedMeetingOption;
    [ObservableProperty] private string blockReason = "Maintenance";
    [ObservableProperty] private DateTime blockDate = DateTime.Today;
    [ObservableProperty] private TimeSpan blockStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan blockEnd = new(12, 0, 0);
    [ObservableProperty] private int inScopeCount;
    [ObservableProperty] private bool hasLocationBookings;
    [ObservableProperty] private string monthTitle = DateTime.Today.ToString("MMMM yyyy");
    [ObservableProperty] private string dayMeetingsCaption = "Pick a room to see its day.";
    [ObservableProperty] private bool hasDayMeetings;
    [ObservableProperty] private bool hasDayBlocks;

    public ObservableCollection<Booking> LocationBookings { get; } = [];
    public ObservableCollection<Boardroom> Rooms { get; } = [];
    public ObservableCollection<MeetingOption> MeetingOptions { get; } = [];
    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = [];
    public ObservableCollection<Booking> DayMeetings { get; } = [];
    public ObservableCollection<BlockedPeriod> DayBlocks { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var user = auth.CurrentUser;
        HasAccess = user is not null && RolePermissions.CanAccessManageHub(user.Role);
        if (!HasAccess)
        {
            Title = "Manage";
            Subtitle = "Only Centre Managers and Administrators can open this console.";
            IsCentreManager = IsAdministrator = false;
            return;
        }

        IsCentreManager = user!.Role == UserRole.CentreManager;
        IsAdministrator = user.Role == UserRole.Administrator;
        Title = IsAdministrator ? "Admin console" : "Centre management";
        Subtitle = IsAdministrator
            ? "View and manage bookings across every location."
            : $"View and manage bookings for {user.LocationId ?? "your centre"}.";

        IsBusy = true;
        Message = null;
        _isLoading = true;
        try
        {
            var scopeLocation = IsAdministrator ? null : user.LocationId;
            var keepRoomId = SelectedRoomToBlock?.Id;
            Rooms.Clear();
            foreach (var room in await rooms.GetRoomsAsync(scopeLocation))
                Rooms.Add(room);
            SelectedRoomToBlock = Rooms.FirstOrDefault(r => r.Id == keepRoomId) ?? Rooms.FirstOrDefault();

            LocationBookings.Clear();
            foreach (var b in await bookings.GetBookingsForCurrentUserScopeAsync())
                LocationBookings.Add(b);

            InScopeCount = LocationBookings.Count;
            HasLocationBookings = InScopeCount > 0;
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }

        await RefreshRoomActivityAsync();
    }

    partial void OnSelectedRoomToBlockChanged(Boardroom? value)
    {
        if (_isLoading) return;
        _ = RefreshRoomActivityAsync();
    }

    partial void OnBlockDateChanged(DateTime value)
    {
        if (_isLoading) return;
        var month = new DateTime(value.Year, value.Month, 1);
        if (month != _calendarMonth)
        {
            _calendarMonth = month;
            RebuildCalendar();
        }
        else
        {
            foreach (var day in CalendarDays)
                day.IsSelected = day.Date.Date == value.Date;
        }

        RefreshDayMeetings();
        SyncMeetingPickerToDate();
    }

    partial void OnSelectedMeetingOptionChanged(MeetingOption? value)
    {
        if (_isLoading || _syncingMeetings) return;
        if (value?.Booking is not { } meeting) return;
        BlockDate = meeting.Start.Date;
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        _calendarMonth = _calendarMonth.AddMonths(-1);
        RebuildCalendar();
    }

    [RelayCommand]
    private void NextMonth()
    {
        _calendarMonth = _calendarMonth.AddMonths(1);
        RebuildCalendar();
    }

    [RelayCommand]
    private void SelectCalendarDay(CalendarDayItem? day)
    {
        if (day is null) return;
        BlockDate = day.Date.Date;
    }

    private async Task RefreshRoomActivityAsync()
    {
        _roomBookings = LocationBookings
            .Where(b => SelectedRoomToBlock is not null &&
                        b.RoomId == SelectedRoomToBlock.Id &&
                        b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.Start)
            .ToList();

        _roomBlocks = SelectedRoomToBlock is null
            ? []
            : [.. await bookings.GetBlockedPeriodsAsync(SelectedRoomToBlock.Id)];

        RebuildMeetingOptions();
        RebuildCalendar();
        RefreshDayMeetings();
    }

    private void RebuildMeetingOptions()
    {
        _syncingMeetings = true;
        MeetingOptions.Clear();
        MeetingOptions.Add(new MeetingOption { Label = "None — browse the calendar" });
        foreach (var booking in _roomBookings)
        {
            MeetingOptions.Add(new MeetingOption
            {
                Booking = booking,
                Label = $"{booking.Start:ddd d MMM · HH:mm}–{booking.End:HH:mm} · {booking.BookerName}"
            });
        }

        SelectedMeetingOption = MeetingOptions[0];
        _syncingMeetings = false;
    }

    private void RebuildCalendar()
    {
        MonthTitle = _calendarMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        var bookedDates = _roomBookings.Select(b => b.Start.Date)
            .Concat(_roomBlocks.Select(b => b.Start.Date))
            .ToHashSet();

        var offset = ((int)_calendarMonth.DayOfWeek + 6) % 7;
        var cursor = _calendarMonth.AddDays(-offset);

        CalendarDays.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = cursor.AddDays(i);
            CalendarDays.Add(new CalendarDayItem
            {
                Date = date,
                DayNumber = date.Day,
                IsCurrentMonth = date.Month == _calendarMonth.Month,
                IsToday = date.Date == DateTime.Today,
                IsSelected = date.Date == BlockDate.Date,
                HasMeetings = bookedDates.Contains(date.Date)
            });
        }
    }

    private void RefreshDayMeetings()
    {
        DayMeetings.Clear();
        foreach (var booking in _roomBookings.Where(b => b.Start.Date == BlockDate.Date))
            DayMeetings.Add(booking);

        DayBlocks.Clear();
        foreach (var block in _roomBlocks.Where(b => b.Start.Date == BlockDate.Date).OrderBy(b => b.Start))
            DayBlocks.Add(block);

        HasDayMeetings = DayMeetings.Count > 0;
        HasDayBlocks = DayBlocks.Count > 0;

        var roomName = SelectedRoomToBlock?.Name ?? "this room";
        if (HasDayMeetings)
        {
            DayMeetingsCaption = DayMeetings.Count == 1
                ? $"1 meeting on {BlockDate:ddd d MMM} · {roomName}"
                : $"{DayMeetings.Count} meetings on {BlockDate:ddd d MMM} · {roomName}";
        }
        else
        {
            DayMeetingsCaption = $"No meetings on {BlockDate:ddd d MMM} for {roomName}.";
        }
    }

    private void SyncMeetingPickerToDate()
    {
        if (SelectedMeetingOption?.Booking is { } meeting && meeting.Start.Date == BlockDate.Date)
            return;

        _syncingMeetings = true;
        SelectedMeetingOption = MeetingOptions.FirstOrDefault();
        _syncingMeetings = false;
    }

    [RelayCommand]
    private async Task CancelAsync(Booking? booking)
    {
        if (booking is null) return;
        var ok = await bookings.CancelBookingAsync(booking.Id);
        Message = ok ? "Booking cancelled." : "Could not cancel booking.";
        await AppearingAsync();
    }

    [RelayCommand]
    private async Task OpenBookingAsync(Booking? booking)
    {
        if (booking is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={booking.Id}");
    }

    [RelayCommand]
    private async Task BlockRoomAsync()
    {
        if (!HasAccess) return;
        if (SelectedRoomToBlock is null)
        {
            Message = "Select a room to block.";
            return;
        }

        if (BlockDate.Date < DateTime.Today)
        {
            Message = "A room cannot be blocked for a date before today.";
            return;
        }

        if (BlockEnd <= BlockStart)
        {
            Message = "Block end time must be after start time.";
            return;
        }

        var start = BlockDate.Date + BlockStart;
        var end = BlockDate.Date + BlockEnd;
        var reason = string.IsNullOrWhiteSpace(BlockReason) ? "Blocked by Centre Manager" : BlockReason.Trim();
        var relatedId = SelectedMeetingOption?.Booking?.Id;
        var result = await bookings.BlockRoomAsync(SelectedRoomToBlock.Id, start, end, reason, relatedId);
        await AppearingAsync();
        Message = result.Success
            ? $"{SelectedRoomToBlock.Name} blocked {start:g}–{end:t}."
            : result.Message;

        if (Shell.Current is not null)
            await Shell.Current.DisplayAlertAsync(result.Title, result.Message, "OK");
    }
}

public sealed class MeetingOption
{
    public Booking? Booking { get; init; }
    public string Label { get; init; } = string.Empty;
    public override string ToString() => Label;
}

public partial class CalendarDayItem : ObservableObject
{
    public DateTime Date { get; init; }
    public int DayNumber { get; init; }
    public bool IsCurrentMonth { get; init; }
    public bool IsToday { get; init; }

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool hasMeetings;

    public Color MarkerColor => IsSelected
        ? Color.FromArgb("#111111")
        : Color.FromArgb("#9A7A1F");

    public double MarkerOpacity => HasMeetings ? 1 : 0;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(MarkerColor));
}
