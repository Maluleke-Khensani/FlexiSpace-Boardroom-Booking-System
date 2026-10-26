using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.ViewModels;

public partial class MyBookingsViewModel(IAuthService auth, IBookingService bookings, INotificationService notifications, IRoomService rooms) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool showMineOnly = true;
    [ObservableProperty] private bool canToggleScope;
    [ObservableProperty] private bool hasLocationFilters;
    [ObservableProperty] private bool hasRoomFilters;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string filterLabel = "Showing: my bookings";
    [ObservableProperty] private string emptyMessage = "You have no bookings yet. Use the Book tab to reserve a room.";

    public ObservableCollection<Booking> Items { get; } = [];
    public ObservableCollection<LocationFilterChip> LocationFilters { get; } = [];
    public ObservableCollection<LocationFilterChip> RoomFilters { get; } = [];

    private readonly List<Booking> _all = [];
    private readonly List<Boardroom> _catalogueRooms = [];
    private readonly List<OfficeLocation> _catalogueLocations = [];
    private string? _locationId;
    private string? _roomId;
    private string? _roomName;
    private bool _loading;

    private bool _watching;
    private bool _refreshing;
    private bool _pending;
    private int _seenUnread = -1;

    /// <summary>
    /// While Mine is open, reload when this device saves a booking or when
    /// someone else's alert arrives, so the list matches confirmation.
    /// </summary>
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
            _ = RefreshAsync(showBusy: false);
    }

    private void OnUnreadChanged(int count)
    {
        if (!_watching || count == _seenUnread)
            return;

        _seenUnread = count;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_watching)
                _ = RefreshAsync(showBusy: false);
        });
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        CanToggleScope = auth.CurrentUser?.Role == UserRole.Administrator;
        ShowMineOnly = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ShowMineAsync()
    {
        if (ShowMineOnly) return;
        ShowMineOnly = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        if (!CanToggleScope || !ShowMineOnly) return;
        ShowMineOnly = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshAsync(showBusy: true);

    private async Task RefreshAsync(bool showBusy)
    {
        if (_refreshing)
        {
            _pending = true;
            return;
        }

        _refreshing = true;
        _loading = true;
        if (showBusy)
            IsBusy = true;
        try
        {
            var user = auth.CurrentUser;
            IReadOnlyList<Booking> list;
            if (user?.Role == UserRole.Administrator && !ShowMineOnly)
            {
                FilterLabel = "Showing: all bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }
            else if (user?.Role == UserRole.CentreManager)
            {
                FilterLabel = "Showing: staff and client bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }
            else
            {
                // The API already returns only this person's bookings. Do not
                // drop a booking because the room is at another centre.
                FilterLabel = "Showing: my bookings";
                list = await bookings.GetBookingsForCurrentUserScopeAsync();
            }

            if (user?.Role == UserRole.Administrator && ShowMineOnly)
                FilterLabel = "Showing: my bookings";

            _all.Clear();
            _all.AddRange(list);
            await LoadCatalogueAsync();
            RebuildLocationFilters();
            RebuildRoomFilters();
            ApplyFilters();
        }
        finally
        {
            _loading = false;
            IsBusy = false;
            _refreshing = false;
        }

        if (_pending && _watching)
        {
            _pending = false;
            await RefreshAsync(showBusy: false);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!_loading)
            ApplyFilters();
    }

    [RelayCommand]
    private void SelectLocation(LocationFilterChip? chip)
    {
        if (chip is null)
            return;

        _locationId = string.IsNullOrEmpty(chip.LocationId) ? null : chip.LocationId;
        _roomId = null;
        _roomName = null;
        foreach (var item in LocationFilters)
            item.IsSelected = SameKey(item.LocationId, _locationId);
        RebuildRoomFilters();
        ApplyFilters();
    }

    [RelayCommand]
    private void SelectRoom(LocationFilterChip? chip)
    {
        if (chip is null)
            return;

        _roomId = string.IsNullOrEmpty(chip.LocationId) ? null : chip.LocationId;
        _roomName = _roomId is null ? null : chip.Name;
        foreach (var item in RoomFilters)
            item.IsSelected = SameKey(item.LocationId, _roomId);
        ApplyFilters();
    }

    private async Task LoadCatalogueAsync()
    {
        _catalogueLocations.Clear();
        _catalogueRooms.Clear();
        try
        {
            _catalogueLocations.AddRange(await rooms.GetLocationsAsync());
            _catalogueRooms.AddRange((await rooms.GetRoomsAsync()).Where(r => !r.IsCombined));
        }
        catch (Exception)
        {
            // Bookings still list if the room catalogue cannot be loaded.
        }
    }

    private void RebuildLocationFilters()
    {
        var centres = new List<(string Id, string Name)>();
        foreach (var location in _catalogueLocations.Where(l => !string.IsNullOrWhiteSpace(l.Id)))
            centres.Add((location.Id, string.IsNullOrWhiteSpace(location.Name) ? location.Id : location.Name));

        foreach (var group in _all.GroupBy(b => b.LocationId))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
                continue;
            if (centres.Any(c => SameKey(c.Id, group.Key)))
                continue;
            var name = group.Select(b => b.LocationName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            centres.Add((group.Key, string.IsNullOrWhiteSpace(name) ? group.Key : name!));
        }

        if (_locationId is not null && centres.All(c => !SameKey(c.Id, _locationId)))
            _locationId = null;

        LocationFilters.Clear();
        LocationFilters.Add(new LocationFilterChip
        {
            Name = "All",
            IsSelected = _locationId is null
        });
        foreach (var centre in centres.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            LocationFilters.Add(new LocationFilterChip
            {
                LocationId = centre.Id,
                Name = centre.Name,
                IsSelected = SameKey(centre.Id, _locationId)
            });
        }

        HasLocationFilters = centres.Count > 0;
    }

    private void RebuildRoomFilters()
    {
        RoomFilters.Clear();
        if (_locationId is null)
        {
            HasRoomFilters = false;
            _roomId = null;
            _roomName = null;
            return;
        }

        var roomChoices = new List<(string Id, string Name)>();
        foreach (var room in _catalogueRooms.Where(r => SameKey(r.LocationId, _locationId)))
            roomChoices.Add((room.Id, string.IsNullOrWhiteSpace(room.Name) ? room.Id : room.Name));

        foreach (var group in _all.Where(b => SameKey(b.LocationId, _locationId)).GroupBy(b => b.RoomId))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
                continue;
            if (roomChoices.Any(r => SameKey(r.Id, group.Key)))
                continue;
            var name = group.Select(b => b.RoomName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            roomChoices.Add((group.Key, string.IsNullOrWhiteSpace(name) ? group.Key : name!));
        }

        if (_roomId is not null && roomChoices.All(r => !SameKey(r.Id, _roomId)))
        {
            _roomId = null;
            _roomName = null;
        }

        RoomFilters.Add(new LocationFilterChip
        {
            Name = "All rooms",
            IsSelected = _roomId is null
        });
        foreach (var room in roomChoices.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            RoomFilters.Add(new LocationFilterChip
            {
                LocationId = room.Id,
                Name = room.Name,
                IsSelected = SameKey(room.Id, _roomId)
            });
        }

        HasRoomFilters = roomChoices.Count > 0;
    }

    private void ApplyFilters()
    {
        IEnumerable<Booking> query = _all;
        if (_locationId is not null)
            query = query.Where(b => SameKey(b.LocationId, _locationId));
        if (_roomId is not null)
        {
            query = query.Where(b =>
                SameKey(b.RoomId, _roomId) ||
                (_roomName is not null && string.Equals(b.RoomName, _roomName, StringComparison.OrdinalIgnoreCase)));
        }

        var term = SearchText.Trim();
        if (term.Length > 0)
        {
            query = query.Where(b =>
                Contains(b.RoomName, term) ||
                Contains(b.LocationName, term) ||
                Contains(b.BookerName, term) ||
                Contains(b.Company, term) ||
                Contains(b.Status.ToString(), term));
        }

        var now = DateTime.Now;
        var visible = query
            .OrderBy(b => b.End < now)
            .ThenBy(b => b.End < now ? (DateTime.MaxValue - b.Start).Ticks : b.Start.Ticks)
            .ToList();

        Items.Clear();
        foreach (var booking in visible)
            Items.Add(booking);
        IsEmpty = Items.Count == 0;

        var filtering = term.Length > 0 || _locationId is not null || _roomId is not null;
        EmptyMessage = _all.Count == 0 && !filtering
            ? "You have no bookings yet. Use the Book tab to reserve a room."
            : _locationId is not null || _roomId is not null
                ? "Nothing is booked here."
                : "No bookings match that search.";
    }

    private static bool SameKey(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private async Task OpenAsync(Booking? booking)
    {
        if (booking is null) return;
        await Shell.Current.GoToAsync($"BookingDetailPage?bookingId={booking.Id}");
    }
}

[QueryProperty(nameof(BookingId), "bookingId")]
public partial class BookingDetailViewModel(IAuthService auth, IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private bool canEditStatus;
    [ObservableProperty] private bool canCancelBooking;
    [ObservableProperty] private string attendeesText = "4";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime editDate = DateTime.Today;
    [ObservableProperty] private TimeSpan editStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan editEnd = new(10, 0, 0);
    [ObservableProperty] private BookingStatus selectedStatus = BookingStatus.Confirmed;

    /// <summary>Statuses a Centre Manager / Admin may set. Pending is not used.</summary>
    public BookingStatus[] EditableStatuses { get; } =
    [
        BookingStatus.Confirmed,
        BookingStatus.Cancelled,
        BookingStatus.Completed
    ];

    public string DurationLabel => Booking is null
        ? string.Empty
        : $"{Math.Max(0, (int)(Booking.End - Booking.Start).TotalMinutes)} min";

    public string WhenRange => Booking is null
        ? string.Empty
        : $"{Booking.Start:HH:mm}  –  {Booking.End:HH:mm}  ·  {DurationLabel}";

    public bool HasNotes => !string.IsNullOrWhiteSpace(Booking?.Notes);
    public bool HasOutlook => !string.IsNullOrWhiteSpace(Booking?.OutlookEventId);
    public bool HasExtraDetails => HasNotes || HasOutlook;
    public bool HasEquipment => Booking?.Equipment.Count > 0;
    public bool HasCatering => Booking?.Catering.Count > 0;

    partial void OnBookingIdChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id)) return;
        Booking = await bookings.GetBookingAsync(id);

        // The store mutates the same Booking instance in place (cancel/edit),
        // so the generated setter's reference-equality check would otherwise skip
        // raising PropertyChanged and the on-screen Status/Start/End labels would
        // silently go stale. Force a refresh every time we reload.
        OnPropertyChanged(nameof(Booking));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(WhenRange));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasOutlook));
        OnPropertyChanged(nameof(HasExtraDetails));
        OnPropertyChanged(nameof(HasEquipment));
        OnPropertyChanged(nameof(HasCatering));

        var user = auth.CurrentUser;
        if (Booking is null || user is null)
        {
            CanEdit = CanEditStatus = CanCancelBooking = false;
            return;
        }

        var inScope = user.Role == UserRole.Administrator ||
                      (user.Role == UserRole.CentreManager && user.LocationId == Booking.LocationId);

        CanEditStatus = RolePermissions.CanEditBookings(user.Role) && inScope;
        CanEdit = CanEditStatus && Booking.Status == BookingStatus.Confirmed;
        CanCancelBooking = Booking.BookerId == user.Id
            && RolePermissions.CanCancelOwnBookings(user.Role)
            && !RolePermissions.CanEditBookings(user.Role)
            && Booking.Status is BookingStatus.Confirmed or BookingStatus.Pending;

        ResetEditFields();
    }

    /// <summary>
    /// WinUI DatePicker/TimePicker/Entry keep values until blur. Call this from the
    /// page before Save so the VM has what is on screen.
    /// </summary>
    public void ApplyScheduleDraft(DateTime date, TimeSpan start, TimeSpan end, string attendees, string notes)
    {
        EditDate = date;
        EditStart = start;
        EditEnd = end;
        AttendeesText = attendees;
        Notes = notes;
    }

    private void ResetEditFields()
    {
        if (Booking is null) return;

        EditDate = Booking.Start.Date;
        EditStart = Booking.Start.TimeOfDay;
        EditEnd = Booking.End.TimeOfDay;
        AttendeesText = Booking.Attendees.ToString();
        Notes = Booking.Notes;
        SelectedStatus = Booking.Status is BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed
            ? Booking.Status
            : BookingStatus.Confirmed;
    }

    [RelayCommand]
    private async Task CancelEditsAsync()
    {
        if (Booking is null) return;
        ResetEditFields();
        Message = null;
        await ActionFeedback.InfoAsync("Your unsaved edits were discarded. The booking is unchanged.", "Edits cancelled");
    }

    [RelayCommand]
    private async Task SaveEditsAsync()
    {
        if (Booking is null || auth.CurrentUser?.Role is UserRole.Staff or UserRole.Client) return;
        if (EditDate.Date < DateTime.Today)
        {
            await ActionFeedback.FailAsync("Bookings cannot be moved to a date before today.");
            return;
        }

        if (EditEnd <= EditStart)
        {
            await ActionFeedback.FailAsync("End time must be after start time.");
            return;
        }

        if (!int.TryParse(AttendeesText, out var attendees) || attendees < 1)
        {
            await ActionFeedback.FailAsync("Enter a valid number of attendees.");
            return;
        }

        var ok = await bookings.UpdateBookingAsync(
            Booking.Id,
            EditDate.Date + EditStart,
            EditDate.Date + EditEnd,
            attendees,
            Notes);
        await LoadAsync();
        Message = null;
        if (ok)
            await ActionFeedback.SuccessAsync("Schedule, headcount, and notes are locked in.", "Booking updated");
        else
            await ActionFeedback.FailAsync("That time overlaps another booking, or you do not have permission.");
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.UpdateBookingStatusAsync(Booking.Id, SelectedStatus);
        await LoadAsync();
        Message = null;
        if (ok)
            await ActionFeedback.SuccessAsync($"Status is now {SelectedStatus}.", "Status saved");
        else
            await ActionFeedback.FailAsync("Could not update the status. Check your permissions and try again.");
    }

    [RelayCommand]
    private async Task CancelBookingAsync()
    {
        if (Booking is null || !CanCancelBooking) return;

        var confirmed = await ActionFeedback.ConfirmAsync(
            "Cancel this booking?",
            $"{Booking.RoomName} on {Booking.Start:ddd d MMM HH:mm} will be released. Book again if you need a different time or room.",
            "Cancel booking",
            "Keep it",
            destructive: true);
        if (!confirmed) return;

        var ok = await bookings.CancelBookingAsync(Booking.Id);
        await LoadAsync();
        if (ok)
            await ActionFeedback.SuccessAsync("The room is free again.", "Booking cancelled");
        else
            await ActionFeedback.FailAsync("This booking could not be cancelled.");
    }
}

[QueryProperty(nameof(BookingId), "bookingId")]
public partial class BookingConfirmationViewModel(IBookingService bookings) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private string roomName = string.Empty;
    [ObservableProperty] private string locationName = string.Empty;
    [ObservableProperty] private string whenLabel = string.Empty;
    [ObservableProperty] private string referenceLabel = string.Empty;
    [ObservableProperty] private string bookerLabel = string.Empty;
    [ObservableProperty] private string companyLabel = string.Empty;
    [ObservableProperty] private string attendeesLabel = string.Empty;
    [ObservableProperty] private string equipmentLabel = string.Empty;
    [ObservableProperty] private string cateringLabel = string.Empty;
    [ObservableProperty] private string notesLabel = string.Empty;
    [ObservableProperty] private string statusLabel = string.Empty;
    [ObservableProperty] private bool hasBooking;
    [ObservableProperty] private bool hasFacts;
    [ObservableProperty] private string missingLabel = string.Empty;

    public bool IsConfirmed => Booking is not null && Booking.Status == BookingStatus.Confirmed && HasBooking;

    partial void OnBookingIdChanged(string value) => _ = LoadAsync();

    partial void OnBookingChanged(Booking? value) => OnPropertyChanged(nameof(IsConfirmed));

    private async Task LoadAsync()
    {
        if (!Guid.TryParse(BookingId, out var id))
        {
            ShowMissing();
            return;
        }

        var group = BookingHandoff.CreatedWith(id);
        var ids = group.Count > 0
            ? group.Select(b => b.Id).Distinct().ToList()
            : new List<Guid> { id };

        var loaded = new List<Booking>();
        foreach (var bookingId in ids)
        {
            Booking? fromApi = null;
            try
            {
                fromApi = await bookings.GetBookingAsync(bookingId);
            }
            catch (Exception)
            {
                fromApi = null;
            }

            var saved = group.FirstOrDefault(b => b.Id == bookingId) ?? BookingHandoff.Peek(bookingId);
            var mergedOne = Merge(fromApi, saved);
            if (mergedOne is not null && mergedOne.Start.Year >= 2000)
                loaded.Add(mergedOne);
        }

        var merged = loaded.FirstOrDefault(b => b.Id == id) ?? loaded.FirstOrDefault();
        if (merged is null)
        {
            ShowMissing();
            return;
        }

        Booking = merged;
        HasBooking = true;
        MissingLabel = string.Empty;
        RoomName = string.Join(" · ", loaded
            .Select(b => b.RoomName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());
        LocationName = merged.LocationName;
        WhenLabel = $"{merged.Start:ddd d MMM yyyy · HH:mm} – {merged.End:HH:mm}";
        var numberText = string.Join(" · ", loaded
            .Select(b => IdAdapter.ToInt(b.Id))
            .Where(n => n > 0)
            .Distinct()
            .Select(n => $"#{n}"));
        ReferenceLabel = string.IsNullOrEmpty(numberText) ? string.Empty : $"Booking {numberText}";
        BookerLabel = merged.BookerName;
        CompanyLabel = merged.Company;
        AttendeesLabel = merged.Attendees > 0 ? merged.Attendees.ToString() : string.Empty;
        EquipmentLabel = merged.Equipment.Count == 0 ? string.Empty : string.Join(", ", merged.Equipment);
        CateringLabel = merged.Catering.Count == 0 ? string.Empty : string.Join(", ", merged.Catering);
        NotesLabel = merged.Notes;
        StatusLabel = loaded.Select(b => b.Status).Distinct().Count() == 1
            ? merged.Status.ToString()
            : string.Join(" · ", loaded.Select(b => b.Status.ToString()).Distinct());
        HasFacts = !string.IsNullOrWhiteSpace(BookerLabel)
                   || !string.IsNullOrWhiteSpace(CompanyLabel)
                   || !string.IsNullOrWhiteSpace(AttendeesLabel)
                   || !string.IsNullOrWhiteSpace(EquipmentLabel)
                   || !string.IsNullOrWhiteSpace(CateringLabel)
                   || !string.IsNullOrWhiteSpace(NotesLabel);
        OnPropertyChanged(nameof(IsConfirmed));
    }

    private void ShowMissing()
    {
        Booking = null;
        HasBooking = false;
        HasFacts = false;
        MissingLabel = "This booking could not be loaded. Open My bookings to see it.";
        OnPropertyChanged(nameof(IsConfirmed));
    }

    private static Booking? Merge(Booking? loaded, Booking? saved)
    {
        if (loaded is null)
            return saved;
        if (saved is null)
            return loaded;

        if (string.IsNullOrWhiteSpace(loaded.RoomName))
            loaded.RoomName = saved.RoomName;
        if (string.IsNullOrWhiteSpace(loaded.LocationName))
            loaded.LocationName = saved.LocationName;
        if (loaded.Start.Year < 2000)
        {
            loaded.Start = saved.Start;
            loaded.End = saved.End;
        }
        if (string.IsNullOrWhiteSpace(loaded.BookerName))
            loaded.BookerName = saved.BookerName;
        if (string.IsNullOrWhiteSpace(loaded.Company))
            loaded.Company = saved.Company;
        if (loaded.Attendees <= 0)
            loaded.Attendees = saved.Attendees;
        if (string.IsNullOrWhiteSpace(loaded.Notes))
            loaded.Notes = saved.Notes;
        if (loaded.Equipment.Count == 0)
            loaded.Equipment = saved.Equipment;
        if (loaded.Catering.Count == 0)
            loaded.Catering = saved.Catering;
        return loaded;
    }

    [RelayCommand]
    private async Task DoneAsync() => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    private async Task ViewBookingsAsync() => await Shell.Current.GoToAsync("//MyBookingsPage");

    [RelayCommand]
    private async Task ViewAlertsAsync() => await Shell.Current.GoToAsync("//NotificationsPage");
}
