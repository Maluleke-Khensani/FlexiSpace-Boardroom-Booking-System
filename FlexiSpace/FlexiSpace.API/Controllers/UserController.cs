
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
    // Admin user-management controller, plus a self-lookup endpoint any
    // signed-in user can call.
    //
    // Everything except GetMyProfile is Administrator-only. RBAC for those
    // is enforced with [AuthorizeRoles] on each action individually (rather
    // than once at the controller level) specifically so GetMyProfile can
    // stay open to any authenticated user - see its comment below for why
    // it needs to exist at all.
    //
    // This controller supports:
    // - A signed-in user fetching their own FlexiSpace profile (NEW)
    // - Viewing all provisioned FlexiSpace users (Administrator)
    // - Viewing an individual user (Administrator)
    // - Provisioning an existing Microsoft Entra user (Administrator)
    // - Updating a user's profile/role/location (Administrator)
    // - Activating/deactivating a user (Administrator)
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

        // NEW: lets a signed-in user fetch their own FlexiSpace profile
        // (Id, Role, LocationId, etc.) without needing Administrator
        // rights.
        //
        // Why this needs to exist: every RBAC check in this API resolves
        // "who is this?" server-side via ICurrentUserService (the caller's
        // Entra object id -> a row in our own Users table). Nothing about
        // that is visible to the caller's own access token - a client app
        // (Web/Mobile) that just finished signing a user in via Entra has
        // no way to find out its own local Id/Role/LocationId, which it
        // needs immediately to decide what UI to show (can this person
        // book? manage? see the admin console?). Without this endpoint,
        // the only "who am I" the front end could see is the Entra token
        // itself, which deliberately isn't what RBAC is keyed on here.
        //
        // 404 covers two states deliberately collapsed together: "never
        // provisioned" and "provisioned but deactivated" - either way, the
        // client's correct response is the same "ask your Administrator"
        // message, not two different codepaths.
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return NotFound(new
                {
                    message = "No FlexiSpace account is linked to this sign-in yet. " +
                        "Ask an Administrator to provision your account."
                });
            }

            return Ok(MapToResponseDto(currentUser));
        }

        // Login history (project plan, Security: "Audit logs for ... login
        // history"). The web and mobile apps call these right after a
        // Microsoft sign-in completes and just before signing out, so each
        // shows up in the audit log as a Login / Logout entry against the
        // user. Any signed-in user can record their own; nobody can record
        // one for someone else.
        [HttpPost("me/sign-in")]
        public Task<IActionResult> RecordSignIn([FromQuery] string? client) =>
            RecordSessionEventAsync(AuditAction.Login, client);

        [HttpPost("me/sign-out")]
        public Task<IActionResult> RecordSignOut([FromQuery] string? client) =>
            RecordSessionEventAsync(AuditAction.Logout, client);

        private async Task<IActionResult> RecordSessionEventAsync(AuditAction action, string? client)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return NotFound(new
                {
                    message = "No FlexiSpace account is linked to this sign-in yet."
                });
            }

            // A page refresh straight after signing in can report the same
            // sign-in twice - one entry per two minutes is plenty.
            var recent = await _auditService.GetLogsForEntityAsync(nameof(User), currentUser.Id.ToString());
            var cutoff = DateTime.UtcNow.AddMinutes(-2);

            if (recent.Any(l => l.Action == action && l.UserId == currentUser.Id && l.Timestamp > cutoff))
            {
                return NoContent();
            }

            var clientName = string.IsNullOrWhiteSpace(client)
                ? "Unknown"
                : client.Trim()[..Math.Min(client.Trim().Length, 20)];

            await _auditService.LogAsync(
                currentUser.Id,
                action,
                entityName: nameof(User),
                entityId: currentUser.Id.ToString(),
                newValues: JsonSerializer.Serialize(new { Client = clientName, currentUser.Email }));

            return NoContent();
        }

        // Retrieves all users that have been provisioned
        // into the FlexiSpace database.
        [HttpGet]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();

            var response = users.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a specific FlexiSpace user using
        // their local database ID.
        [HttpGet("{id}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(user));
        }

        // Provisions an existing Microsoft Entra user
        // into the FlexiSpace application.
        //
        // The administrator supplies:
        // - The Entra Object ID of the selected user
        // - The optional FlexiSpace location
        //
        // The user's name, email, and FlexiSpace role are retrieved from
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
                Role = dto.Role,
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
                    Role = dto.Role.ToString(),
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
