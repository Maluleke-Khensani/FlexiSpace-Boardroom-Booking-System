namespace Flexispace.Core.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public string? LocationId { get; set; }
    public string Password { get; set; } = string.Empty;
}

