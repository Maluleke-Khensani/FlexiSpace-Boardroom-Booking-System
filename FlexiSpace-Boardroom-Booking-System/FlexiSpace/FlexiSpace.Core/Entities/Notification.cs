using FlexiSpace.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public required string Title { get; set; }

        public NotificationType Type { get; set; }

        public required string Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
