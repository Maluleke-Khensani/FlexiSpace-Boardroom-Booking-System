namespace Flexispace.Mobile.Services.Real;

// Central place for the handful of values the real backend needs that
// don't belong hardcoded inside individual services. MAUI has no
// appsettings.json/IConfiguration pipeline wired up by default the way
// ASP.NET does (see Flexispace.Web's appsettings.json for the web
// equivalent of these same values) - these are plain constants instead.
//
// Entra app registrations (Khensani owns them): the API, the Web app and
// this Mobile app each have their own. This app signs in as the Mobile
// registration and asks for a token to call the API.
//
// The Mobile registration needs, once:
// 1. Authentication -> "Mobile and desktop applications" platform with the
//    redirect URI used by Windows' account broker (WAM - see
//    MsalTokenProvider):
//      ms-appx-web://microsoft.aad.brokerplugin/85378c65-ead1-4b71-956a-389142bd3342
//    and "Allow public client flows" set to Yes. Missing it gives
//    AADSTS50011 (redirect URI not registered) or AADSTS7000218.
// 2. API permissions -> the API registration's "access_as_user" delegated
//    permission (with admin consent), so it can request the token below.
public static class ApiConfig
{
    public const string TenantId = "2427e95a-1238-4880-8236-996db36bc62c";

    // This app's own registration ("Mobile").
    public const string ClientId = "85378c65-ead1-4b71-956a-389142bd3342";

    // The API's registration - only used to name the scope we ask for.
    public const string ApiClientId = "77163347-59be-48f4-8675-535af30a3a53";

    public static string Authority => $"https://login.microsoftonline.com/{TenantId}";

    // The API's exposed scope - the same one the Web app requests.
    public static readonly string[] ApiScopes =
    {
        $"api://{ApiClientId}/access_as_user"
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
