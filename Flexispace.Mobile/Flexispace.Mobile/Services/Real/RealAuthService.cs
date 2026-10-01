using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Sign-in is Microsoft Entra ID via MSAL.NET (MsalTokenProvider). Once MSAL
// confirms who signed in, GET api/user/me resolves that person's FlexiSpace
// identity (role, location). An Entra account that isn't in FlexiSpace's
// Users table gets a clear message instead of a half-signed-in app.
//
// Singleton (see MauiProgram.cs): one signed-in person per device.
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

    public async Task<SignInResult> SignInAsync()
    {
        var signIn = await _tokenProvider.AcquireTokenInteractiveAsync();
        if (signIn.Cancelled)
            return SignInResult.Cancelled;
        if (signIn.Result is null)
            return SignInResult.Failed($"Microsoft sign-in didn't complete. {signIn.Error}".Trim());

        bool found;
        ApiUser? apiUser;
        try
        {
            (found, apiUser) = await _api.TryGetAsync<ApiUser>("api/user/me");
        }
        catch (HttpRequestException)
        {
            await _tokenProvider.SignOutAsync();
            return SignInResult.Failed(
                $"Signed in, but the FlexiSpace API at {ApiConfig.ApiBaseUrl} isn't reachable. Make sure it's running.");
        }

        if (!found || apiUser is null)
        {
            // Clear the MSAL account so the next attempt can pick a different one.
            await _tokenProvider.SignOutAsync();
            return SignInResult.Failed(
                $"{signIn.Result.Account.Username} isn't set up in FlexiSpace yet. Ask an administrator to add you.");
        }

        _currentUser = MapUser(apiUser);
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        await RecordAsync("api/user/me/sign-in?client=mobile");
        return SignInResult.Success;
    }

    public async Task LogoutAsync()
    {
        // Recorded before the token is cleared - the call needs it.
        if (_currentUser is not null)
            await RecordAsync("api/user/me/sign-out?client=mobile");

        _currentUser = null;
        await _tokenProvider.SignOutAsync();
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Login history for the audit log. Best effort: never blocks sign-in/out.
    private async Task RecordAsync(string path)
    {
        try { await _api.PostAsync(path, new { }); }
        catch { /* the audit entry is nice to have, not worth an error */ }
    }

    private static User MapUser(ApiUser api) => new()
    {
        // The app's User.Id is a Guid and the API's is an int, so the
        // Entra object id (stable and unique per person) is used instead.
        // Same approach as Flexispace.Web's RealAuthService.
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
