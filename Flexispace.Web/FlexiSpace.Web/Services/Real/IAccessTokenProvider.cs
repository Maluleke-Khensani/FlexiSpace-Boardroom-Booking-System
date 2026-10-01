namespace Flexispace.Web.Services.Real;

// Provides a bearer access token (scoped to the FlexiSpace API) for
// whoever is signed in on this circuit, or null if there isn't a usable
// signed-in session. See AccessTokenProvider.
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync();
}
