using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User
{
    public class EntraUserResponseDto
    {
        public Guid EntraObjectId { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        // The user's FlexiSpace role obtained from
        // Microsoft Entra ID.
        //
        // Nullable because an Entra user may exist but
        // may not have a FlexiSpace app role assigned yet.
        public UserRole? Role { get; set; }
    }
}