using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event EventHandler? AuthStateChanged;

    // Opens the Microsoft Entra sign-in window, then loads the person's
    // FlexiSpace profile (GET api/user/me).
    Task<SignInResult> SignInAsync();
    Task LogoutAsync();
}

// Succeeded = signed in and found in FlexiSpace. A failure with no
// ErrorMessage means the person closed the sign-in window themselves, so
// there's nothing to show.
public sealed record SignInResult(bool Succeeded, string? ErrorMessage = null)
{
    public static SignInResult Success { get; } = new(true);
    public static SignInResult Cancelled { get; } = new(false);
    public static SignInResult Failed(string message) => new(false, message);
}
