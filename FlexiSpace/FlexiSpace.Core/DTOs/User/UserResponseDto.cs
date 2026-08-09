using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User
{
    public class UserResponseDto
    {
        public int Id { get; set; }

        public Guid EntraObjectId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        public bool IsActive { get; set; }

        public int LocationId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}