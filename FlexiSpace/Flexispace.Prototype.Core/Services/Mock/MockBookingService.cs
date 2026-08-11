using Flexispace.Core.Helpers;
using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

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
                b.RoomId == request.RoomId &&
                request.Start < b.End &&
                request.End > b.Start))
        {
            return new BookingResult { Success = false, Message = "This room is blocked for that period." };
        }

        var location = await rooms.GetLocationAsync(room.LocationId);
        var conflict = store.Bookings.Any(b =>
            b.RoomId == request.RoomId &&
            b.Status != BookingStatus.Cancelled &&
            request.Start < b.End &&
            request.End > b.Start);

        if (conflict)
        {
            var suggestions = SuggestSlots(request.RoomId, request.Start.Date, request.End - request.Start);
            return new BookingResult
            {
                Success = false,
                Message = "This room is already booked for that time. Try one of the suggested slots.",
                SuggestedSlots = suggestions
            };
        }

        // Client / staff bookings can sit as Pending when optional approval is on for CM demo
        var needsApproval = auth.CurrentUser.Role is UserRole.Client or UserRole.Staff;

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
            Status = needsApproval ? BookingStatus.Pending : BookingStatus.Confirmed,
            OutlookEventId = $"mock-{Guid.NewGuid():N}"[..20]
        };

        store.Bookings.Add(booking);

        // Always drop an unread Alerts item the booker can tap open (links to this booking).
        await notifications.AddAsync(new AppNotification
        {
            Title = needsApproval ? "Booking request sent" : "Booking confirmed",
            Message = needsApproval
                ? $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}–{booking.End:HH:mm} · awaiting Centre Manager approval. Tap to open."
                : $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}–{booking.End:HH:mm}. Tap to open.",
            Type = needsApproval ? "Reminder" : "Confirmation",
            CreatedAt = DateTime.Now,
            IsRead = false,
            BookingId = booking.Id
        });

        return new BookingResult
        {
            Success = true,
            Message = needsApproval
                ? "Booking submitted for Centre Manager approval. Check Alerts for your request."
                : "Your boardroom is booked. Check Alerts for the confirmation.",
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

    public async Task<bool> ApproveBookingAsync(Guid bookingId)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null) return false;
        if (!RolePermissions.CanApproveBookings(user.Role) || !IsInScope(user, booking)) return false;
        if (booking.Status != BookingStatus.Pending) return false;

        booking.Status = BookingStatus.Confirmed;
        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking approved",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}",
            Type = "Confirmation",
            BookingId = booking.Id
        });
        return true;
    }

    public async Task<bool> DeclineBookingAsync(Guid bookingId)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null) return false;
        if (!RolePermissions.CanApproveBookings(user.Role) || !IsInScope(user, booking)) return false;
        if (booking.Status != BookingStatus.Pending) return false;

        booking.Status = BookingStatus.Cancelled;
        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking declined",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm} was declined by {user.Name}.",
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

        var conflict = store.Bookings.Any(b =>
            b.Id != bookingId &&
            b.RoomId == booking.RoomId &&
            b.Status != BookingStatus.Cancelled &&
            start < b.End && end > b.Start);
        if (conflict) return Task.FromResult(false);

        booking.Start = start;
        booking.End = end;
        booking.Attendees = Math.Max(1, attendees);
        booking.Notes = notes;
        return Task.FromResult(true);
    }

    public async Task<bool> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason)
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanBlockRooms(user.Role)) return false;

        var room = await rooms.GetRoomAsync(roomId);
        if (room is null) return false;
        if (user.Role == UserRole.CentreManager && user.LocationId != room.LocationId) return false;
        if (end <= start) return false;

        store.BlockedPeriods.Add(new BlockedPeriod
        {
            RoomId = roomId,
            Start = start,
            End = end,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Blocked by Centre Manager" : reason,
            CreatedBy = user.Name
        });

        await notifications.AddAsync(new AppNotification
        {
            Title = "Room blocked",
            Message = $"{room.Name} · {start:g}–{end:t} · {reason}",
            Type = "Info"
        });
        return true;
    }

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
            var busy = store.Bookings.Any(b =>
                b.RoomId == roomId &&
                b.Status != BookingStatus.Cancelled &&
                start < b.End && end > b.Start);
            var blocked = store.BlockedPeriods.Any(b =>
                b.RoomId == roomId && start < b.End && end > b.Start);
            if (!busy && !blocked) suggestions.Add(start);
        }
        return suggestions;
    }
}

