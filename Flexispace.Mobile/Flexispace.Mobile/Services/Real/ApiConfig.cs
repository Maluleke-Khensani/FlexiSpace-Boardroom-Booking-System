namespace Flexispace.Mobile.Services.Real;

// Central place for the handful of values the real backend needs that
// don't belong hardcoded inside individual services. MAUI has no
// appsettings.json/IConfiguration pipeline wired up by default the way
// ASP.NET does (see Flexispace.Web's appsettings.json for the web
// equivalent of these same values) - these are plain constants instead,
// same spirit as the mock services' own SeedData.cs.
//
// IMPORTANT - two things must be true in Azure before sign-in works:
// 1. TenantId/ClientId below are the SAME Entra app registration the API
//    and Web already use (see Flexispace.Web/appsettings.json's AzureAd
//    section) - reused here because it's one registration serving several
//    "platforms" (the TestClient's SPA platform, Web's confidential Web
//    platform, and now this one).
// 2. That registration needs a "Mobile and desktop applications" platform
//    added (Azure Portal -> App registrations -> this app -> Authentication
//    -> Add a platform), with the redirect URI MSAL.NET's
//    PublicClientApplicationBuilder.WithDefaultRedirectUri() expects
//    (currently "http://localhost") added under it, and "Allow public
//    client flows" switched to Yes. Until Khensani (or whoever owns the
//    app registration) does that one-time step, MsalTokenProvider's
//    interactive sign-in will fail with AADSTS7000218/9002326-style
//    errors - that's a config gap, not a bug in this code.
public static class ApiConfig
{
    // Same tenant/app registration as Flexispace.Web/appsettings.json's
    // AzureAd section and the React TestClient's authConfig.js.
    public const string TenantId = "2427e95a-1238-4880-8236-996db36bc62c";
    public const string ClientId = "77163347-59be-48f4-8675-535af30a3a53";

    public static string Authority => $"https://login.microsoftonline.com/{TenantId}";

    // The API's own exposed scope - same one Web/TestClient request.
    public static readonly string[] ApiScopes =
    {
        "api://77163347-59be-48f4-8675-535af30a3a53/access_as_user"
    };

    // The API's base address. "https://localhost:7055" matches
    // FlexiSpace.API's own launch profile - correct when this app and the
    // API run on the same Windows machine (the current build target is
    // Windows-only, see Flexispace.Mobile.csproj). Swap this for the
    // machine's real address (not "localhost") once either side runs on
    // a different device, or point it at wherever the API is actually
    // deployed.
    public const string ApiBaseUrl = "https://localhost:7055";
}
