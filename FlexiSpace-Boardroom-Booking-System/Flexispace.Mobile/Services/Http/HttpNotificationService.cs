using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public sealed class HttpNotificationService(ApiClient api) : INotificationService
{
    public event Action<int>? UnreadCountChanged;

    public async Task<IReadOnlyList<AppNotification>> GetNotificationsAsync()
    {
        var items = await api.GetAsync<List<ApiNotificationDto>>("api/Notification") ?? [];
        return items
            .OrderByDescending(n => n.CreatedAt)
            .Select(Map)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var dto = await api.GetAsync<ApiUnreadCountDto>("api/Notification/unread-count");
        var count = dto?.UnreadCount ?? 0;
        UnreadCountChanged?.Invoke(count);
        return count;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        using var response = await api.PatchAsync($"api/Notification/{IdAdapter.ToInt(notificationId)}/read");
        if (response.IsSuccessStatusCode)
            await GetUnreadCountAsync();
    }

    public Task MarkAsUnreadAsync(Guid notificationId) =>
        Task.CompletedTask; // API has no unread endpoint

    public async Task MarkAllAsReadAsync()
    {
        using var response = await api.PatchAsync("api/Notification/read-all");
        if (response.IsSuccessStatusCode)
            UnreadCountChanged?.Invoke(0);
    }

    public Task DeleteAsync(Guid notificationId) =>
        Task.CompletedTask; // API has no delete endpoint

    public Task AddAsync(AppNotification notification) =>
        Task.CompletedTask; // Notifications are created server-side

    private static AppNotification Map(ApiNotificationDto dto) => new()
    {
        Id = IdAdapter.ToGuid(dto.Id),
        Title = dto.Title,
        Message = dto.Message,
        CreatedAt = dto.CreatedAt.ToLocalTime(),
        Type = MapType(dto.Type),
        IsRead = dto.IsRead
    };

    private static string MapType(string type) => type switch
    {
        "BookingCreated" => "Confirmation",
        "BookingCancelled" => "Cancellation",
        "BookingReminder" => "Reminder",
        "BookingModified" => "Info",
        _ => string.IsNullOrWhiteSpace(type) ? "Info" : type
    };
}
