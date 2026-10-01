using Microsoft.Identity.Client;
#if WINDOWS
using Microsoft.Identity.Client.Broker;
#endif

namespace Flexispace.Mobile.Services.Real;

// Owns the single IPublicClientApplication instance for the app's
// lifetime and is the only place that talks to MSAL.NET directly.
// RealAuthService (interactive sign-in/sign-out) and BearerTokenHandler
// (silent token refresh on every API call) both depend on this instead
// of on each other, which avoids a circular dependency between "the
// service that knows who's signed in" and "the handler that attaches
// that person's token to outgoing requests".
//
// Registered Singleton in MauiProgram.cs - MSAL's own token cache is
// already an in-memory singleton internally, so wrapping it in a second,
// shorter-lived instance would just mean re-authenticating unnecessarily.
public class MsalTokenProvider
{
    private readonly IPublicClientApplication _pca;

    public MsalTokenProvider()
    {
        var builder = PublicClientApplicationBuilder
            .Create(ApiConfig.ClientId)
            .WithAuthority(ApiConfig.Authority)
            .WithDefaultRedirectUri();

#if WINDOWS
        // Sign in through Windows' account broker (WAM) rather than a
        // browser on http://localhost. The app registration's SPA platform
        // (the TestClient, http://localhost:5173) claims every localhost
        // port, so a localhost sign-in fails with AADSTS9002327. WAM uses
        // its own redirect URI instead - see ApiConfig.
        builder = builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
        {
            Title = "Flexispace"
        });
#endif

        _pca = builder.Build();
    }

    // Tries the cached/refresh-token flow first - this is what
    // BearerTokenHandler calls before every API request, and it must
    // never pop a sign-in window itself (a background HTTP call is the
    // wrong moment to interrupt the person with a browser popup). Returns
    // null on any failure so the caller can decide what "no token right
    // now" means for that request.
    public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        if (account is null) return null;

        try
        {
            return await _pca.AcquireTokenSilent(ApiConfig.ApiScopes, account)
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            // Refresh token expired/revoked/needs re-consent - only an
            // interactive sign-in can resolve this, which the caller (a
            // background HTTP handler) must not attempt on its own.
            return null;
        }
        catch (MsalException)
        {
            return null;
        }
    }

    // Pops the sign-in UI (the Windows account picker via WAM). Only called from an explicit user action - the Login page's
    // "Sign in with Microsoft" button - never from inside BearerTokenHandler.
    public async Task<InteractiveSignIn> AcquireTokenInteractiveAsync()
    {
        try
        {
            var builder = _pca.AcquireTokenInteractive(ApiConfig.ApiScopes);

#if WINDOWS
            var handle = WindowHandleProvider.GetActiveWindowHandle();
            if (handle != IntPtr.Zero)
            {
                builder = builder.WithParentActivityOrWindow(handle);
            }
#endif

            return new InteractiveSignIn(await builder.ExecuteAsync(), false, null);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            return new InteractiveSignIn(null, true, null);
        }
        catch (MsalException ex)
        {
            // e.g. AADSTS50011 (redirect URI not registered) or
            // AADSTS7000218 (public client flows not allowed) - see ApiConfig.
            var firstLine = ex.Message.Split('\n')[0].Trim();
            return new InteractiveSignIn(null, false, firstLine);
        }
    }

    public async Task SignOutAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _pca.RemoveAsync(account);
        }
    }
}

public sealed record InteractiveSignIn(AuthenticationResult? Result, bool Cancelled, string? Error);
