using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

public class MockNotificationService : INotificationService
{
    private readonly List<AppNotification> _notifications;

    public MockNotificationService(MockDataStore store)
    {
        // Link demo notifications to real seeded bookings so tapping one actually
        // opens something, instead of just marking it read with nowhere to go.
        var exec = store.Bookings.FirstOrDefault(b => b.RoomId == "hou-exec");
        var meetingRoom = store.Bookings.FirstOrDefault(b => b.RoomId == "eag-meet");
        var boardroomA = store.Bookings.FirstOrDefault(b => b.RoomId == "cen-a");

        _notifications =
        [
            new()
            {
                Title = "Booking confirmed",
                Message = "Executive Boardroom · Houghton · Today 10:00–11:00",
                Type = "Confirmation",
                CreatedAt = DateTime.Now.AddHours(-2),
                BookingId = exec?.Id
            },
            new()
            {
                Title = "Reminder · 24 hours",
                Message = "Meeting Room · Eagle Canyon tomorrow at 14:00",
                Type = "Reminder",
                CreatedAt = DateTime.Now.AddHours(-5),
                BookingId = meetingRoom?.Id
            },
            new()
            {
                Title = "Reminder · 1 hour",
                Message = "Boardroom A · Centurion starts in 1 hour",
                Type = "Reminder",
                CreatedAt = DateTime.Now.AddMinutes(-40),
                IsRead = true,
                BookingId = boardroomA?.Id
            },
            new()
            {
                Title = "Booking cancelled",
                Message = "Confuzzled · Eagle Canyon was cancelled by the organiser",
                Type = "Cancellation",
                CreatedAt = DateTime.Now.AddDays(-1),
                IsRead = true
            }
        ];
    }

    public Task<IReadOnlyList<AppNotification>> GetNotificationsAsync() =>
        Task.FromResult<IReadOnlyList<AppNotification>>(_notifications.OrderByDescending(n => n.CreatedAt).ToList());

    public Task<int> GetUnreadCountAsync() =>
        Task.FromResult(_notifications.Count(n => !n.IsRead));

    public Task MarkAsReadAsync(Guid notificationId)
    {
        var n = _notifications.FirstOrDefault(x => x.Id == notificationId);
        if (n is not null) n.IsRead = true;
        return Task.CompletedTask;
    }

    public Task MarkAsUnreadAsync(Guid notificationId)
    {
        var n = _notifications.FirstOrDefault(x => x.Id == notificationId);
        if (n is not null) n.IsRead = false;
        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync()
    {
        foreach (var n in _notifications) n.IsRead = true;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid notificationId)
    {
        _notifications.RemoveAll(n => n.Id == notificationId);
        return Task.CompletedTask;
    }

    public Task AddAsync(AppNotification notification)
    {
        _notifications.Insert(0, notification);
        return Task.CompletedTask;
    }
}

