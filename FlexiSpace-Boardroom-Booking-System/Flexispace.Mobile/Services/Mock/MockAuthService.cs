using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Mock;

public class MockAuthService : IAuthService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event EventHandler? AuthStateChanged;

    public Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = SeedData.Users.FirstOrDefault(u =>
            u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        CurrentUser = user;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new AuthResult
        {
            Success = user is not null,
            Message = user is not null
                ? $"Welcome, {user.Name}."
                : "Invalid email or password."
        });
    }

    public Task<AuthResult> LoginWithMicrosoftAsync() =>
        Task.FromResult(new AuthResult
        {
            Success = false,
            Message = "Microsoft sign-in requires the live API (UseMockServices = false) and azuread.local.json."
        });

    public Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();
        if (SeedData.Users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new AuthResult
            {
                Success = false,
                Message = "An account with this email already exists. Sign in instead."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(email) ||
            request.Password.Length < 6)
        {
            return Task.FromResult(new AuthResult
            {
                Success = false,
                Message = "Please complete all fields. Password must be at least 6 characters."
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            Email = email,
            Password = request.Password,
            Role = UserRole.Client
        };
        SeedData.Users.Add(user);
        CurrentUser = user;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        return Task.FromResult(new AuthResult
        {
            Success = true,
            Message = "Account created. Welcome to Flexispace."
        });
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
