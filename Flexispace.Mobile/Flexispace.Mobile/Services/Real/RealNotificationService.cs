using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Bridges INotificationService onto /api/notification. The real API only
// supports: list mine, unread count, mark one read, mark all read -
// notifications are created server-side (NotificationService, fired from
// booking events - see FlexiSpace.Infrastructure.Services.BookingService/
// BoardroomService), not created from the client. MarkAsUnreadAsync/
// DeleteAsync/AddAsync are safe no-ops here rather than silently-wrong
// local mutations that wouldn't persist past a refresh - same reasoning
// as Flexispace.Web's identical service.
//
// UnreadCountChanged only fires from RefreshUnreadCountAsync (called by
// AppShell/pages after a relevant action) - there's no server push here,
// unlike a live SignalR/websocket feed, so the badge updates on the next
// explicit check rather than the instant a new notification arrives.
public class RealNotificationService : INotificationService
{
    private readonly FlexiSpaceApiClient _api;

    public event Action<int>? UnreadCountChanged;

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
        var count = result?.UnreadCount ?? 0;
        UnreadCountChanged?.Invoke(count);
        return count;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var id = ApiId.Decode(notificationId);
        if (id is null) return;
        await _api.PatchAsync($"api/notification/{id}/read");
        await GetUnreadCountAsync();
    }

    public Task MarkAsUnreadAsync(Guid notificationId) => Task.CompletedTask; // not supported by the real API

    public async Task MarkAllAsReadAsync()
    {
        await _api.PatchAsync("api/notification/read-all");
        UnreadCountChanged?.Invoke(0);
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
        // its booking isn't possible from the API response alone yet.
        BookingId = null
    };

    private class UnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}
