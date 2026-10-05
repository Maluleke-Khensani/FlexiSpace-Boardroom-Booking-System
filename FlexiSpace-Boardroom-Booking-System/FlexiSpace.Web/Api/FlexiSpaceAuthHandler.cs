using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace Flexispace.Web.Api;

public sealed class FlexiSpaceAuthHandler(
    ITokenAcquisition tokenAcquisition,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            try
            {
                var scopes = configuration.GetSection("FlexiSpaceApi:Scopes").Get<string[]>()
                    ?? [configuration["FlexiSpaceApi:Scopes"] ?? string.Empty];
                scopes = scopes.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (scopes.Length > 0)
                {
                    var token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                // Cookie is present but a fresh consent/login is required for the API scope.
            }
            catch (Exception)
            {
                // Anonymous catalog calls can still proceed without a token.
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
