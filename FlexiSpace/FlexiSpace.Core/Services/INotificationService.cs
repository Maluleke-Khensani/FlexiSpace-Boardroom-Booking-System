using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Services
{
    // Notification is system-managed (see data model handover) - there is
    // deliberately no CRUD controller for it. CreateNotificationAsync is
    // meant to be called by other services as a side effect of a real
    // event (booking approved/rejected/cancelled, a reminder firing,
    // etc.), not by users directly.
    public interface INotificationService
    {
        Task<Notification> CreateNotificationAsync(
            int userId,
            string title,
            string message,
            NotificationType type);

        Task<IReadOnlyList<Notification>> GetNotificationsForUserAsync(int userId);

        Task<int> GetUnreadCountAsync(int userId);

        // Returns false if the notification doesn't exist or doesn't
        // belong to userId - callers must not be able to mark someone
        // else's notification as read just by guessing an id.
        Task<bool> MarkAsReadAsync(int notificationId, int userId);

        Task<int> MarkAllAsReadAsync(int userId);
    }
}
