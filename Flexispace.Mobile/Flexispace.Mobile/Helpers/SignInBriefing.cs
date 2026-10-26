using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// After a real sign-in, show unread alerts, then a booking reminder.
/// Guest browsing does not get these cards.
/// </summary>
public static class SignInBriefing
{
    public static async Task<bool> ShowAsync(
        INotificationService notifications,
        IBookingService bookings,
        IAuthService auth)
    {
        var user = auth.CurrentUser;
        if (user is null || user.IsGuest)
            return false;

        List<AppNotification> notes;
        try
        {
            notes = (await notifications.GetNotificationsAsync()).ToList();
        }
        catch (Exception)
        {
            notes = [];
        }

        var unread = notes.Where(n => !n.IsRead).ToList();
        var alerts = unread.Where(n => n.Type != "Reminder").ToList();
        var reminderNotes = unread.Where(n => n.Type == "Reminder").ToList();

        var showed = false;
        if (alerts.Count > 0)
        {
            var title = alerts.Count == 1 ? "Unread alert" : "Unread alerts";
            await ActionFeedback.MessageAsync(DescribeNotes(alerts, "alert"), title);
            showed = true;
        }

        if (AppPreferences.BookingReminders)
        {
            var reminder = reminderNotes.Count > 0
                ? DescribeNotes(reminderNotes, "reminder")
                : await DescribeUpcomingAsync(bookings, user);
            if (!string.IsNullOrWhiteSpace(reminder))
            {
                await ActionFeedback.MessageAsync(reminder, "Reminder");
                showed = true;
            }
        }

        return showed;
    }

    private static string DescribeNotes(IReadOnlyList<AppNotification> notes, string noun)
    {
        if (notes.Count == 1)
        {
            var one = notes[0];
            return string.IsNullOrWhiteSpace(one.Message)
                ? one.Title
                : $"{one.Title}\n{one.Message}";
        }

        var lines = notes.Take(3).Select(n => $"• {n.Title}");
        var extra = notes.Count - 3;
        var more = extra > 0
            ? $"\n{extra} more {(extra == 1 ? noun : noun + "s")} in Alerts."
            : "\nOpen Alerts to read them.";
        return string.Join("\n", lines) + more;
    }

    private static async Task<string?> DescribeUpcomingAsync(IBookingService bookings, User user)
    {
        IReadOnlyList<Booking> mine;
        try
        {
            mine = user.Id == Guid.Empty
                ? []
                : await bookings.GetBookingsAsync(userId: user.Id);
        }
        catch (Exception)
        {
            return null;
        }

        var now = DateTime.Now;
        var upcoming = mine
            .Where(b => b.Status != BookingStatus.Cancelled && b.End > now && b.Start <= now.AddHours(24))
            .OrderBy(b => b.Start)
            .ToList();
        if (upcoming.Count == 0)
            return null;

        var next = upcoming[0];
        var where = string.IsNullOrWhiteSpace(next.LocationName)
            ? next.RoomName
            : $"{next.RoomName} at {next.LocationName}";
        var when = $"{next.Start:ddd d MMM HH:mm}–{next.End:HH:mm}";
        var extra = upcoming.Count - 1;
        var more = extra > 0
            ? $"\n{extra} more in the next 24 hours."
            : string.Empty;
        return $"{where}\n{when}{more}";
    }
}
