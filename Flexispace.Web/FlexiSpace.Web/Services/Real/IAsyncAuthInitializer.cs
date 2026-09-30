namespace Flexispace.Web.Services.Real;

// Not part of the shared IAuthService interface (that one's also
// implemented by MockAuthService, which has nothing async to do at
// startup). MainLayout checks for this interface and awaits it once per
// circuit before rendering @Body, so every page's first render already
// has CurrentUser resolved (or confirmed null) instead of racing the
// GET /api/user/me call.
public interface IAsyncAuthInitializer
{
    Task EnsureInitializedAsync();
}
