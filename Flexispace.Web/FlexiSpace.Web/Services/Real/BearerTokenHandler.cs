using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Flexispace.Web.Services.Real;

// Attached to the named "FlexiSpaceApi" HttpClient (see Program.cs).
// Before every outgoing request, silently acquires an access token for
// the signed-in user (using the same OIDC session Microsoft.Identity.Web
// set up at login) and attaches it as a Bearer header - this is exactly
// the same token the TestClient's "Authorize" button pastes into Swagger
// by hand, just fetched and attached automatically here.
public class BearerTokenHandler : DelegatingHandler
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly FlexiSpaceApiOptions _options;

    public BearerTokenHandler(
        ITokenAcquisition tokenAcquisition,
        IOptions<FlexiSpaceApiOptions> options)
    {
        _tokenAcquisition = tokenAcquisition;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // GetAccessTokenForUserAsync throws (MicrosoftIdentityWebChallengeUserException)
        // when there's no signed-in user or the session needs re-consent.
        // Let that happen for a signed-in-but-stale session - the caller
        // will see it as a failed HTTP call and the UI already treats API
        // failures as "couldn't load", which is the right degraded state
        // here. For a plain "not signed in" request, skip straight to
        // sending without a token so public/anonymous 401s come back
        // cleanly from the API rather than throwing from this handler.
        try
        {
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync(
                new[] { _options.Scope });

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // No usable session - send unauthenticated, API will 401.
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
