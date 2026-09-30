using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Microsoft.AspNetCore.Http;

namespace Flexispace.Web.Services.Real;

// Real sign-in is Microsoft Entra ID via Microsoft.Identity.Web (wired in
// Program.cs) - the actual "log in" action happens outside this class
// entirely, as a full-page redirect to MicrosoftIdentity/Account/SignIn
// (see the "Sign in with Microsoft" button on Login.razor). This class's
// job is narrower: once Entra sign-in has happened, figure out who that
// is in FlexiSpace's own terms (Id, Role, LocationId) by calling
// GET /api/user/me, and expose that the same way IAuthService always has
// so the rest of the app (ViewModels, RolePermissions checks) doesn't
// need to know or care that the login mechanism changed.
//
// Registered Scoped in Program.cs (one instance per Blazor circuit, i.e.
// per signed-in browser tab) - this MUST NOT be Singleton like the mock
// was, or one user's CurrentUser would leak into every other user's
// circuit. Same reasoning applies to every other Real*Service in this
// folder that reads HttpContext or calls the API on the user's behalf.
public class RealAuthService : IAuthService, IAsyncAuthInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FlexiSpaceApiClient _api;

    private bool _initialized;
    private User? _currentUser;

    public event EventHandler? AuthStateChanged;

    public RealAuthService(IHttpContextAccessor httpContextAccessor, FlexiSpaceApiClient api)
    {
        _httpContextAccessor = httpContextAccessor;
        _api = api;
    }

    // True the moment Entra sign-in succeeded, even if this person hasn't
    // been provisioned into FlexiSpace's Users table yet - see CurrentUser
    // below for how those two states are told apart.
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    // Null in two cases the UI is expected to treat the same way (see
    // UserController.GetMyProfile's comment on the API side): not signed
    // in at all, or signed in but not yet provisioned by an Administrator.
    // Callers that already gate on IsAuthenticated first (e.g. MainLayout)
    // can tell the two apart themselves if they need to.
    public User? CurrentUser => _currentUser;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (!IsAuthenticated) return;

        var (found, apiUser) = await _api.TryGetAsync<ApiUser>("api/user/me");
        _currentUser = found && apiUser is not null ? MapUser(apiUser) : null;
    }

    // Real sign-in doesn't take an email/password - it's the "Sign in
    // with Microsoft" redirect on Login.razor. This stays implemented
    // (rather than removed from the interface, which MockAuthService also
    // implements) so nothing that references IAuthService.LoginAsync
    // elsewhere fails to compile; it simply isn't the real entry point
    // any more.
    public Task<bool> LoginAsync(string email, string password) =>
        Task.FromResult(false);

    public Task LogoutAsync()
    {
        _currentUser = null;
        _initialized = false;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        // The actual sign-out redirect (clearing the Entra session too,
        // not just this app's cookie) is triggered separately - see
        // Profile.razor / ProfileViewModel.LogoutAsync, which does a
        // forceLoad navigate to MicrosoftIdentity/Account/SignOut.
        return Task.CompletedTask;
    }

    // Demo-user switching was a prototype-only convenience for trying
    // different roles without real accounts. There's no equivalent with
    // real Entra sign-in - switching roles now means an Administrator
    // changing your role via PUT /api/user/{id} and you signing in again.
    public Task SwitchDemoUserAsync(string email) => Task.CompletedTask;

    public IReadOnlyList<User> GetDemoUsers() => Array.Empty<User>();

    private static User MapUser(ApiUser api) => new()
    {
        // The prototype's User.Id is a Guid; the real backend's is an int.
        // There's no lossless int->Guid mapping, and nothing in the UI
        // does arithmetic on User.Id (it's only ever compared/displayed),
        // so EntraObjectId - which the API already returns and which
        // uniquely and stably identifies this person - is used here
        // instead of trying to fabricate a Guid from the int id.
        Id = api.EntraObjectId,
        Name = $"{api.FirstName} {api.LastName}".Trim(),
        Email = api.Email,
        Role = MapRole(api.Role),
        LocationId = api.LocationId?.ToString(),
        Password = string.Empty
    };

    internal static UserRole MapRole(ApiUserRole role) => role switch
    {
        ApiUserRole.CentreManager => UserRole.CentreManager,
        ApiUserRole.Staff => UserRole.Staff,
        ApiUserRole.Administrator => UserRole.Administrator,
        ApiUserRole.Client => UserRole.Client,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown API role")
    };
}
