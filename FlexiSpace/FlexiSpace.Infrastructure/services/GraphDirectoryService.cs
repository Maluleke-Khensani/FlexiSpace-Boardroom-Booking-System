using Azure.Identity;
using Microsoft.Graph;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.services
{
    public class GraphDirectoryService : IGraphDirectoryService
    {
        private readonly GraphServiceClient _graphClient;

        // Same ClientSecretCredential pattern as MicrosoftGraphCalendarService
        // - one Graph app registration, used for two different jobs
        // (calendar + directory) with two different Application
        // permissions granted to it.
        public GraphDirectoryService(
            string tenantId,
            string clientId,
            string clientSecret)
        {
            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            _graphClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });
        }

        public async Task<IEnumerable<GraphUserDto>> GetTenantUsersAsync()
        {
            // $select keeps the response to only the fields we actually
            // use - no point pulling every property Graph knows about a
            // user just to throw most of it away.
            var result = await _graphClient.Users.GetAsync(config =>
            {
                config.QueryParameters.Select = new[]
                {
                    "id", "givenName", "surname", "mail", "userPrincipalName"
                };
            });

            var users = result?.Value ?? new List<Microsoft.Graph.Models.User>();

            return users
                // Skip anything without a parseable id or names - Graph can
                // return service/system accounts here that were never
                // meant to be a FlexiSpace user.
                .Where(u => u.Id != null && Guid.TryParse(u.Id, out _))
                .Select(u => new GraphUserDto
                {
                    EntraObjectId = Guid.Parse(u.Id!),
                    FirstName = u.GivenName ?? string.Empty,
                    LastName = u.Surname ?? string.Empty,
                    // Mail is the real mailbox address, but it's not always
                    // populated for accounts created without a mailbox
                    // license - fall back to the sign-in name so the Admin
                    // still sees something recognisable in the picker.
                    Email = u.Mail ?? u.UserPrincipalName ?? string.Empty
                });
        }
    }
}