using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace FlexiSpace.API.Services;

/// <summary>
/// Verifies a username/password against Microsoft Entra (ROPC) using the API app registration.
/// Passwords are never stored — only checked with Azure and discarded.
/// Guest / MFA accounts often cannot use ROPC; those must use interactive MSAL.
/// </summary>
public sealed class EntraPasswordLoginService(IConfiguration config, IHttpClientFactory httpClientFactory)
{
    public sealed record Result(Guid ObjectId, string Email, string? DisplayName);
    public sealed record Attempt(Result? Success, string? ErrorCode, string? ErrorDescription);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(config["AzureAd:TenantId"]) &&
        !string.IsNullOrWhiteSpace(config["AzureAd:ClientId"]) &&
        !string.IsNullOrWhiteSpace(config["AzureAd:ClientSecret"]);

    public async Task<Attempt> TryValidateAsync(string username, string password, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new Attempt(null, "not_configured", null);

        var tenantId = config["AzureAd:TenantId"]!;
        var clientId = config["AzureAd:ClientId"]!;
        var clientSecret = config["AzureAd:ClientSecret"]!;

        var client = httpClientFactory.CreateClient(nameof(EntraPasswordLoginService));
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["username"] = username.Trim(),
            ["password"] = password,
            ["scope"] = "openid profile email"
        });

        using var response = await client.PostAsync(
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
            form,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        if (!response.IsSuccessStatusCode)
        {
            var code = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "error";
            var desc = doc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : null;
            return new Attempt(null, code, desc);
        }

        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenEl) &&
            !doc.RootElement.TryGetProperty("access_token", out idTokenEl))
            return new Attempt(null, "missing_token", null);

        var token = idTokenEl.GetString();
        if (string.IsNullOrWhiteSpace(token))
            return new Attempt(null, "missing_token", null);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var oid = jwt.Claims.FirstOrDefault(c => c.Type is "oid" or "sub")?.Value;
        if (oid is null || !Guid.TryParse(oid, out var objectId))
            return new Attempt(null, "missing_oid", null);

        var email =
            jwt.Claims.FirstOrDefault(c => c.Type is "preferred_username" or "upn" or "email")?.Value
            ?? username.Trim();
        var name = jwt.Claims.FirstOrDefault(c => c.Type is "name")?.Value;

        return new Attempt(new Result(objectId, email, name), null, null);
    }
}
