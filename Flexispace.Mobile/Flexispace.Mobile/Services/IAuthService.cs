using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    bool IsGuest { get; }
    event EventHandler? AuthStateChanged;
    void ContinueAsGuest();
    Task<AuthResult> LoginWithMicrosoftAsync();
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
}
