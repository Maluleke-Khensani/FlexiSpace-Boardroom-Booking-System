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

    public Task<IReadOnlyList<OccupiedSlot>> GetOccupiedSlotsAsync(string roomId, DateTime day)
    {
        var linked = RoomCombinations.GetConflictRoomIds(roomId);
        var slots = store.Bookings
            .Where(b =>
                linked.Contains(b.RoomId) &&
                b.Status != BookingStatus.Cancelled &&
                b.Start.Date == day.Date)
            .Select(b => new OccupiedSlot { Start = b.Start.TimeOfDay, End = b.End.TimeOfDay })
            .ToList();

        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        foreach (var block in store.BlockedPeriods.Where(b => linked.Contains(b.RoomId)))
        {
            if (block.Start >= dayEnd || block.End <= dayStart)
                continue;
            var occStart = block.Start <= dayStart ? TimeSpan.Zero : block.Start.TimeOfDay;
            var occEnd = block.End >= dayEnd ? TimeSpan.FromHours(24) : block.End.TimeOfDay;
            if (occEnd > occStart)
                slots.Add(new OccupiedSlot { Start = occStart, End = occEnd });
        }

        return Task.FromResult<IReadOnlyList<OccupiedSlot>>(slots);
    }

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

        if (request.Attendees > room.Capacity)
        {
            return new BookingResult
            {
                Success = false,
                Message = $"{room.Name} seats {room.Capacity}. Reduce the attendee count or choose a larger room."
            };
        }

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

        if (Overlaps(request.RoomId, request.Start, request.End))
        {
            return new BookingResult { Success = false, Message = "This room is blocked for that period." };
        }

        var location = await rooms.GetLocationAsync(room.LocationId);
        if (HasBookingConflict(request.RoomId, request.Start, request.End))
        {
            var suggestions = SuggestSlots(request.RoomId, request.Start.Date, request.End - request.Start);
            return new BookingResult
            {
                Success = false,
                Message = room.IsCombined
                    ? "Both rooms must be free to conjoin them. Try another time, or book one room on its own."
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
            Status = BookingStatus.Confirmed
        };

        store.Bookings.Add(booking);

        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking confirmed",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}–{booking.End:HH:mm}.",
            Type = "Confirmation",
            CreatedAt = DateTime.Now,
            IsRead = false,
            BookingId = booking.Id
        });

        return new BookingResult
        {
            Success = true,
            Message = "Your boardroom is booked.",
            Booking = booking
        };
    }

    public async Task<BookingResult> CancelBookingAsync(Guid bookingId)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null)
            return new BookingResult { Success = false, Message = "Booking not found." };

        var isOwner = booking.BookerId == user.Id;
        var canCancel =
            (isOwner && RolePermissions.CanCancelOwnBookings(user.Role)) ||
            (RolePermissions.CanCancelAnyBooking(user.Role) && IsInScope(user, booking));

        if (!canCancel)
            return new BookingResult { Success = false, Message = "You don't have permission to cancel this booking." };

        booking.Status = BookingStatus.Cancelled;
        await notifications.AddAsync(new AppNotification
        {
            Title = "Booking cancelled",
            Message = $"{booking.RoomName} · {booking.LocationName} · {booking.Start:ddd d MMM HH:mm}",
            Type = "Cancellation",
            BookingId = booking.Id
        });
        return new BookingResult { Success = true, Message = "Booking cancelled.", Booking = booking };
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

    public Task<BookingResult> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes)
    {
        var user = auth.CurrentUser;
        var booking = store.Bookings.FirstOrDefault(b => b.Id == bookingId);
        if (user is null || booking is null)
            return Task.FromResult(new BookingResult { Success = false, Message = "Booking not found." });

        var isOwner = booking.BookerId == user.Id;
        if (!isOwner && (!RolePermissions.CanEditBookings(user.Role) || !IsInScope(user, booking)))
            return Task.FromResult(new BookingResult { Success = false, Message = "You don't have permission to edit this booking." });
        if (end <= start)
            return Task.FromResult(new BookingResult { Success = false, Message = "End time must be after start time." });

        var conflict = HasBookingConflict(booking.RoomId, start, end, bookingId);
        if (conflict)
            return Task.FromResult(new BookingResult { Success = false, Message = "That time overlaps another booking." });

        booking.Start = start;
        booking.End = end;
        booking.Attendees = Math.Max(1, attendees);
        booking.Notes = notes;
        return Task.FromResult(new BookingResult { Success = true, Message = "Booking updated.", Booking = booking });
    }

    public async Task<BookingResult> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason)
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanBlockRooms(user.Role))
            return new BookingResult { Success = false, Message = "You don't have permission to block rooms." };

        var room = await rooms.GetRoomAsync(roomId);
        if (room is null)
            return new BookingResult { Success = false, Message = "Room not found." };
        if (user.Role == UserRole.CentreManager && user.LocationId != room.LocationId)
            return new BookingResult { Success = false, Message = "You can only block rooms at your centre." };
        if (end <= start)
            return new BookingResult { Success = false, Message = "The block must end after it starts." };

        store.BlockedPeriods.Add(new BlockedPeriod
        {
            RoomId = roomId,
            RoomName = room.Name,
            LocationId = room.LocationId,
            Start = start,
            End = end,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Blocked by Centre Manager" : reason,
            CreatedBy = user.Name
        });

        var affected = store.Bookings
            .Where(b =>
                b.RoomId == roomId &&
                b.Status != BookingStatus.Cancelled &&
                start < b.End &&
                end > b.Start)
            .Select(b => b.BookerId)
            .Distinct()
            .ToList();

        if (affected.Contains(user.Id) || RolePermissions.CanBlockRooms(user.Role))
        {
            await notifications.AddAsync(new AppNotification
            {
                Title = "Room blocked",
                Message = $"{room.Name} is unavailable {start:g}–{end:t}. {reason}",
                Type = "Blocked"
            });
        }

        return new BookingResult { Success = true, Message = "The room is now blocked for that period." };
    }

    public Task<IReadOnlyList<BlockedPeriod>> GetBlockedPeriodsAsync(string? roomId = null)
    {
        var user = auth.CurrentUser;
        IEnumerable<BlockedPeriod> query = store.BlockedPeriods;
        if (!string.IsNullOrEmpty(roomId))
            query = query.Where(b => b.RoomId == roomId);
        if (user?.Role == UserRole.CentreManager && !string.IsNullOrEmpty(user.LocationId))
            query = query.Where(b => b.LocationId == user.LocationId);

        return Task.FromResult<IReadOnlyList<BlockedPeriod>>(
            query.OrderByDescending(b => b.Start).ToList());
    }

    public Task<bool> RemoveBlockedPeriodAsync(Guid blockId)
    {
        var user = auth.CurrentUser;
        if (user is null || !RolePermissions.CanBlockRooms(user.Role))
            return Task.FromResult(false);

        var block = store.BlockedPeriods.FirstOrDefault(b => b.Id == blockId);
        if (block is null)
            return Task.FromResult(false);
        if (user.Role == UserRole.CentreManager && user.LocationId != block.LocationId)
            return Task.FromResult(false);

        store.BlockedPeriods.Remove(block);
        return Task.FromResult(true);
    }

    private static bool IsInScope(User user, Booking booking) =>
        user.Role == UserRole.Administrator ||
        (user.Role == UserRole.CentreManager && user.LocationId == booking.LocationId);

    private bool HasBookingConflict(string roomId, DateTime start, DateTime end, Guid? excludeId = null)
    {
        var linked = RoomCombinations.GetConflictRoomIds(roomId);
        return store.Bookings.Any(b =>
            (excludeId is null || b.Id != excludeId) &&
            linked.Contains(b.RoomId) &&
            b.Status != BookingStatus.Cancelled &&
            start < b.End &&
            end > b.Start);
    }

    private bool Overlaps(string roomId, DateTime start, DateTime end)
    {
        var linked = RoomCombinations.GetConflictRoomIds(roomId);
        return store.BlockedPeriods.Any(b =>
            linked.Contains(b.RoomId) &&
            start < b.End &&
            end > b.Start);
    }

    private List<DateTime> SuggestSlots(string roomId, DateTime day, TimeSpan duration)
    {
        var suggestions = new List<DateTime>();
        for (var hour = 8; hour <= 17 && suggestions.Count < 3; hour++)
        {
            var start = day.AddHours(hour);
            var end = start + duration;
            var busy = HasBookingConflict(roomId, start, end);
            var blocked = Overlaps(roomId, start, end);
            if (!busy && !blocked) suggestions.Add(start);
        }
        return suggestions;
    }
}

