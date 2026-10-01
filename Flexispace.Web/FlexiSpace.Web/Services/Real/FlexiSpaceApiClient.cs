using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flexispace.Web.Services.Real;

// Thin wrapper around the named "FlexiSpaceApi" HttpClient (registered in
// Program.cs with the API's base address). Centralises JSON options,
// attaching the signed-in user's bearer token, and the "treat 401/403/404
// as a clean failure, not an exception" behaviour every Real*Service
// needs - none of the prototype interfaces have a concept of "the API
// said no", they just return false/null/empty, so this is where that
// gets decided once instead of in five different services.
//
// The token is attached here (Scoped, per circuit) rather than by a
// DelegatingHandler - see AccessTokenProvider for why.
public class FlexiSpaceApiClient
{
    private readonly HttpClient _http;
    private readonly IAccessTokenProvider _accessTokenProvider;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public FlexiSpaceApiClient(IHttpClientFactory httpClientFactory, IAccessTokenProvider accessTokenProvider)
    {
        _http = httpClientFactory.CreateClient("FlexiSpaceApi");
        _accessTokenProvider = accessTokenProvider;
    }

    // Re-acquires (MSAL silently caches/refreshes) and attaches the bearer
    // token before every call. A null token (not signed in, or no usable session)
    // means the request goes out unauthenticated, so the API's own 401
    // comes back cleanly rather than this throwing.
    private async Task EnsureAuthorizationAsync()
    {
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        _http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.GetAsync(path);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // Downloads a file (e.g. the bookings CSV) as raw bytes, plus the file
    // name the API suggested in Content-Disposition. Null content = the
    // call failed (not signed in, wrong role, API down).
    public async Task<(byte[]? Content, string? FileName)> GetFileAsync(string path)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.GetAsync(path);
        if (!response.IsSuccessStatusCode) return (null, null);

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return (await response.Content.ReadAsByteArrayAsync(), fileName);
    }

    // Returns (found, value) so callers can tell "genuinely not found"
    // (404) apart from "empty result" without a second overload.
    public async Task<(bool Found, T? Value)> TryGetAsync<T>(string path)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound) return (false, default);
        if (!response.IsSuccessStatusCode) return (false, default);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (true, value);
    }

    public async Task<ApiResult<TResult>> PostAsync<TBody, TResult>(string path, TBody body)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        return await ToResultAsync<TResult>(response);
    }

    public async Task<bool> PostAsync<TBody>(string path, TBody body)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<ApiResult<TResult>> PutAsync<TBody, TResult>(string path, TBody body)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PutAsJsonAsync(path, body, JsonOptions);
        return await ToResultAsync<TResult>(response);
    }

    public async Task<bool> PutAsync<TBody>(string path, TBody body)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PutAsJsonAsync(path, body, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PatchAsync<TBody>(string path, TBody body)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PatchAsync(path,
            JsonContent.Create(body, options: JsonOptions));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PatchAsync(string path)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.PatchAsync(path, content: null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string path)
    {
        await EnsureAuthorizationAsync();
        var response = await _http.DeleteAsync(path);
        return response.IsSuccessStatusCode;
    }

    private static async Task<ApiResult<TResult>> ToResultAsync<TResult>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response);
            return ApiResult<TResult>.Fail(message ?? $"Request failed ({(int)response.StatusCode}).");
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return ApiResult<TResult>.Ok(default!);
        }

        var value = await response.Content.ReadFromJsonAsync<TResult>(JsonOptions);
        return ApiResult<TResult>.Ok(value!);
    }

    // Pulls a readable message out of any of the error shapes the API
    // returns, so the UI can show the real reason instead of
    // "Request failed (400)":
    //   { "errors": ["...", "..."] }               - BusinessRuleException (booking rules)
    //   { "errors": { "Field": ["..."] }, title }  - ASP.NET model validation (ProblemDetails)
    //   { "message": "..." } / { "error": "..." }  - Forbidden/NotFound and the RBAC filter
    //   "plain string"                             - BadRequest("...")
    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        string raw;
        try { raw = await response.Content.ReadAsStringAsync(); }
        catch { return null; }

        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            if (root.ValueKind != JsonValueKind.Object)
                return raw;

            if (TryGetProperty(root, "errors", out var errors))
            {
                var messages = new List<string>();
                if (errors.ValueKind == JsonValueKind.Array)
                {
                    messages.AddRange(errors.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!));
                }
                else if (errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                            messages.AddRange(field.Value.EnumerateArray()
                                .Where(e => e.ValueKind == JsonValueKind.String)
                                .Select(e => e.GetString()!));
                    }
                }

                if (messages.Count > 0) return string.Join(" ", messages);
            }

            foreach (var name in new[] { "message", "error", "detail", "title" })
            {
                if (TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return raw; // not JSON - show it as-is
        }
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public class ApiResult<T>
{
    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ApiResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ApiResult<T> Fail(string message) => new() { Success = false, ErrorMessage = message };
}
