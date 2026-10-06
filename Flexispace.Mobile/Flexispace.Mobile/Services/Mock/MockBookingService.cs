using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Mock;

public class MockBookingService(
    MockDataStore store,
    IAuthService auth,
    IRoomService rooms,
    INotificationService notifications) : IBookingService
{
    public Task<IReadOnlyList<Booking>> GetBookingsAsync(string? locationId = null, Guid? userId = null, DateTime? day = null)
    {
        var query = store.Bookings.AsEnumerable();
        if (!string.IsNullOrEmpty(locationId))
            query = query.Where(b => b.LocationId == locationId);
        if (userId.HasValue)
            query = query.Where(b => b.BookerId == userId.Value);
        if (day.HasValue)
            query = query.Where(b => b.Start.Date == day.Value.Date);

        return Task.FromResult<IReadOnlyList<Booking>>(
            query.OrderBy(b => b.Start).ToList());
    }

    public Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null)
    {
        var user = auth.CurrentUser;
        if (user is null)
            return Task.FromResult<IReadOnlyList<Booking>>([]);

        return user.Role switch
        {
            UserRole.Administrator => GetBookingsAsync(day: day),
            UserRole.CentreManager => GetBookingsAsync(locationId: user.LocationId, day: day),
            _ => GetBookingsAsync(userId: user.Id, day: day)
        };
    }

    public Task<Booking?> GetBookingAsync(Guid bookingId) =>
        Task.FromResult(store.Bookings.FirstOrDefault(b => b.Id == bookingId));

    public Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync() =>
        GetBookingsForCurrentUserScopeAsync(DateTime.Today);

    public async Task<BookingResult> CreateBookingAsync(BookingRequest request)
    {
        if (auth.CurrentUser is null)
            return new BookingResult { Success = false, Message = "Please sign in to book a room." };

        if (!RolePermissions.CanBookRooms(auth.CurrentUser.Role))
            return new BookingResult { Success = false, Message = "Your role cannot create bookings." };

        if (request.Start.Date < DateTime.Today)
            return new BookingResult { Success = false, Message = "Bookings cannot be made for a date before today." };

        if (request.End <= request.Start)
            return new BookingResult { Success = false, Message = "End time must be after start time." };

        var room = await rooms.GetRoomAsync(request.RoomId);
        if (room is null)
            return new BookingResult { Success = false, Message = "Room not found." };

        // Centre managers only book / manage their own centre unless admin
        if (auth.CurrentUser.Role == UserRole.CentreManager &&
            !string.IsNullOrEmpty(auth.CurrentUser.LocationId) &&
            room.LocationId != auth.CurrentUser.LocationId)
        {
            return new BookingResult
            {
                Success = false,
                Message = "Centre Managers can only book rooms at their assigned location."
            };
        }

        if (store.BlockedPeriods.Any(b =>
                RoomCombinations.ConflictRoomIds(request.RoomId).Contains(b.RoomId) &&
                request.Start < b.End &&
                request.End > b.Start))
        {
            return new BookingResult { Success = false, Message = "This room is blocked for that period." };
        }

        var location = await rooms.GetLocationAsync(room.LocationId);
        var conflictIds = RoomCombinations.ConflictRoomIds(request.RoomId);
        var conflict = store.Bookings.Any(b =>
            conflictIds.Contains(b.RoomId) &&
            b.Status != BookingStatus.Cancelled &&
            request.Start < b.End &&
            request.End > b.Start);

        if (conflict)
        {
            var suggestions = SuggestSlots(request.RoomId, request.Start.Date, request.End - request.Start);
            return new BookingResult
            {
                Success = false,
                Message = room.IsCombined
                    ? "One of these adjoining rooms is already booked for that time. Try a suggested slot."
                    : "This room is already booked for that time. Try one of the suggested slots.",
                SuggestedSlots = suggestions
            };
        }

        var booking = new Booking
        {
            RoomId = room.Id,
            LocationId = room.LocationId,
            RoomName = room.Name,
            LocationName = location?.Name ?? room.LocationId,
            Start = request.Start,
            End = request.End,
            BookerId = auth.CurrentUser.Id,
            BookerName = auth.CurrentUser.Name,
            Company = string.IsNullOrWhiteSpace(request.Company) ? "Flexispace" : request.Company,
            Attendees = request.Attendees,
            Equipment = [.. request.Equipment],
            Catering = [.. request.Catering],
            Notes = request.Notes,
            // Always confirmed on create — there is no approval gate.
            Status = BookingStatus.Confirmed,
            OutlookEventId = $"mock-{Guid.NewGuid():N}"[..20]
        };

        store.Bookings.Add(booking);

        // Always drop an unread Alerts item the booker can tap open (links to this booking).
        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking confirmed",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}–{booking.End:HH:mm}. Tap to open.",
            Type = "Confirmation",
            CreatedAt = DateTime.Now,
            IsRead = false,
            BookingId = booking.Id
        });

        return new BookingResult
        {
            Success = true,
            Message = "Your boardroom is booked. Check Alerts for the confirmation.",
            Booking = booking
        };
    }

    public async Task<bool> CancelBookingAsync(Guid bookingId)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null) return false;

        var isOwner = booking.BookerId == user.Id;
        var canCancel =
            (isOwner && RolePermissions.CanCancelOwnBookings(user.Role)) ||
            (RolePermissions.CanCancelAnyBooking(user.Role) && IsInScope(user, booking));

        if (!canCancel) return false;

        booking.Status = BookingStatus.Cancelled;
        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking cancelled",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}",
            Type = "Cancellation",
            BookingId = booking.Id
        });
        return true;
    }

    public Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null) return Task.FromResult(false);
        if (!RolePermissions.CanEditBookings(user.Role) || !IsInScope(user, booking)) return Task.FromResult(false);
        if (end <= start) return Task.FromResult(false);
        if (start.Date < DateTime.Today) return Task.FromResult(false);

        var conflictIds = RoomCombinations.ConflictRoomIds(booking.RoomId);
        var conflict = store.Bookings.Any(b =>
            b.Id != bookingId &&
            conflictIds.Contains(b.RoomId) &&
            b.Status is BookingStatus.Confirmed or BookingStatus.Pending &&
            start < b.End && end > b.Start);
        if (conflict) return Task.FromResult(false);

        booking.Start = start;
        booking.End = end;
        booking.Attendees = Math.Max(1, attendees);
        booking.Notes = notes;
        return Task.FromResult(true);
    }

    public async Task<bool> UpdateBookingStatusAsync(Guid bookingId, BookingStatus status)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null) return false;
        if (!RolePermissions.CanEditBookings(user.Role) || !IsInScope(user, booking)) return false;

        // Pending is not used — bookings are confirmed on create.
        if (status is not (BookingStatus.Confirmed or BookingStatus.Cancelled or BookingStatus.Completed))
            return false;

        if (booking.Status == status) return true;

        booking.Status = status;
        var title = status switch
        {
            BookingStatus.Cancelled => "Booking cancelled",
            BookingStatus.Completed => "Booking completed",
            _ => "Booking status updated"
        };
        await notifications.AddAsync(new AppNotification
        {
            Title = title,
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm} is now {status}.",
            Type = status == BookingStatus.Cancelled ? "Cancellation" : "Info",
            BookingId = booking.Id
        });
        return true;
    }

    public async Task<BlockRoomResult> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason, Guid? relatedBookingId = null)
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanBlockRooms(user.Role))
            return Fail("You don't have permission to block this room.");

        var room = await rooms.GetRoomAsync(roomId);
        if (room is null) return Fail("That room could not be found.");
        if (user.Role == UserRole.CentreManager && user.LocationId != room.LocationId)
            return Fail("You can only block rooms at your centre.");
        if (end <= start) return Fail("Block end time must be after start time.");

        var reasonText = string.IsNullOrWhiteSpace(reason) ? "Blocked by Centre Manager" : reason.Trim();
        store.BlockedPeriods.Add(new BlockedPeriod
        {
            RoomId = roomId,
            Start = start,
            End = end,
            Reason = reasonText,
            CreatedBy = user.Name
        });

        var conflictIds = RoomCombinations.ConflictRoomIds(roomId);
        var affected = store.Bookings
            .Where(b =>
                conflictIds.Contains(b.RoomId) &&
                b.Status == BookingStatus.Confirmed &&
                start < b.End && end > b.Start)
            .OrderBy(b => b.Start)
            .ToList();

        if (relatedBookingId is { } relatedId &&
            affected.All(b => b.Id != relatedId) &&
            store.Bookings.FirstOrDefault(b => b.Id == relatedId) is { } related &&
            related.Status == BookingStatus.Confirmed)
        {
            affected.Add(related);
            affected = [.. affected.OrderBy(b => b.Start)];
        }

        var when = $"{start:ddd d MMM}, {start:HH:mm}–{end:HH:mm}";
        var bookerSummary = affected.Count == 0
            ? "There were no bookings in this window."
            : string.Join(
                Environment.NewLine,
                affected.Select(b => $"• {b.BookerName} at {b.Start:HH:mm}–{b.End:HH:mm}"));
        var notifiedNames = affected.Select(b => b.BookerName).Distinct().ToList();
        var notifiedLine = notifiedNames.Count == 0
            ? "No booker was notified."
            : $"A notification was sent to {JoinNames(notifiedNames)}.";

        var confirmation =
            $"{room.Name} has been blocked.{Environment.NewLine}{Environment.NewLine}" +
            $"When: {when}{Environment.NewLine}" +
            $"Reason: {reasonText}{Environment.NewLine}{Environment.NewLine}" +
            (affected.Count == 0
                ? bookerSummary
                : $"Who booked this room:{Environment.NewLine}{bookerSummary}") +
            $"{Environment.NewLine}{Environment.NewLine}{notifiedLine}";

        await notifications.AddAsync(new AppNotification
        {
            Title = $"{room.Name} blocked",
            Message = affected.Count == 0
                ? $"{room.Name} is blocked {when} ({reasonText}). {notifiedLine}"
                : $"{room.Name} is blocked {when} ({reasonText}). " +
                  $"Booked by {string.Join("; ", affected.Select(b => $"{b.BookerName} at {b.Start:HH:mm}–{b.End:HH:mm}"))}. {notifiedLine}",
            Type = "Info",
            BookingId = affected.FirstOrDefault()?.Id
        });

        foreach (var booking in affected)
        {
            await notifications.AddAsync(new AppNotification
            {
                Title = "Your room was blocked",
                Message = $"{room.Name} is blocked {when}. Your booking ({booking.Start:HH:mm}–{booking.End:HH:mm}) was made by {booking.BookerName}.",
                Type = "Cancellation",
                BookingId = booking.Id
            });
        }

        return new BlockRoomResult
        {
            Success = true,
            Title = "Room blocked",
            Message = confirmation
        };

        static BlockRoomResult Fail(string message) => new() { Success = false, Title = "Could not block room", Message = message };

        static string JoinNames(IReadOnlyList<string> names) => names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
        };
    }

    public Task<IReadOnlyList<BlockedPeriod>> GetBlockedPeriodsAsync(string roomId) =>
        Task.FromResult<IReadOnlyList<BlockedPeriod>>(
            store.BlockedPeriods.Where(p => p.RoomId == roomId).OrderBy(p => p.Start).ToList());

    private static bool IsInScope(User user, Booking booking) =>
        user.Role == UserRole.Administrator ||
        (user.Role == UserRole.CentreManager && user.LocationId == booking.LocationId);

    private List<DateTime> SuggestSlots(string roomId, DateTime day, TimeSpan duration)
    {
        var suggestions = new List<DateTime>();
        for (var hour = 8; hour <= 17 && suggestions.Count < 3; hour++)
        {
            var start = day.AddHours(hour);
            var end = start + duration;
            var conflictIds = RoomCombinations.ConflictRoomIds(roomId);
            var busy = store.Bookings.Any(b =>
                conflictIds.Contains(b.RoomId) &&
                b.Status != BookingStatus.Cancelled &&
                start < b.End && end > b.Start);
            var blocked = store.BlockedPeriods.Any(b =>
                conflictIds.Contains(b.RoomId) && start < b.End && end > b.Start);
            if (!busy && !blocked) suggestions.Add(start);
        }
        return suggestions;
    }
}
