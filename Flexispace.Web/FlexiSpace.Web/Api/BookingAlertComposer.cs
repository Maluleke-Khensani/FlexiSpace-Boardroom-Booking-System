using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class BookingAlertComposer(IAuthService auth, IBookingService bookings)
{
    public async Task<IReadOnlyList<AppNotification>> ComposeAsync()
    {
        var user = auth.CurrentUser;
        if (user is null)
            return [];

        var now = DateTime.Now;
        var alerts = new List<AppNotification>();
        IReadOnlyList<Booking> scope;
        try
        {
            scope = await bookings.GetBookingsForCurrentUserScopeAsync();
        }
        catch
        {
            return [];
        }

        var manager = RolePermissions.CanManageLocationBookings(user.Role);

        foreach (var booking in scope)
        {
            var own = booking.BookerId == user.Id;
            if (!own && !manager)
                continue;

            if (booking.Status == BookingStatus.Cancelled)
            {
                alerts.Add(Alert(
                    booking, 2, "Cancellation",
                    "Booking cancelled",
                    own
                        ? $"Your booking for {booking.RoomName} on {booking.Start:g} has been cancelled."
                        : $"{booking.BookerName}'s booking for {booking.RoomName} on {booking.Start:g} has been cancelled.",
                    booking.ModifiedAt ?? booking.CreatedAt.IfDefault(booking.Start)));
                continue;
            }

            if (own || manager)
            {
                var created = booking.CreatedAt == default ? booking.Start : booking.CreatedAt;
                if (created > now.AddDays(-14) && created <= now.AddDays(1))
                {
                    alerts.Add(Alert(
                        booking, 1, "Confirmation",
                        own ? "Booking confirmed" : $"New booking at {booking.RoomName}",
                        own
                            ? $"Your booking for {booking.RoomName} on {booking.Start:g} is confirmed."
                            : $"{booking.BookerName} booked {booking.RoomName} on {booking.Start:g}.",
                        created));
                }
            }

            if (booking.ModifiedAt is { } modified &&
                modified > now.AddDays(-14) &&
                (booking.CreatedAt == default || modified > booking.CreatedAt.AddMinutes(1)))
            {
                alerts.Add(Alert(
                    booking, 3, "Info",
                    "Booking updated",
                    own
                        ? $"Your booking for {booking.RoomName} was updated to {booking.Start:g}–{booking.End:t}."
                        : $"{booking.BookerName}'s booking for {booking.RoomName} was updated to {booking.Start:g}–{booking.End:t}.",
                    modified));
            }

            if (own && booking.Start > now && booking.Start <= now.AddHours(24))
            {
                var oneHour = booking.Start <= now.AddHours(1);
                alerts.Add(Alert(
                    booking, 4, "Reminder",
                    oneHour ? "Reminder · 1 hour" : "Reminder · 24 hours",
                    $"{booking.RoomName} · {booking.LocationName} · {booking.Start:g}",
                    now));
            }
        }

        if (RolePermissions.CanBlockRooms(user.Role))
        {
            try
            {
                foreach (var block in await bookings.GetBlockedPeriodsAsync())
                {
                    alerts.Add(new AppNotification
                    {
                        Id = SynthId(block.Id, 5),
                        Title = "Room blocked",
                        Message = $"{block.RoomName} is unavailable {block.Start:g}–{block.End:t}. {block.Reason}",
                        Type = "Blocked",
                        CreatedAt = block.Start < now ? now : block.Start,
                        IsRead = false
                    });
                }
            }
            catch
            {
                // Blocking data is optional for the inbox.
            }
        }

        return alerts;
    }

    private static AppNotification Alert(
        Booking booking,
        byte kind,
        string type,
        string title,
        string message,
        DateTime createdAt) => new()
    {
        Id = SynthId(booking.Id, kind),
        Title = title,
        Message = message,
        Type = type,
        BookingId = booking.Id,
        CreatedAt = createdAt == default ? DateTime.Now : createdAt,
        IsRead = false
    };

    private static Guid SynthId(Guid source, byte kind)
    {
        var bytes = new byte[16];
        var src = source.ToByteArray();
        Buffer.BlockCopy(src, 0, bytes, 4, 4);
        bytes[8] = 0xA1;
        bytes[9] = kind;
        return new Guid(bytes);
    }
}

file static class DateTimeFallback
{
    public static DateTime IfDefault(this DateTime value, DateTime fallback) =>
        value == default ? fallback : value;
}
