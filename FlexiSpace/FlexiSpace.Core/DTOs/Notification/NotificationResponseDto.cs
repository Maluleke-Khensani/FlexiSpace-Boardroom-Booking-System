using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Notification
{
    public class NotificationResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        // Note: the data model handover describes this as nullable
        // ("a notification may be created before it is delivered"), but
        // the actual Notification entity has SentAt as a non-nullable
        // DateTime defaulting to UtcNow - i.e. every notification is
        // currently modelled as sent the instant it's created. Left as-is
        // here to match the real entity; flag with Khensani/Tino if the
        // "created but not yet sent" state needs to be representable
        // later (would need a migration).
        public DateTime SentAt { get; set; }
    }
}
