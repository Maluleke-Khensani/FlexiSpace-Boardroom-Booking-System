namespace Flexispace.Web.Services.Real;

// Kept separate from the shared IAuthService interface, which has no async
// start-up step. MainLayout checks for this interface and awaits it once per
// circuit before rendering @Body, so every page's first render already
// has CurrentUser resolved (or confirmed null) instead of racing the
// GET /api/user/me call.
public interface IAsyncAuthInitializer
{
    Task EnsureInitializedAsync();
}
