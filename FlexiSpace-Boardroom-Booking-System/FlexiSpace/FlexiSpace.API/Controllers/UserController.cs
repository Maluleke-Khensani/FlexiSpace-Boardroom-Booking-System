
using FlexiSpace.Core.Common;
using System.Text.Json;
using FlexiSpace.API.Authorization;
using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FlexiSpace.API.Controllers
{
    // Admin user-management controller.
    //
    // Only authenticated Administrators can access this controller.
    // RBAC is enforced through the AuthorizeRoles attribute below.
    //
    // This controller supports:
    // - Viewing all provisioned FlexiSpace users
    // - Viewing an individual user
    // - Provisioning an existing Microsoft Entra user
    // - Updating a user's profile/role/location
    // - Activating/deactivating a user
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public UserController(
            IUserService userService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _userService = userService;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        // Retrieves all users that have been provisioned
        // into the FlexiSpace database.
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized();
            }

            var users = await _userService.GetAllUsersAsync();

            IEnumerable<User> filtered;

            if (currentUser.Role == UserRole.Administrator)
            {
                filtered = users;
            }
            else if (currentUser.Role == UserRole.CentreManager)
            {
                // Centre managers can view users in their own location only.
                filtered = users.Where(u => u.LocationId.HasValue && u.LocationId == currentUser.LocationId);
            }
            else
            {
                // Other roles cannot list all users.
                return Forbid();
            }

            var response = filtered.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a specific FlexiSpace user using
        // their local database ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Administrators can view any user.
            if (currentUser.Role == UserRole.Administrator)
            {
                return Ok(MapToResponseDto(user));
            }

            // CentreManagers can view users in their own location.
            if (currentUser.Role == UserRole.CentreManager &&
                user.LocationId.HasValue &&
                currentUser.LocationId.HasValue &&
                user.LocationId == currentUser.LocationId)
            {
                return Ok(MapToResponseDto(user));
            }

            // Allow a user to view their own profile.
            if (currentUser.Id == user.Id)
            {
                return Ok(MapToResponseDto(user));
            }

            return Forbid();
        }

        // Provisions an existing Microsoft Entra user
        // into the FlexiSpace application.
        //
        // The administrator supplies:
        // - The Entra Object ID of the selected user
        // - The FlexiSpace role
        // - The optional FlexiSpace location
        //
        // The user's name and email are retrieved from
        // Microsoft Entra ID by the UserService.
        [HttpPost("provision")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> ProvisionUser(
            UserProvisionDto dto)
        {
            // Ask the UserService to provision the selected
            // Microsoft Entra user into the local database.
            var user = await _userService.ProvisionUserAsync(dto);

            // ProvisionUserAsync returns null when provisioning
            // cannot be completed.
            //
            // This can happen if:
            // - The Entra user does not exist
            // - The Entra account is inactive
            // - The user is already provisioned
            // - The supplied LocationId does not exist
            if (user == null)
            {
                return BadRequest(
                    "The user could not be provisioned. " +
                    "Verify that the Microsoft Entra user exists and is active, " +
                    "has a valid FlexiSpace application role, " +
                    "has not already been provisioned, and that the selected " +
                    "location exists.");
            }

            // Record the provisioning action in the audit log.
            //
            // The target user is the newly created FlexiSpace user.
            await LogAdminActionAsync(
                AuditAction.Create,
                user.Id,
                oldValues: null,
                newValues: JsonSerializer.Serialize(new
                {
                    user.EntraObjectId,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    Role = user.Role.ToString(),
                    user.LocationId,
                    user.IsActive
                }));

            // Return the newly provisioned user using the DTO
            // rather than exposing the database entity directly.
            return CreatedAtAction(
                nameof(GetUserById),
                new { id = user.Id },
                MapToResponseDto(user));
        }

        // Updates an existing user's information.
        [HttpPut("{id}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> UpdateUser(
            int id,
            UserUpdateDto dto)
        {
            // Retrieve the existing user first so that the
            // previous values can be recorded in the audit log.
            var before = await _userService.GetUserByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            // Create a User entity containing only the fields
            // that can be updated through this endpoint.
            //
            // Email and EntraObjectId are deliberately not changed
            // because they originate from Microsoft Entra ID.
            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                LocationId = dto.LocationId,

                // Required properties that are not being updated.
                Email = string.Empty,
                EntraObjectId = Guid.Empty,
                Location = null!
            };

            var updated = await _userService.UpdateUserAsync(id, user);

            if (!updated)
            {
                return NotFound();
            }

            // Record the changes made by the Administrator.
            await LogAdminActionAsync(
                AuditAction.Update,
                id,
                oldValues: JsonSerializer.Serialize(new
                {
                    before.FirstName,
                    before.LastName,
                    Role = before.Role.ToString(),
                    before.LocationId
                }),
                newValues: JsonSerializer.Serialize(new
                {
                    dto.FirstName,
                    dto.LastName,
                    dto.LocationId
                }));

            return NoContent();
        }

        // Activates or deactivates a user's FlexiSpace account.
        //
        // This changes the user's status inside FlexiSpace.
        // It does not disable the user's Microsoft Entra account.
        [HttpPatch("{id}/status")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> UpdateUserStatus(
            int id,
            UserStatusDto dto)
        {
            // Retrieve the existing user so that the previous
            // status can be recorded in the audit log.
            var before = await _userService.GetUserByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var updated = await _userService.UpdateUserStatusAsync(
                id,
                dto.IsActive);

            if (!updated)
            {
                return NotFound();
            }

            // Record the status change in the audit log.
            await LogAdminActionAsync(
                AuditAction.Update,
                id,
                oldValues: JsonSerializer.Serialize(new
                {
                    before.IsActive
                }),
                newValues: JsonSerializer.Serialize(new
                {
                    dto.IsActive
                }));

            return NoContent();
        }

        // Records who performed an administrative action.
        //
        // CurrentUserService identifies the authenticated
        // FlexiSpace user from their Microsoft Entra Object ID.
        private async Task LogAdminActionAsync(
            AuditAction action,
            int targetUserId,
            string? oldValues,
            string? newValues)
        {
            var actingUserId =
                await _currentUserService.GetCurrentUserIdAsync();

            // If the current user cannot be identified locally,
            // do not create an audit record with an invalid user ID.
            if (actingUserId == null)
            {
                return;
            }

            await _auditService.LogAsync(
                actingUserId.Value,
                action,
                entityName: nameof(User),
                entityId: targetUserId.ToString(),
                oldValues: oldValues,
                newValues: newValues);
        }

        // Converts a User entity into a UserResponseDto.
        //
        // DTOs are returned to the API consumer instead of
        // exposing the Entity Framework entity directly.
        private static UserResponseDto MapToResponseDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                EntraObjectId = user.EntraObjectId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                LocationId = user.LocationId,
                CreatedAt = user.CreatedAt
            };
        }
    }
}

