using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiNotificationService(FlexiSpaceApiClient api) : INotificationService
{
    public async Task<IReadOnlyList<AppNotification>> GetNotificationsAsync()
    {
        var dtos = await api.GetAsync<List<NotificationDto>>("api/notification") ?? [];
        return dtos
            .Select(CatalogMapper.ToNotification)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var dto = await api.GetAsync<UnreadCountDto>("api/notification/unread-count");
        return dto?.UnreadCount ?? 0;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        await api.PatchAsync($"api/notification/{IdMap.ToInt(notificationId)}/read");
    }

    public Task MarkAsUnreadAsync(Guid notificationId)
    {
        _ = notificationId;
        return Task.CompletedTask;
    }

    public async Task MarkAllAsReadAsync()
    {
        await api.PatchAsync("api/notification/read-all");
    }

    public Task DeleteAsync(Guid notificationId)
    {
        _ = notificationId;
        return Task.CompletedTask;
    }

    public Task AddAsync(AppNotification notification)
    {
        _ = notification;
        return Task.CompletedTask;
    }
}
