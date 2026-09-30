using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flexispace.Mobile.Services.Real;

// Thin wrapper around the named "FlexiSpaceApi" HttpClient (registered in
// MauiProgram.cs with the API's base address and BearerTokenHandler
// attached). Centralises JSON options and the "treat 401/403/404 as a
// clean failure, not an exception" behaviour every Real*Service needs -
// none of the app's own service interfaces have a concept of "the API
// said no", they just return false/null/empty. Ported from
// Flexispace.Web's identical helper.
public class FlexiSpaceApiClient
{
    private readonly HttpClient _http;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public FlexiSpaceApiClient(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("FlexiSpaceApi");
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    // Returns (found, value) so callers can tell "genuinely not found"
    // (404) apart from "empty result" without a second overload.
    public async Task<(bool Found, T? Value)> TryGetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound) return (false, default);
        if (!response.IsSuccessStatusCode) return (false, default);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (true, value);
    }

    public async Task<ApiResult<TResult>> PostAsync<TBody, TResult>(string path, TBody body)
    {
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        return await ToResultAsync<TResult>(response);
    }

    public async Task<bool> PostAsync<TBody>(string path, TBody body)
    {
        var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<ApiResult<TResult>> PutAsync<TBody, TResult>(string path, TBody body)
    {
        var response = await _http.PutAsJsonAsync(path, body, JsonOptions);
        return await ToResultAsync<TResult>(response);
    }

    public async Task<bool> PutAsync<TBody>(string path, TBody body)
    {
        var response = await _http.PutAsJsonAsync(path, body, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PatchAsync<TBody>(string path, TBody body)
    {
        var response = await _http.PatchAsync(path,
            JsonContent.Create(body, options: JsonOptions));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PatchAsync(string path)
    {
        var response = await _http.PatchAsync(path, content: null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string path)
    {
        var response = await _http.DeleteAsync(path);
        return response.IsSuccessStatusCode;
    }

    private static async Task<ApiResult<TResult>> ToResultAsync<TResult>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? message = null;
            try
            {
                var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(JsonOptions);
                message = body?.Message ?? body?.Error;
            }
            catch
            {
                // Some error responses are a bare JSON string, not an
                // object - fall back to the raw text rather than failing
                // to report an error at all.
                try { message = await response.Content.ReadAsStringAsync(); } catch { /* ignore */ }
            }

            return ApiResult<TResult>.Fail(message ?? $"Request failed ({(int)response.StatusCode}).");
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return ApiResult<TResult>.Ok(default!);
        }

        var value = await response.Content.ReadFromJsonAsync<TResult>(JsonOptions);
        return ApiResult<TResult>.Ok(value!);
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
