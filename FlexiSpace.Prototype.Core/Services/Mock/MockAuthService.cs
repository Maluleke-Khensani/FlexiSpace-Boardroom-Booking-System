using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

public class MockAuthService : IAuthService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event EventHandler? AuthStateChanged;

    public Task<bool> LoginAsync(string email, string password)
    {
        var user = SeedData.Users.FirstOrDefault(u =>
            u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        CurrentUser = user;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(user is not null);
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task SwitchDemoUserAsync(string email)
    {
        var user = SeedData.Users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (user is not null)
        {
            CurrentUser = user;
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<User> GetDemoUsers() => SeedData.Users;
}

