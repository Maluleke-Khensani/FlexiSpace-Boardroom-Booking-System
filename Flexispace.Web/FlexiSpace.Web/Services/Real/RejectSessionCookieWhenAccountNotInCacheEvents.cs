using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace Flexispace.Web.Services.Real;

// Rejects the app's sign-in cookie when MSAL's token cache no longer has
// an account for that user, forcing a clean re-sign-in instead of a
// half-signed-in state.
//
// Why this is needed: Program.cs uses AddInMemoryTokenCaches(), so every
// time the Web app restarts (every `dotnet run` during development, or an
// app pool recycle in production) the token cache is wiped - but the
// browser's auth cookie survives. The result was the app believing you
// were signed in (valid cookie) while every API call failed with
// MsalUiRequiredException / user_null (no cached account to get a token
// for), so GET api/user/me returned 401, CurrentUser stayed null, and
// the UI showed "Guest" with empty data.
//
// This is Microsoft's documented pattern for exactly this situation:
// https://learn.microsoft.com/entra/identity-platform/scenario-web-app-call-api-acquire-token
// (see "RejectSessionCookieWhenAccountNotInCacheEvents").
public class RejectSessionCookieWhenAccountNotInCacheEvents : CookieAuthenticationEvents
{
    private readonly string[] _scopes;

    public RejectSessionCookieWhenAccountNotInCacheEvents(string scope)
    {
        _scopes = new[] { scope };
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        try
        {
            var tokenAcquisition = context.HttpContext.RequestServices
                .GetRequiredService<ITokenAcquisition>();

            // Served from the in-memory cache when the account is there
            // (cheap), so this only costs anything real right after a
            // restart - which is exactly the case it's here to catch.
            await tokenAcquisition.GetAccessTokenForUserAsync(_scopes, user: context.Principal);
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
            when (AccountDoesNotExistInTokenCache(ex))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static bool AccountDoesNotExistInTokenCache(MicrosoftIdentityWebChallengeUserException ex) =>
        ex.InnerException is MsalUiRequiredException { ErrorCode: "user_null" };
}
