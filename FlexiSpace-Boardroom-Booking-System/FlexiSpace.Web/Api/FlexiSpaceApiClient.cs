using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;

namespace Flexispace.Web.Api;

public sealed class FlexiSpaceApiClient(
    IHttpClientFactory httpClientFactory,
    ITokenAcquisition tokenAcquisition,
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor,
    AccessTokenCache tokenCache,
    IConfiguration configuration)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http = httpClientFactory.CreateClient("FlexiSpaceApi");

    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T body, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, url, JsonContent.Create(body, options: Json), cancellationToken);

    public Task<HttpResponseMessage> PutAsJsonAsync<T>(string url, T body, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, url, JsonContent.Create(body, options: Json), cancellationToken);

    public Task<HttpResponseMessage> PatchAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Patch, url, content: null, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, url, content: null, cancellationToken);

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync(request);
        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        await AttachTokenAsync(request);
        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        try
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var user = state.User;
            if (user.Identity?.IsAuthenticated != true)
                return;

            var key = AccessTokenCache.UserKey(user);
            var token = tokenCache.Get(key);

            var scopes = configuration.GetSection("FlexiSpaceApi:Scopes").Get<string[]>()
                ?? [configuration["FlexiSpaceApi:Scopes"] ?? string.Empty];
            scopes = scopes.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (string.IsNullOrWhiteSpace(token) && scopes.Length > 0)
                token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user);

            if (string.IsNullOrWhiteSpace(token) && httpContextAccessor.HttpContext is { } http)
                token = await http.GetTokenAsync("access_token");

            if (!string.IsNullOrWhiteSpace(token))
            {
                if (!string.IsNullOrWhiteSpace(key))
                    tokenCache.Set(key, token);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            await AttachSavedTokenAsync(request);
        }
        catch (Exception)
        {
            await AttachSavedTokenAsync(request);
        }
    }

    private async Task AttachSavedTokenAsync(HttpRequestMessage request)
    {
        try
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var cached = tokenCache.Get(AccessTokenCache.UserKey(state.User));
            var saved = cached
                ?? (httpContextAccessor.HttpContext is { } http
                    ? await http.GetTokenAsync("access_token")
                    : null);
            if (!string.IsNullOrWhiteSpace(saved))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", saved);
        }
        catch
        {
            // Leave the request unauthenticated.
        }
    }

    public static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorDto>(Json);
            if (payload?.Errors is { Count: > 0 })
                return string.Join(" ", payload.Errors);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
                return payload.Message;
        }
        catch
        {
            // Fall through to status text.
        }

        var raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw)
            ? $"Request failed ({(int)response.StatusCode})."
            : raw;
    }
}
