using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User
{
    public class UserCreateDto
    {
        // The unique Object ID of the user's Microsoft Entra account.
        public Guid EntraObjectId { get; set; }

        // The user's basic information comes from Microsoft Entra.
        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string Email { get; set; }

        // The application role assigned to the user.
        public UserRole Role { get; set; }

        // The FlexiSpace location assigned by the Administrator.
        public int LocationId { get; set; }
    }
}