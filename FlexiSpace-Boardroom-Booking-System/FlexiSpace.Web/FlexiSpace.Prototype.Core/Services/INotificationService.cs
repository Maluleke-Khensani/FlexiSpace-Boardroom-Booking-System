using Flexispace.Core.Models;

namespace Flexispace.Core.Services;

public interface INotificationService
{
    Task<IReadOnlyList<AppNotification>> GetNotificationsAsync();
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAsUnreadAsync(Guid notificationId);
    Task MarkAllAsReadAsync();
    Task DeleteAsync(Guid notificationId);
    Task AddAsync(AppNotification notification);
}

