namespace Flexispace.Mobile.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>API integer user id (for create-booking UserId).</summary>
    public int ApiId { get; set; }
    public Guid EntraObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public bool HasAssignedFlexiSpaceRole { get; set; } = true;
    public string? LocationId { get; set; }
    public string LocationLabel { get; set; } = "All locations";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string RoleLabel => Role == UserRole.CentreManager ? "Centre Manager" : Role.ToString();
    public string EntraRoleLabel => HasAssignedFlexiSpaceRole ? RoleLabel : "No FlexiSpace role assigned";
    public bool CanBeProvisioned => IsActive && HasAssignedFlexiSpaceRole;
    public string AccountStatusLabel => IsActive ? "Active" : "Inactive";
    public string ReferenceLabel => ApiId > 0 ? $"#{ApiId}" : string.Empty;
    public string EntraLabel => EntraObjectId == Guid.Empty ? "Microsoft link not set" : EntraObjectId.ToString();
    public string CreatedLabel => CreatedAt.Year < 2000 ? string.Empty : CreatedAt.ToString("d MMM yyyy");
    /// <summary>In-memory browse session. Never written to disk.</summary>
    public bool IsGuest { get; set; }
    public string Password { get; set; } = string.Empty;
}
