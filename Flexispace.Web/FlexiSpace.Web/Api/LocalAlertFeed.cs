using System.Text.Json;
using Flexispace.Core.Models;
using Microsoft.JSInterop;

namespace Flexispace.Web.Api;

public sealed class LocalAlertFeed(IJSRuntime js)
{
    private const string StorageKey = "flexispace.localAlerts.v1";
    private readonly List<AppNotification> _items = [];
    private bool _loaded;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            var raw = await js.InvokeAsync<string>("flexispaceAlerts.getFeed", StorageKey);
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var stored = JsonSerializer.Deserialize<List<AppNotification>>(raw, FlexiSpaceApiClient.Json) ?? [];
            _items.Clear();
            _items.AddRange(stored);
        }
        catch
        {
            // Keep an empty in-memory feed if localStorage is unavailable.
        }
    }

    public async Task AddAsync(AppNotification notification)
    {
        await EnsureLoadedAsync();
        _items.RemoveAll(n => n.Id == notification.Id);
        _items.Insert(0, notification);
        await PersistAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await EnsureLoadedAsync();
        _items.RemoveAll(n => n.Id == id);
        await PersistAsync();
    }

    public IReadOnlyList<AppNotification> Items => _items;

    private async Task PersistAsync()
    {
        try
        {
            await js.InvokeVoidAsync(
                "flexispaceAlerts.setFeed",
                StorageKey,
                JsonSerializer.Serialize(_items, FlexiSpaceApiClient.Json));
        }
        catch
        {
        }
    }
}
