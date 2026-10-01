using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Flexispace.Web.Services.Real;

// Gets the signed-in user's access token for the FlexiSpace API.
//
// In a Blazor Server circuit, IHttpContextAccessor.HttpContext isn't
// reliable (and DelegatingHandlers run outside the circuit's DI scope), so
// ITokenAcquisition can't find the user on its own - every call failed
// with MsalUiRequiredException / "user_null" and pages hung on
// "Loading your account...". Instead this reads the user from
// AuthenticationStateProvider (flowed in by AddCascadingAuthenticationState
// in Program.cs) and passes it to ITokenAcquisition explicitly. It's
// Scoped, so it always runs in the current circuit's scope.
// See https://learn.microsoft.com/aspnet/core/blazor/security/server/interactive-server-side-rendering.
public class AccessTokenProvider : IAccessTokenProvider
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly FlexiSpaceApiOptions _options;

    public AccessTokenProvider(
        AuthenticationStateProvider authenticationStateProvider,
        ITokenAcquisition tokenAcquisition,
        IOptions<FlexiSpaceApiOptions> options)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _tokenAcquisition = tokenAcquisition;
        _options = options.Value;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true) return null;

        try
        {
            // Passing `user` explicitly is the whole fix - see the class
            // comment above. Without it, GetAccessTokenForUserAsync falls
            // back to IHttpContextAccessor.HttpContext.User, which is
            // exactly what was failing.
            return await _tokenAcquisition.GetAccessTokenForUserAsync(
                new[] { _options.Scope },
                user: user);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // No usable session/consent - let the caller send the request
            // unauthenticated so the API's own 401 comes back cleanly.
            return null;
        }
    }
}
