using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event EventHandler? AuthStateChanged;
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> LoginWithMicrosoftAsync();
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task SwitchDemoUserAsync(string email);
    IReadOnlyList<User> GetDemoUsers();
}
