using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services.Api;
using Microsoft.Identity.Client;

namespace Flexispace.Mobile.Services.Http;

public interface IMsalAuthService
{
    Task<AuthResult> SignInInteractiveAsync();
}

/// <summary>
/// FlexiSpace-MAUI-Mobile (85378c65…). Never use the SPA web client id.
/// Windows OS browser only supports http://localhost; Android/iOS use msal{clientId}://auth.
/// </summary>
public sealed class MsalAuthService(ApiSettings settings, ITokenStorage tokens) : IMsalAuthService
{
    public async Task<AuthResult> SignInInteractiveAsync()
    {
        if (!settings.IsMicrosoftSignInConfigured)
        {
            return new AuthResult
            {
                Success = false,
                Message = "Microsoft sign-in isn’t configured yet. See azuread.local.example.json / MSAL_SETUP.md."
            };
        }

        try
        {
            var clientId = settings.MobileClientId.Trim();
            var redirectUri = ResolveRedirectUri(clientId, settings.RedirectUri);
            var tenant = string.IsNullOrWhiteSpace(settings.TenantId) ? "common" : settings.TenantId.Trim();

            var app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority($"https://login.microsoftonline.com/{tenant}")
                .WithRedirectUri(redirectUri)
                .Build();

            var scopeCandidates = BuildScopeCandidates(settings);
            AuthenticationResult? result = null;
            Exception? lastError = null;

            foreach (var account in await app.GetAccountsAsync())
                await app.RemoveAsync(account);

            foreach (var scope in scopeCandidates)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(new[] { scope })
                        .WithPrompt(Prompt.SelectAccount)
                        .WithUseEmbeddedWebView(false)
                        .ExecuteAsync();
                    if (!string.IsNullOrWhiteSpace(result.AccessToken))
                        break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (ex.Message.Contains("AADSTS9002327", StringComparison.OrdinalIgnoreCase))
                        throw;
                }
            }

            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = FormatMsalError(lastError?.Message ?? "Microsoft did not return an access token.")
                };
            }

            if (!TokenIsForApi(result.AccessToken, settings.ApiClientId))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Microsoft signed you in, but the token is for the wrong app. In Entra, on FlexiSpace-MAUI-Mobile, add API permission api://77163347-59be-48f4-8675-535af30a3a53/access_as_user and grant admin consent."
                };
            }

            await tokens.SetAccessTokenAsync(result.AccessToken);
            return new AuthResult
            {
                Success = true,
                Message = "Microsoft sign-in succeeded.",
                LinkedEmailHint = result.Account?.Username
            };
        }
        catch (MsalClientException ex)
        {
            return new AuthResult { Success = false, Message = FormatMsalError(ex.Message) };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Message = FormatMsalError(ex.Message) };
        }
    }

    /// <summary>
    /// Windows + system browser: only http://localhost (any port) is allowed by MSAL.NET.
    /// Android/iOS: custom scheme registered in Entra + manifests.
    /// </summary>
    private static string ResolveRedirectUri(string clientId, string? configured)
    {
#if WINDOWS
        // Ignore msal:// on Windows — OS browser rejects it (aka.ms/msal-net-os-browser).
        return "http://localhost";
#else
        if (!string.IsNullOrWhiteSpace(configured) &&
            !configured.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            return configured.Trim();

        return $"msal{clientId}://auth";
#endif
    }

    private static IReadOnlyList<string> BuildScopeCandidates(ApiSettings settings)
    {
        var list = new List<string>();
        void Add(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            if (!list.Contains(s, StringComparer.OrdinalIgnoreCase))
                list.Add(s);
        }

        // Only request tokens for the FlexiSpace API. Falling back to the mobile
        // app id / Graph produces a "successful" sign-in whose token Azure rejects.
        Add(settings.ApiScope);
        if (!string.IsNullOrWhiteSpace(settings.ApiClientId))
        {
            Add($"api://{settings.ApiClientId}/access_as_user");
            Add($"api://{settings.ApiClientId}/.default");
        }
        return list;
    }

    private static string FormatMsalError(string message)
    {
        if (message.Contains("AADSTS9002327", StringComparison.OrdinalIgnoreCase))
            return "Wrong Entra app: use FlexiSpace-MAUI-Mobile ClientId 85378c65…, not the SPA web client.";
        if (message.Contains("AADSTS50011", StringComparison.OrdinalIgnoreCase))
            return "Redirect URI mismatch. On FlexiSpace-MAUI-Mobile in Entra, under Mobile and desktop, add http://localhost (allows any port).";
        if (message.Contains("Only loopback redirect uri is supported", StringComparison.OrdinalIgnoreCase))
            return "Windows requires http://localhost for Microsoft sign-in. Rebuild so the app uses that redirect.";
        if (message.Contains("AADSTS65001", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("AADSTS650057", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("AADSTS70011", StringComparison.OrdinalIgnoreCase))
            return "The mobile app is not allowed to call the FlexiSpace API yet. In Entra, add delegated permission access_as_user on API 77163347-59be-48f4-8675-535af30a3a53 to FlexiSpace-MAUI-Mobile, then grant admin consent.";
        return $"Microsoft sign-in failed: {message}";
    }

    private static bool TokenIsForApi(string accessToken, string? apiClientId)
    {
        if (string.IsNullOrWhiteSpace(apiClientId))
            return true;

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
                return false;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("aud", out var aud))
                return false;

            bool Matches(string value) =>
                value.Contains(apiClientId, StringComparison.OrdinalIgnoreCase);

            if (aud.ValueKind == System.Text.Json.JsonValueKind.String)
                return Matches(aud.GetString() ?? "");

            if (aud.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in aud.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String &&
                        Matches(item.GetString() ?? ""))
                        return true;
                }
            }
        }
        catch
        {
            return true;
        }

        return false;
    }
}
