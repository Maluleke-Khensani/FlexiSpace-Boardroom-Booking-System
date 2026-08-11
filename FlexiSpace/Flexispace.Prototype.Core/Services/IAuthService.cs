using Flexispace.Core.Models;

namespace Flexispace.Core.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event EventHandler? AuthStateChanged;
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task SwitchDemoUserAsync(string email);
    IReadOnlyList<User> GetDemoUsers();
}

