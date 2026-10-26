using Flexispace.Core.Models;

namespace Flexispace.Core.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    bool IsGuest { get; }
    bool IsSessionReady { get; }
    string? SessionError { get; }
    event EventHandler? AuthStateChanged;
    Task EnsureSessionAsync();
    Task MicrosoftSignInAsync();
    Task ContinueAsGuestAsync();
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task SwitchDemoUserAsync(string email);
    IReadOnlyList<User> GetDemoUsers();
}

