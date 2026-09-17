using Azure.Identity;
using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace FlexiSpace.Infrastructure.Services
{
    public class EntraUserService : IEntraUserService
    {
        private readonly GraphServiceClient _graphClient;

        // This is the Application (Client) ID of the
        // FlexiSpace Boardroom Booking System.
        //
        // We use it to find our application's Service Principal
        // in Microsoft Entra ID.
        private readonly string _clientId;

        public EntraUserService(
            string tenantId,
            string clientId,
            string clientSecret)
        {
            _clientId = clientId;

            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            // This service uses application permissions.
            // Therefore Graph uses the application's permissions
            // configured in Entra ID.
            _graphClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });
        }

        public async Task<IEnumerable<EntraUserResponseDto>> GetUsersAsync()
        {
            // Retrieve active users from Microsoft Entra ID.
            //
            // We do not use server-side sorting because the
            // Graph users query previously rejected $orderby.
            var response = await _graphClient.Users.GetAsync(config =>
            {
                config.QueryParameters.Select = new[]
                {
                    "id",
                    "displayName",
                    "givenName",
                    "surname",
                    "mail",
                    "userPrincipalName",
                    "accountEnabled"
                };

                // Only retrieve active Entra accounts.
                config.QueryParameters.Filter =
                    "accountEnabled eq true";
            });

            if (response?.Value == null)
            {
                return Enumerable.Empty<EntraUserResponseDto>();
            }

            var users = new List<EntraUserResponseDto>();

            foreach (var user in response.Value)
            {
                var dto = MapToDto(user);

                // Retrieve the FlexiSpace app role assigned to
                // this Entra user.
                dto.Role = await GetFlexiSpaceRoleAsync(
                    dto.EntraObjectId);

                users.Add(dto);
            }

            // Sort locally instead of using Graph $orderby.
            return users
                .OrderBy(u => u.DisplayName)
                .ToList();
        }

        public async Task<EntraUserResponseDto?> GetUserByIdAsync(
            Guid entraObjectId)
        {
            var user = await _graphClient
                .Users[entraObjectId.ToString()]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = new[]
                    {
                        "id",
                        "displayName",
                        "givenName",
                        "surname",
                        "mail",
                        "userPrincipalName",
                        "accountEnabled"
                    };
                });

            if (user == null)
            {
                return null;
            }

            var dto = MapToDto(user);

            // Retrieve the user's FlexiSpace app role.
            dto.Role = await GetFlexiSpaceRoleAsync(
                entraObjectId);

            return dto;
        }

        private async Task<UserRole?> GetFlexiSpaceRoleAsync(
            Guid entraObjectId)
        {
            // First find the Service Principal representing
            // our FlexiSpace application.
            var servicePrincipalResponse =
                await _graphClient.ServicePrincipals.GetAsync(config =>
                {
                    // The app registration's Application (Client) ID
                    // identifies our FlexiSpace application.
                    config.QueryParameters.Filter =
                        $"appId eq '{_clientId}'";

                    // We need the Service Principal ID and its
                    // configured app roles.
                    config.QueryParameters.Select = new[]
                    {
                        "id",
                        "appId",
                        "appRoles"
                    };
                });

            var servicePrincipal =
                servicePrincipalResponse?.Value?.FirstOrDefault();

            if (servicePrincipal?.Id == null)
            {
                return null;
            }

            // Get the roles assigned to this user for our
            // FlexiSpace application.
            //
            // Filtering by resourceId prevents us from accidentally
            // reading roles belonging to another application.
            var assignments =
                await _graphClient
                    .Users[entraObjectId.ToString()]
                    .AppRoleAssignments
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Filter =
                            $"resourceId eq {servicePrincipal.Id}";
                    });

            if (assignments?.Value == null)
            {
                return null;
            }

            // Find assignments that match one of the app roles
            // configured for FlexiSpace.
            var flexiSpaceAssignments =
                assignments.Value
                    .Where(a =>
                        a.AppRoleId.HasValue &&
                        servicePrincipal.AppRoles != null &&
                        servicePrincipal.AppRoles.Any(
                            role =>
                                role.Id == a.AppRoleId &&
                                role.IsEnabled == true))
                    .ToList();

            // A user should have one FlexiSpace application role.
            //
            // If none exists, the user cannot be provisioned yet.
            if (flexiSpaceAssignments.Count == 0)
            {
                return null;
            }

            // If more than one FlexiSpace role is assigned,
            // do not guess which role should be used.
            //
            // The Administrator should correct the assignments
            // in Microsoft Entra ID.
            if (flexiSpaceAssignments.Count > 1)
            {
                return null;
            }

            var assignment = flexiSpaceAssignments[0];

            var appRole = servicePrincipal.AppRoles?
                .FirstOrDefault(role =>
                    role.Id == assignment.AppRoleId);

            if (appRole?.Value == null)
            {
                return null;
            }

            // Convert the Entra app role value into our
            // FlexiSpace UserRole enum.
            return MapEntraRoleToUserRole(appRole.Value);
        }

        private static UserRole? MapEntraRoleToUserRole(
            string roleValue)
        {
            // These values must match the app role values configured
            // for the FlexiSpace application in Microsoft Entra ID.
            return roleValue switch
            {
                "Administrator" => UserRole.Administrator,

                "CentreManager" => UserRole.CentreManager,

                "Staff" => UserRole.Staff,

                "Client" => UserRole.Client,

                _ => null
            };
        }

        private static EntraUserResponseDto MapToDto(
            Microsoft.Graph.Models.User user)
        {
            return new EntraUserResponseDto
            {
                EntraObjectId = Guid.Parse(user.Id!),

                FirstName = user.GivenName
                    ?? string.Empty,

                LastName = user.Surname
                    ?? string.Empty,

                DisplayName = user.DisplayName
                    ?? string.Empty,

                Email = user.Mail
                    ?? user.UserPrincipalName
                    ?? string.Empty,

                IsActive = user.AccountEnabled
                    ?? false
            };
        }
    }
}