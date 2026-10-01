using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPushNotificationSender _pushNotificationSender;

        public NotificationService(
            ApplicationDbContext context,
            IPushNotificationSender pushNotificationSender)
        {
            _context = context;
            _pushNotificationSender = pushNotificationSender;
        }

        // Kept for the tests (and any other call site) written before push
        // notifications existed, so they don't all need a mock
        // IPushNotificationSender just to construct this class. Falls back
        // to a no-op sender - CreateNotificationAsync always "sends" a
        // push, so this constructor must never leave that field null.
        public NotificationService(ApplicationDbContext context)
            : this(context, NoOpPushNotificationSender.Instance)
        {
        }

        private sealed class NoOpPushNotificationSender : IPushNotificationSender
        {
            public static readonly NoOpPushNotificationSender Instance = new();

            public Task SendAsync(int userId, string title, string body) => Task.CompletedTask;
        }

        public async Task<Notification> CreateNotificationAsync(
            int userId,
            string title,
            string message,
            NotificationType type)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                throw new NotFoundException($"User {userId} was not found.");
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type
                // IsRead defaults to false, CreatedAt/SentAt default to
                // UtcNow on the entity.
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            // Push is mobile-only by construction: a user only has device tokens
            // if the mobile app registered one, so a web-only user never receives
            // a push here - no platform check needed. AzureNotificationHubPushSender
            // never throws, so a push failure can't fail booking creation/etc.
            await _pushNotificationSender.SendAsync(userId, title, message);
            return notification;
        }

        public async Task<IReadOnlyList<Notification>> GetNotificationsForUserAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return false;
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<int> MarkAllAsReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unread)
            {
                notification.IsRead = true;
            }

            if (unread.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return unread.Count;
        }
    }
}
