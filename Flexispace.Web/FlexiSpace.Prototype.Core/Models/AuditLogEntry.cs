namespace Flexispace.Core.Models;

public class AuditLogEntry
{
    public int Id { get; set; }
    public int ActingUserId { get; set; }
    public string ActingUserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
