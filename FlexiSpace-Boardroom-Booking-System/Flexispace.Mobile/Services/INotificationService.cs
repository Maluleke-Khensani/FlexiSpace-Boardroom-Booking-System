using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface INotificationService
{
    /// <summary>Raised with the current unread count whenever alerts change.</summary>
    event Action<int>? UnreadCountChanged;

    Task<IReadOnlyList<AppNotification>> GetNotificationsAsync();
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAsUnreadAsync(Guid notificationId);
    Task MarkAllAsReadAsync();
    Task DeleteAsync(Guid notificationId);
    Task AddAsync(AppNotification notification);
}
