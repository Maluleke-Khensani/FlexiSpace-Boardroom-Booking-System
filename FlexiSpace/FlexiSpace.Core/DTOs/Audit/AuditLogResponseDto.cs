namespace FlexiSpace.Core.DTOs.Audit
{
    // What the "who did what, at what time" admin screen renders one row
    // from. ActingUserName is a convenience for the UI so it doesn't have
    // to separately look up ActingUserId against the user list.
    public class AuditLogResponseDto
    {
        public int Id { get; set; }

        public int ActingUserId { get; set; }

        public string? ActingUserName { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }
    }
}
