namespace Flexispace.Mobile.Services.Api;

/// <summary>
/// Stable mobile slug ids (centurion / hou-exec / eag-thi) so RoomCombinations
/// and CM scoping keep working against API integer keys.
/// </summary>
public sealed class CatalogSlugs
{
    private readonly Dictionary<int, string> _locationByApiId = new();
    private readonly Dictionary<string, int> _locationApiBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _roomByApiId = new();
    private readonly Dictionary<string, int> _roomApiBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _roomLocationSlug = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterLocation(int apiId, string name)
    {
        var slug = LocationSlug(name);
        _locationByApiId[apiId] = slug;
        _locationApiBySlug[slug] = apiId;
    }

    public void RegisterRoom(int apiId, string name, int locationApiId)
    {
        var locationSlug = _locationByApiId.TryGetValue(locationApiId, out var ls)
            ? ls
            : locationApiId.ToString();
        var slug = RoomSlug(locationSlug, name);
        _roomByApiId[apiId] = slug;
        _roomApiBySlug[slug] = apiId;
        _roomLocationSlug[slug] = locationSlug;
    }

    public string LocationSlugFromApi(int apiId) =>
        _locationByApiId.TryGetValue(apiId, out var slug) ? slug : apiId.ToString();

    public int? LocationApiId(string? slug) =>
        !string.IsNullOrEmpty(slug) && _locationApiBySlug.TryGetValue(slug, out var id) ? id : null;

    public string RoomSlugFromApi(int apiId) =>
        _roomByApiId.TryGetValue(apiId, out var slug) ? slug : apiId.ToString();

    public int? RoomApiId(string? slug) =>
        !string.IsNullOrEmpty(slug) && _roomApiBySlug.TryGetValue(slug, out var id) ? id : null;

    public string? LocationSlugForRoom(string roomSlug) =>
        _roomLocationSlug.TryGetValue(roomSlug, out var loc) ? loc : null;

    public static string LocationSlug(string name) => name.Trim() switch
    {
        "Centurion" => "centurion",
        "Houghton Estate" => "houghton",
        "Eagle Canyon" => "eagle",
        _ => Slugify(name)
    };

    public static string RoomSlug(string locationSlug, string roomName) => (locationSlug, roomName.Trim()) switch
    {
        ("centurion", "Boardroom A") => "cen-a",
        ("centurion", "Boardroom B") => "cen-b",
        ("centurion", "Training Room") => "cen-t",
        ("houghton", "Boardroom 1") => "hou-1",
        ("houghton", "Executive Boardroom") => "hou-exec",
        ("eagle", "Confuzzled") => "eag-con",
        ("eagle", "Thingamajik") => "eag-thi",
        ("eagle", "Whachamacallit") => "eag-wha",
        ("eagle", "Fiddlestix") => "eag-fid",
        ("eagle", "Meeting Room") => "eag-meet",
        ("eagle", "Training Room") => "eag-train",
        _ => $"{locationSlug}-{Slugify(roomName)}"
    };

    private static string Slugify(string value) =>
        string.Concat(value.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' '))
            .Trim()
            .Replace(' ', '-');
}
