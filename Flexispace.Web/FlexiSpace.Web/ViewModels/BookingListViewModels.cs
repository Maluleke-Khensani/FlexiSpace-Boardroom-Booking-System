using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Api;
using Flexispace.Web.Helpers;
using Flexispace.Web.Services;

namespace Flexispace.Web.ViewModels;

internal static class BookingListQuery
{
    public static bool Matches(Booking booking, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        query = query.Trim().TrimStart('#');
        return MatchesBookingId(booking.Id, query) ||
               booking.RoomName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.LocationName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.BookerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.Company.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.Status.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.Notes.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               booking.Start.ToString("g").Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBookingId(Guid id, string query)
    {
        var bookingRef = DisplayHelpers.BookingRef(id);
        if (bookingRef.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
            id.ToString("N").Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var digits = query;
        if (digits.StartsWith("FS-", StringComparison.OrdinalIgnoreCase))
            digits = digits[3..];
        else if (digits.StartsWith("FS", StringComparison.OrdinalIgnoreCase))
            digits = digits[2..];

        return int.TryParse(digits, out var number) &&
               BitConverter.ToInt32(id.ToByteArray(), 0) == number;
    }

    public static IEnumerable<Booking> Apply(IEnumerable<Booking> source, string query, string locationId, string roomId)
    {
        return source
            .Where(b => Matches(b, query))
            .Where(b => string.IsNullOrEmpty(locationId) || b.LocationId == locationId)
            .Where(b => string.IsNullOrEmpty(roomId) || b.RoomId == roomId)
            .OrderBy(b => b.LocationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.RoomName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Start);
    }
}

//Booking history list with optional scope filter (my bookings vs all in role scope).
public partial class MyBookingsViewModel(IAuthService auth, IBookingService bookings, IRoomService rooms, INavigationService nav) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool showMineOnly = true;
    [ObservableProperty] private bool canToggleScope;
    [ObservableProperty] private string filterLabel = "My bookings";
    [ObservableProperty] private bool isGuest;
    [ObservableProperty] private string filterQuery = string.Empty;
    [ObservableProperty] private string locationFilterId = string.Empty;
    [ObservableProperty] private string roomFilterId = string.Empty;

    private readonly List<Booking> _all = [];
    private readonly List<Boardroom> _rooms = [];

    public ObservableCollection<Booking> Items { get; } = [];
    public ObservableCollection<OfficeLocation> FilterLocations { get; } = [];
    public ObservableCollection<Boardroom> FilterRooms { get; } = [];

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsGuest = auth.IsGuest;
        var role = auth.CurrentUser?.Role;
        CanToggleScope = !IsGuest && role is UserRole.Administrator or UserRole.CentreManager;
        if (CanToggleScope)
            ShowMineOnly = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SetFilterAsync(bool mineOnly)
    {
        if (!CanToggleScope || ShowMineOnly == mineOnly) return;
        ShowMineOnly = mineOnly;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            FilterLabel = ShowMineOnly ? "Showing: my bookings" : "Showing: all in scope";
            if (auth.IsGuest)
            {
                _all.Clear();
                _rooms.Clear();
                Items.Clear();
                FilterLocations.Clear();
                FilterRooms.Clear();
                IsEmpty = true;
                return;
            }

            IReadOnlyList<Booking> list;
            if (ShowMineOnly)
                list = await bookings.GetBookingsAsync(userId: auth.CurrentUser?.Id);
            else
                list = await bookings.GetBookingsForCurrentUserScopeAsync();

            _all.Clear();
            _all.AddRange(list);
            _rooms.Clear();
            _rooms.AddRange(await rooms.GetRoomsAsync());
            IsEmpty = _all.Count == 0;
            RebuildLocationFilter();
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFilterQueryChanged(string value) => ApplyFilter();
    partial void OnRoomFilterIdChanged(string value) => ApplyFilter();
    partial void OnLocationFilterIdChanged(string value)
    {
        RebuildRoomFilter();
        ApplyFilter();
    }

    [RelayCommand]
    private void FilterLocation(OfficeLocation? location)
    {
        if (location is null) return;
        LocationFilterId = location.Id;
    }

    [RelayCommand]
    private void FilterRoom(Boardroom? room)
    {
        if (room is null) return;
        RoomFilterId = room.Id;
    }

    private void RebuildLocationFilter()
    {
        FilterLocations.Clear();
        var locations = _all
            .GroupBy(b => b.LocationId)
            .Select(g => new OfficeLocation { Id = g.Key, Name = g.First().LocationName })
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilterLocations.Add(new OfficeLocation { Id = string.Empty, Name = "All locations" });
        foreach (var loc in locations)
            FilterLocations.Add(loc);

        if (!string.IsNullOrEmpty(LocationFilterId) && FilterLocations.All(l => l.Id != LocationFilterId))
            LocationFilterId = string.Empty;

        RebuildRoomFilter();
    }

    private void RebuildRoomFilter()
    {
        FilterRooms.Clear();
        if (string.IsNullOrEmpty(LocationFilterId))
        {
            RoomFilterId = string.Empty;
            return;
        }

        var locationRooms = _rooms
            .Where(r => r.LocationId == LocationFilterId && !r.IsCombined)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase);

        FilterRooms.Add(new Boardroom { Id = string.Empty, Name = "All rooms" });
        foreach (var room in locationRooms)
            FilterRooms.Add(room);

        if (!string.IsNullOrEmpty(RoomFilterId) && FilterRooms.All(r => r.Id != RoomFilterId))
            RoomFilterId = string.Empty;
    }

    private void ApplyFilter()
    {
        Items.Clear();
        foreach (var booking in BookingListQuery.Apply(_all, FilterQuery, LocationFilterId, RoomFilterId))
            Items.Add(booking);
    }

    [RelayCommand]
    private void Open(Booking? booking)
    {
        if (booking is null) return;
        nav.NavigateTo($"/bookings/{booking.Id}");
    }
}

//Single booking detail: view, edit, cancel, approve, or decline based on the signed-in user's role.
public partial class BookingDetailViewModel(IAuthService auth, IBookingService bookings, INotificationService notifications) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isError;
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private bool notFound;
    [ObservableProperty] private bool canCancel;
    [ObservableProperty] private bool canApprove;
    [ObservableProperty] private bool canDecline;
    [ObservableProperty] private bool canEdit;
    [ObservableProperty] private string attendeesText = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private DateTime editDate = DateTime.Today;
    [ObservableProperty] private TimeSpan editStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan editEnd = new(10, 0, 0);
    [ObservableProperty] private bool isSaving;

    public async Task LoadByIdAsync(string id)
    {
        if (BookingId == id && Booking is not null && !IsLoading && !NotFound)
            return;

        BookingId = id;
        await LoadCoreAsync(clearMessage: true, showSpinner: true);
    }

    [RelayCommand]
    private Task LoadAsync() => LoadCoreAsync(clearMessage: false, showSpinner: false);

    private async Task LoadCoreAsync(bool clearMessage, bool showSpinner)
    {
        if (showSpinner)
            IsLoading = true;
        NotFound = false;
        if (clearMessage)
        {
            Message = null;
            IsError = false;
        }
        try
        {
            if (!Guid.TryParse(BookingId, out var id))
            {
                Booking = null;
                NotFound = true;
                CanCancel = CanApprove = CanDecline = CanEdit = false;
                return;
            }

            try
            {
                Booking = await bookings.GetBookingAsync(id);
            }
            catch
            {
                Booking = null;
            }

            OnPropertyChanged(nameof(Booking));
            if (Booking is null)
            {
                NotFound = true;
                CanCancel = CanApprove = CanDecline = CanEdit = false;
                return;
            }

            var user = auth.CurrentUser;
            if (user is null)
            {
                CanCancel = CanApprove = CanDecline = CanEdit = false;
                return;
            }

            var isOwner = Booking.BookerId == user.Id;
            var inScope = user.Role == UserRole.Administrator ||
                          (user.Role == UserRole.CentreManager &&
                           !string.IsNullOrEmpty(user.LocationId) &&
                           user.LocationId == Booking.LocationId);

            CanApprove = Booking.Status == BookingStatus.Pending &&
                         RolePermissions.CanApproveBookings(user.Role) && inScope;
            CanDecline = CanApprove;

            var active = Booking.Status is BookingStatus.Confirmed or BookingStatus.Pending;
            CanCancel = active && (
                (isOwner && RolePermissions.CanCancelOwnBookings(user.Role)) ||
                (RolePermissions.CanCancelAnyBooking(user.Role) && (inScope || user.Role == UserRole.Administrator)));

            CanEdit = Booking.Status != BookingStatus.Cancelled && (
                isOwner ||
                (RolePermissions.CanEditBookings(user.Role) && (inScope || user.Role == UserRole.Administrator)));

            EditDate = Booking.Start.Date;
            EditStart = Booking.Start.TimeOfDay;
            EditEnd = Booking.End.TimeOfDay;
            AttendeesText = Booking.Attendees.ToString();
            Notes = Booking.Notes;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (Booking is null || IsSaving) return;
        IsSaving = true;
        try
        {
            var result = await bookings.CancelBookingAsync(Booking.Id);
            if (result.Success)
            {
                try
                {
                    await notifications.AddAsync(new AppNotification
                    {
                        Title = "Booking cancelled",
                        Message = $"Your booking for {Booking.RoomName} on {Booking.Start:g} has been cancelled.",
                        Type = "Cancellation",
                        BookingId = Booking.Id
                    });
                }
                catch
                {
                }

                Booking.Status = BookingStatus.Cancelled;
                CanCancel = CanApprove = CanDecline = CanEdit = false;
                OnPropertyChanged(nameof(Booking));
            }

            Message = string.IsNullOrWhiteSpace(result.Message)
                ? (result.Success ? "Booking cancelled." : "You don't have permission to cancel this booking.")
                : result.Message;
            IsError = !result.Success;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.ApproveBookingAsync(Booking.Id);
        Message = ok ? "Booking approved." : "Unable to approve.";
        IsError = !ok;
        await LoadCoreAsync(clearMessage: false, showSpinner: false);
    }

    [RelayCommand]
    private async Task DeclineAsync()
    {
        if (Booking is null) return;
        var ok = await bookings.DeclineBookingAsync(Booking.Id);
        Message = ok ? "Booking declined." : "Unable to decline.";
        IsError = !ok;
        await LoadCoreAsync(clearMessage: false, showSpinner: false);
    }

    [RelayCommand]
    private async Task SaveEditsAsync()
    {
        if (Booking is null || IsSaving) return;
        IsSaving = true;
        try
        {
            _ = int.TryParse(AttendeesText, out var attendees);
            var start = EditDate.Date + EditStart;
            var end = EditDate.Date + EditEnd;
            var result = await bookings.UpdateBookingAsync(Booking.Id, start, end, attendees, Notes);
            if (result.Success)
            {
                try
                {
                    await notifications.AddAsync(new AppNotification
                    {
                        Title = "Booking updated",
                        Message = $"Your booking for {Booking.RoomName} was updated to {start:g}–{end:t}.",
                        Type = "Info",
                        BookingId = Booking.Id
                    });
                }
                catch
                {
                }

                Booking.Start = start;
                Booking.End = end;
                Booking.Attendees = Math.Max(1, attendees);
                Booking.Notes = Notes ?? string.Empty;
                OnPropertyChanged(nameof(Booking));
            }

            Message = string.IsNullOrWhiteSpace(result.Message)
                ? (result.Success ? "Booking updated." : "Update failed — check the times and try again.")
                : result.Message;
            IsError = !result.Success;
        }
        finally
        {
            IsSaving = false;
        }
    }
}

//Post-booking confirmation screen shown after a successful reservation is created.
public partial class BookingConfirmationViewModel(IBookingService bookings, INavigationService nav, BookingSessionCache session) : ObservableObject
{
    [ObservableProperty] private string bookingId = string.Empty;
    [ObservableProperty] private Booking? booking;
    [ObservableProperty] private bool isPending;
    [ObservableProperty] private bool isBusy;

    public async Task LoadAsync(string id)
    {
        BookingId = id;
        IsBusy = true;
        try
        {
            if (!Guid.TryParse(id, out var bookingId))
                return;

            Booking = session.LastCreated?.Id == bookingId
                ? session.LastCreated
                : await bookings.GetBookingAsync(bookingId);
            Booking ??= session.LastCreated;
            IsPending = Booking?.Status == BookingStatus.Pending;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Done() => nav.NavigateTo("/home");

    [RelayCommand]
    private void ViewBookings() => nav.NavigateTo("/my-bookings");

    [RelayCommand]
    private void ViewAlerts() => nav.NavigateTo("/notifications");
}
