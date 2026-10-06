namespace Flexispace.Mobile.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>API integer user id (for create-booking UserId).</summary>
    public int ApiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public string? LocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid EntraObjectId { get; set; }
    public string Password { get; set; } = string.Empty;
}
