using System.Collections.Concurrent;
using System.Security.Claims;

namespace Flexispace.Web.Api;

public sealed class AccessTokenCache
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string userKey, string token)
    {
        if (!string.IsNullOrWhiteSpace(userKey) && !string.IsNullOrWhiteSpace(token))
            _tokens[userKey] = token;
    }

    public string? Get(string? userKey) =>
        string.IsNullOrWhiteSpace(userKey) ? null :
        _tokens.TryGetValue(userKey, out var token) ? token : null;

    public static string? UserKey(ClaimsPrincipal? user) =>
        user?.FindFirst("oid")?.Value
        ?? user?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

