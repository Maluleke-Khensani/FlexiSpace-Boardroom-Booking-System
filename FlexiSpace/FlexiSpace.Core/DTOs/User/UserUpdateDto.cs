using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User
{
    public class UserUpdateDto
    {
        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string PhoneNumber { get; set; }

        public UserRole Role { get; set; }

        public int LocationId { get; set; }
    }
}