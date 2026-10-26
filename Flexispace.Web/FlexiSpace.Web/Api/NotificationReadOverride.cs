using Flexispace.Core.Models;
using Microsoft.JSInterop;

namespace Flexispace.Web.Api;

/// <summary>
/// Hosted FlexiSpace currently has mark-read but not mark-unread.
/// Keep unread choices in this browser so the Alerts button still works.
/// </summary>
public sealed class NotificationReadOverride(IJSRuntime js)
{
    private const string StorageKey = "flexispace.unreadOverrides.v1";
    private readonly HashSet<Guid> _unread = [];
    private bool _loaded;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            var ids = await js.InvokeAsync<string[]>("flexispaceAlerts.getUnread", StorageKey);
            foreach (var id in ids ?? [])
            {
                if (Guid.TryParse(id, out var guid))
                    _unread.Add(guid);
            }
        }
        catch
        {
            // First render / missing JS — in-memory overrides still work for this session.
        }
    }

    public async Task ForceUnreadAsync(Guid id)
    {
        _unread.Add(id);
        await PersistAsync();
    }

    public async Task ClearAsync(Guid id)
    {
        _unread.Remove(id);
        await PersistAsync();
    }

    public async Task ClearAllAsync()
    {
        _unread.Clear();
        await PersistAsync();
    }

    public int ExtraUnreadCount => _unread.Count;

    public void Apply(IEnumerable<AppNotification> items)
    {
        foreach (var item in items)
        {
            if (_unread.Contains(item.Id))
                item.IsRead = false;
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            await js.InvokeVoidAsync(
                "flexispaceAlerts.setUnread",
                StorageKey,
                _unread.Select(id => id.ToString()).ToArray());
        }
        catch
        {
            // Keep the in-memory set even if localStorage is blocked.
        }
    }
}
