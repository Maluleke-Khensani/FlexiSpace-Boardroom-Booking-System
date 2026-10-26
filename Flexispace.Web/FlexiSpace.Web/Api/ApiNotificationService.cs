using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiNotificationService(
    FlexiSpaceApiClient api,
    NotificationReadOverride unreadOverride,
    LocalAlertFeed localFeed,
    BookingAlertComposer composer) : INotificationService
{
    public async Task<IReadOnlyList<AppNotification>> GetNotificationsAsync()
    {
        await unreadOverride.EnsureLoadedAsync();
        await localFeed.EnsureLoadedAsync();

        var merged = new List<AppNotification>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dtos = await api.GetAsync<List<NotificationDto>>("api/notification") ?? [];
            foreach (var item in dtos.Select(CatalogMapper.ToNotification))
                TryAdd(merged, seen, item);
        }
        catch
        {
            // Fall through to local and derived alerts.
        }

        foreach (var item in localFeed.Items)
            TryAdd(merged, seen, item);

        try
        {
            foreach (var item in await composer.ComposeAsync())
                TryAdd(merged, seen, item);
        }
        catch
        {
            // Derived alerts are extra context, not a hard requirement.
        }

        unreadOverride.Apply(merged);
        return merged
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var items = await GetNotificationsAsync();
        return items.Count(n => !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        await unreadOverride.ClearAsync(notificationId);
        var id = IdMap.ToInt(notificationId);
        if (id > 0)
            await api.PatchAsync($"api/notification/{id}/read");
    }

    public async Task MarkAsUnreadAsync(Guid notificationId)
    {
        await unreadOverride.ForceUnreadAsync(notificationId);
        try
        {
            var id = IdMap.ToInt(notificationId);
            if (id > 0)
            {
                using var response = await api.PatchAsync($"api/notification/{id}/unread");
                _ = response.IsSuccessStatusCode;
            }
        }
        catch
        {
            // Hosted API may not have unread yet; the local override still stands.
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        await unreadOverride.ClearAllAsync();
        await api.PatchAsync("api/notification/read-all");
    }

    public async Task DeleteAsync(Guid notificationId)
    {
        await unreadOverride.ClearAsync(notificationId);
        await localFeed.DeleteAsync(notificationId);
        var id = IdMap.ToInt(notificationId);
        if (id > 0)
            await api.DeleteAsync($"api/notification/{id}");
    }

    public async Task AddAsync(AppNotification notification)
    {
        if (notification.Id == Guid.Empty)
            notification.Id = Guid.NewGuid();
        if (notification.CreatedAt == default)
            notification.CreatedAt = DateTime.Now;
        await localFeed.AddAsync(notification);
    }

    private static void TryAdd(List<AppNotification> merged, HashSet<string> seen, AppNotification item)
    {
        var key = $"{item.Title}|{item.Message}";
        if (!seen.Add(key) || !seen.Add(item.Id.ToString()))
            return;
        merged.Add(item);
    }
}
