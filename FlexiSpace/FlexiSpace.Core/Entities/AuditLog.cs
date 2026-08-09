

using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        public AuditAction Action { get; set; }
        public required string EntityName { get; set; }

        public required string EntityId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


        public  string? OldValues { get; set; }
        public  string? NewValues { get; set; }

    }
}
