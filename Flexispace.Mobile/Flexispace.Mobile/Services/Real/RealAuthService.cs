using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Real sign-in is Microsoft Entra ID via MSAL.NET (MsalTokenProvider) -
// LoginAsync pops the interactive sign-in window regardless of what's
// typed into the Email/Password fields on LoginPage (those stay in the
// UI only so the "Use demo account" buttons and layout don't need
// touching; tapping either now just triggers the same real sign-in).
// Once MSAL confirms who signed in, GET /api/user/me resolves that
// person's FlexiSpace identity (Id, Role, LocationId) - not every Entra
// user is necessarily provisioned into FlexiSpace's Users table yet, see
// CurrentUser below for how that's told apart from "not signed in at
// all".
//
// Registered Singleton in MauiProgram.cs - this is a single-user device
// app (unlike Flexispace.Web, which needs one instance per browser
// circuit), so one shared CurrentUser for the app's lifetime is correct,
// matching how MockAuthService was already registered.
public class RealAuthService : IAuthService
{
    private readonly MsalTokenProvider _tokenProvider;
    private readonly FlexiSpaceApiClient _api;

    private User? _currentUser;

    public RealAuthService(MsalTokenProvider tokenProvider, FlexiSpaceApiClient api)
    {
        _tokenProvider = tokenProvider;
        _api = api;
    }

    public event EventHandler? AuthStateChanged;

    public User? CurrentUser => _currentUser;

    public bool IsAuthenticated => _currentUser is not null;

    // email/password are ignored - see the class comment. Returns true
    // only once both the Entra sign-in AND the /api/user/me lookup
    // succeed, so "logged in but nobody's provisioned you yet" correctly
    // shows the same "couldn't log in" error LoginViewModel already
    // displays for bad demo credentials, rather than pretending to be
    // signed in with a null profile.
    public async Task<bool> LoginAsync(string email, string password)
    {
        var result = await _tokenProvider.AcquireTokenInteractiveAsync();
        if (result is null)
        {
            return false;
        }

        var (found, apiUser) = await _api.TryGetAsync<ApiUser>("api/user/me");
        _currentUser = found && apiUser is not null ? MapUser(apiUser) : null;

        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return _currentUser is not null;
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await _tokenProvider.SignOutAsync();
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Demo-user switching was a prototype-only convenience for trying
    // different roles without real accounts. There's no equivalent with
    // real Entra sign-in - switching roles now means an Administrator
    // changing your role via the website's admin dashboard (PUT
    // /api/user/{id}) and you signing in again.
    public Task SwitchDemoUserAsync(string email) => Task.CompletedTask;

    public IReadOnlyList<User> GetDemoUsers() => Array.Empty<User>();

    private static User MapUser(ApiUser api) => new()
    {
        // The prototype's User.Id is a Guid; the real backend's is an
        // int. There's no lossless int->Guid mapping, and nothing in the
        // UI does arithmetic on User.Id (it's only ever compared/
        // displayed), so EntraObjectId - which the API already returns
        // and which uniquely and stably identifies this person - is used
        // here instead of fabricating a Guid from the int id. Same
        // approach as Flexispace.Web's RealAuthService.
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
