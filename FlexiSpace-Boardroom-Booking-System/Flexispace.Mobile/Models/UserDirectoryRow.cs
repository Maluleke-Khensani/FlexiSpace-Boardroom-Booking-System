namespace Flexispace.Mobile.Models;

public sealed class UserDirectoryRow
{
    public int ApiId { get; init; }
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public string RoleLabel { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = string.Empty;
    public bool CanManage { get; init; }
    public Guid EntraObjectId { get; init; }
}
