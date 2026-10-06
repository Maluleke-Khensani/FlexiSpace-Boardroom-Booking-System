using System.Text.Json;

namespace Flexispace.Mobile.Services.Api;

public sealed class ApiSettings
{
    /// <summary>Trailing slash required.</summary>
    public string BaseUrl { get; set; } = "https://flexispace-exdye9dzg3bhejh9.southafricanorth-01.azurewebsites.net/";

    /// <summary>
    /// When true, MauiProgram registers Mock* services.
    /// </summary>
    public bool UseMockServices { get; set; }

    /// <summary>Entra tenant id (from local azuread.local.json — not committed).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Public mobile app client id (no secret).</summary>
    public string MobileClientId { get; set; } = string.Empty;

    /// <summary>API app client id used as token audience / scope host.</summary>
    public string ApiClientId { get; set; } = string.Empty;

    /// <summary>Delegated scope, e.g. api://{apiClientId}/access_as_user</summary>
    public string ApiScope { get; set; } = string.Empty;

    /// <summary>
    /// Optional override. Default is msal{MobileClientId}://auth (Entra Mobile/desktop scheme).
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsMicrosoftSignInConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(MobileClientId) &&
        !string.IsNullOrWhiteSpace(ApiScope);

    public static ApiSettings Load()
    {
        var settings = new ApiSettings
        {
            UseMockServices = false,
            BaseUrl = "https://flexispace-exdye9dzg3bhejh9.southafricanorth-01.azurewebsites.net/"
        };

        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "azuread.local.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "azuread.local.json"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Flexispace",
                    "azuread.local.json"),
                Path.Combine(AppContext.BaseDirectory, "azuread.local.example.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "azuread.local.example.json")
            };

            var path = candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
            if (path is null)
                return settings;

            using var stream = File.OpenRead(path);
            var local = JsonSerializer.Deserialize<AzureAdLocalFile>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (local is null) return settings;

            if (!string.IsNullOrWhiteSpace(local.BaseUrl))
                settings.BaseUrl = local.BaseUrl.EndsWith('/') ? local.BaseUrl : local.BaseUrl + "/";
            settings.TenantId = local.TenantId?.Trim() ?? string.Empty;
            settings.MobileClientId = local.MobileClientId?.Trim() ?? string.Empty;
            settings.ApiClientId = local.ApiClientId?.Trim() ?? string.Empty;
            settings.ApiScope = local.ApiScope?.Trim() ?? string.Empty;
            settings.RedirectUri = local.RedirectUri?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(settings.ApiScope) && !string.IsNullOrWhiteSpace(settings.ApiClientId))
                settings.ApiScope = $"api://{settings.ApiClientId}/.default";
        }
        catch
        {
            // Keep defaults — Microsoft sign-in will report it isn't configured.
        }

        return settings;
    }

    private sealed class AzureAdLocalFile
    {
        public string? BaseUrl { get; set; }
        public string? TenantId { get; set; }
        public string? MobileClientId { get; set; }
        public string? ApiClientId { get; set; }
        public string? ApiScope { get; set; }
        public string? RedirectUri { get; set; }
    }
}
