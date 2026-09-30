using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Services.Real;

// Bridges INotificationService onto /api/notification. The real API only
// supports: list mine, unread count, mark one read, mark all read. It has
// no mark-unread, delete, or create-from-client endpoints (notifications
// are created server-side, by NotificationService, when a booking event
// happens - see the push-notifications handover) - those three methods
// are safe no-ops here rather than silently-wrong local mutations that
// wouldn't persist past a page refresh.
public class RealNotificationService : INotificationService
{
    private readonly FlexiSpaceApiClient _api;

    public RealNotificationService(FlexiSpaceApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<AppNotification>> GetNotificationsAsync()
    {
        var notifications = await _api.GetAsync<List<ApiNotification>>("api/notification") ?? new();
        return notifications.Select(Map).ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await _api.GetAsync<UnreadCountResponse>("api/notification/unread-count");
        return result?.UnreadCount ?? 0;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var id = ApiId.Decode(notificationId);
        if (id is null) return;
        await _api.PatchAsync($"api/notification/{id}/read");
    }

    public Task MarkAsUnreadAsync(Guid notificationId) => Task.CompletedTask; // not supported by the real API

    public async Task MarkAllAsReadAsync()
    {
        await _api.PatchAsync("api/notification/read-all");
    }

    public Task DeleteAsync(Guid notificationId) => Task.CompletedTask; // not supported by the real API

    public Task AddAsync(AppNotification notification) => Task.CompletedTask; // notifications are server-created only

    private static AppNotification Map(ApiNotification n) => new()
    {
        Id = ApiId.Encode(n.Id),
        Title = n.Title,
        Message = n.Message,
        CreatedAt = n.CreatedAt,
        IsRead = n.IsRead,
        Type = n.Type,
        // The real Notification entity doesn't carry a BookingId back-link
        // (see NotificationResponseDto) - deep-linking a notification to
        // its booking isn't possible yet from the API response alone.
        BookingId = null
    };

    private class UnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}
