namespace FlexiSpace.Core.DTOs.User
{
    public class UserProvisionDto
    {
        // The Entra ID of the user being provisioned.
        // The role is NOT supplied here because it comes from
        // Microsoft Entra ID.
        public Guid EntraObjectId { get; set; }

        // The FlexiSpace location assigned by the Administrator.
        // This is separate from the user's Entra role.
        public int? LocationId { get; set; }
    }
}