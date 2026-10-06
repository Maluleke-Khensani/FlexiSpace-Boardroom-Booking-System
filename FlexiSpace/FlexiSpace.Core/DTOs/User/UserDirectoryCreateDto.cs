using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User;

/// <summary>
/// Admin creates a FlexiSpace directory row so the person can sign in with Microsoft
/// (oid is linked on first successful Entra login when email matches).
/// </summary>
public class UserDirectoryCreateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Client;
    public int? LocationId { get; set; }
    public Guid? EntraObjectId { get; set; }
}
