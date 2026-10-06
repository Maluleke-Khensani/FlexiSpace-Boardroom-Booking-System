namespace Flexispace.Mobile.Models;

public sealed class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>MSAL account username/UPN used to link FlexiSpace directory rows.</summary>
    public string? LinkedEmailHint { get; set; }
}
