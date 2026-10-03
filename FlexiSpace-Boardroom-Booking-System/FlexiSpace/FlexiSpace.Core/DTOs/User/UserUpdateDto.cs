using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.User
{
    public class UserUpdateDto
    {
        public required string FirstName { get; set; }

        public required string LastName { get; set; }



        public int? LocationId { get; set; }
    }
}