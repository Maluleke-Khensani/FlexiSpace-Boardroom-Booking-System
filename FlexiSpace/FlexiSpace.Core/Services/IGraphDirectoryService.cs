namespace FlexiSpace.Core.Services
{
    // Separate from ICalendarService on purpose - that one talks to a
    // specific mailbox's calendar, this one talks to the tenant's user
    // directory. Different Graph permission (User.Read.All vs
    // Calendars.ReadWrite), different concern, so it gets its own
    // interface rather than growing ICalendarService into two jobs.
    public interface IGraphDirectoryService
    {
        // Returns every user account that exists in the FlexiSpace Entra
        // tenant, straight from Microsoft Graph - not from our own
        // database. The controller is responsible for cross-referencing
        // this against the Users table to work out who still needs a
        // FlexiSpace account created for them.
        Task<IEnumerable<GraphUserDto>> GetTenantUsersAsync();
    }

    // Minimal shape - only what CreateUser actually needs to build a User
    // row. Deliberately not the full Graph user object.
    public class GraphUserDto
    {
        public Guid EntraObjectId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
    }
}