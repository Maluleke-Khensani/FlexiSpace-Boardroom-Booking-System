using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Entities
{
    public class User
    {
        public int Id { get; set; }

        public Guid EntraObjectId { get; set; }
        public required string FirstName { get; set; }   

        public required string LastName { get; set; }

        public required string Email { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// PBKDF2 password hash for self-registered / local accounts.
        /// Null for seeded demo users that still use the shared demo password.
        /// </summary>
        public string? PasswordHash { get; set; }

        //IsActive property is used to indicate whether the user is currently active or not. It can be useful for managing user accounts, such as deactivating users who are no longer part of the organization or temporarily suspending access.
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public UserRole Role{ get; set; }

        //Many Users --> One Location, so we use a navigation property to represent this relationship

        // A user may be associated with a FlexiSpace location.
        // This is nullable because Clients do not belong to a specific
        // FlexiSpace location.
        public int? LocationId { get; set; }

        public  Location? Location { get; set; }

        //One User can have many bookings, so we use a collection to represent this relationship
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();


        // Bookings this user has modified (Centre Manager action)
        public ICollection<Booking> ModifiedBookings { get; set; } = new List<Booking>();

        // Bookings this user has cancelled (Centre Manager action)
        public ICollection<Booking> CancelledBookings { get; set; } = new List<Booking>();
        public ICollection<Notification> Notifications { get; set; }
    = new List<Notification>();

        public ICollection<AuditLog> AuditLogs { get; set; }
    = new List<AuditLog>();
    }
}
