using System.Net.Http.Headers;

namespace Flexispace.Mobile.Services.Real;

// Attached to the named "FlexiSpaceApi" HttpClient (see MauiProgram.cs).
// Before every outgoing request, silently tries to get a cached/refreshed
// access token from MSAL and attaches it as a Bearer header. Mirrors
// Flexispace.Web's BearerTokenHandler, but goes through MsalTokenProvider
// (silent-only) instead of ITokenAcquisition, since there's no server-side
// OIDC session here to piggy-back on.
public class BearerTokenHandler : DelegatingHandler
{
    private readonly MsalTokenProvider _tokenProvider;

    public BearerTokenHandler(MsalTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Silent only - a background HTTP call is the wrong moment to pop
        // a sign-in window. No cached/refreshable token (not signed in,
        // or the session needs re-consent) just means the request goes
        // out unauthenticated and the API answers with a clean 401/403,
        // which every Real*Service already treats as "couldn't load"/
        // "not allowed" rather than throwing.
        var result = await _tokenProvider.AcquireTokenSilentAsync();

        if (result is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", result.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
