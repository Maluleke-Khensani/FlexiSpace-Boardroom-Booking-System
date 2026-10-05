using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flexispace.Mobile.Services.Api;

public sealed class ApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly HttpClient _http;
    private readonly ITokenStorage _tokens;

    public ApiClient(HttpClient http, ITokenStorage tokens, ApiSettings settings)
    {
        _http = http;
        _tokens = tokens;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(settings.BaseUrl);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _http.SendAsync(request, ct);
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            using var response = await SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (HttpRequestException)
        {
            return default;
        }
        catch (TaskCanceledException)
        {
            return default;
        }
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string relativeUrl, T body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(string relativeUrl, T body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, relativeUrl)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PatchAsJsonAsync<T>(string relativeUrl, T body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, relativeUrl)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        return await SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PatchAsync(string relativeUrl, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, relativeUrl);
        return await SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, relativeUrl);
        return await SendAsync(request, ct);
    }
}
