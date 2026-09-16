using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


namespace FlexiSpace.API.Controllers
{
<<<<<<< Updated upstream
=======
    // This is the admin "manage users" screen's backing controller - view,
    // edit, activate/deactivate, and change a user's role. Every action
    // requires Administrator (see AuthorizeRolesAttribute for why this is
    // a custom RBAC check rather than [Authorize(Roles = "...")]), and
    // every mutation writes an AuditLog row so "who changed what, when" is
    // reconstructable later from the admin dashboard.
    //
    // NOTE: [Authorize] at the class level only proves "someone is logged
    // in" - it does NOT restrict this to admins. Every action below that
    // can create/see/change a user's Role or Location must carry its own
    // [AuthorizeRoles(UserRole.Administrator)], otherwise any authenticated
    // Staff/Client account could self-assign a Role or Location, which is
    // exactly what we're trying to prevent by not letting users set their
    // own Location on first login.
>>>>>>> Stashed changes
    [ApiController]
    [Route("api/[controller]")] 
    [Authorize] // Every endpoint in this controller requires an authenticated user.
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
<<<<<<< Updated upstream

        public UserController(IUserService userService)
        {
            _userService = userService;
=======
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGraphDirectoryService _graphDirectoryService;

        public UserController(
            IUserService userService,
            IAuditService auditService,
            ICurrentUserService currentUserService,
            IGraphDirectoryService graphDirectoryService)
        {
            _userService = userService;
            _auditService = auditService;
            _currentUserService = currentUserService;
            _graphDirectoryService = graphDirectoryService;
>>>>>>> Stashed changes
        }

        // Admin-only: backs the "create user" popup. Returns every Entra tenant
        // account that does NOT already have a FlexiSpace User row, so the Admin
        // can only ever pick someone who genuinely still needs one - closes off
        // the possibility of accidentally creating a duplicate row for someone
        // already registered.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpGet("unprovisioned")]
        public async Task<IActionResult> GetUnprovisionedUsers()
        {
            var tenantUsers = await _graphDirectoryService.GetTenantUsersAsync();

            // Reuses the existing GetAllUsersAsync rather than adding a new
            // IUserService method just for this - fine at your current user
            // count; worth revisiting only if the Users table gets large enough
            // that loading every row here becomes a real cost.
            var existingUsers = await _userService.GetAllUsersAsync();
            var registeredObjectIds = existingUsers
                .Select(u => u.EntraObjectId)
                .ToHashSet();

            var unprovisioned = tenantUsers
                .Where(u => !registeredObjectIds.Contains(u.EntraObjectId));

            return Ok(unprovisioned);
        }

        // Deliberately NOT [AuthorizeRoles(Administrator)] - every signed-in
        // user (Staff, Client, Centre Manager, Administrator) needs to be
        // able to ask "who am I / does my profile exist yet" on login. This
        // is the endpoint the frontend calls right after Entra sign-in.
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            // Get the FlexiSpace User linked to the signed-in Entra account.
            var user = await _currentUserService.GetCurrentUserAsync();

            // If no active FlexiSpace User is linked to this Entra account,
            // the user has authenticated with Entra but has not been
            // registered in the FlexiSpace database yet. The frontend
            // should treat this as "contact your Administrator" rather
            // than offering any kind of self-registration.
            if (user == null)
            {
                return NotFound(new
                {
                    message = "No active FlexiSpace user is linked to this Entra account."
                });
            }

            // Return the existing FlexiSpace User.
            return Ok(MapToResponseDto(user));
        }


        // Admin-only: this is how a FlexiSpace account actually gets
        // created. EntraObjectId/FirstName/LastName/Email are expected to
        // come from the Entra account the Admin already created there -
        // Role and LocationId are the two fields ONLY an Administrator
        // should ever be setting, which is why this whole action is locked
        // down rather than something a user could hit on their own first
        // login.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPost]
        public async Task<IActionResult> CreateUser(UserCreateDto dto)
        {
            // Creates a FlexiSpace User from the Entra account
            // and the location assigned by the Administrator.
            var user = new User
            {
                EntraObjectId = dto.EntraObjectId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Role = dto.Role,
                LocationId = dto.LocationId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createdUser = await _userService.CreateUserAsync(user);

            // The Entra account is already registered in FlexiSpace.
            if (createdUser == null)
            {
                return Conflict(new
                {
                    message = "This Entra account is already registered in FlexiSpace."
                });
            }

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = createdUser.Id },
                MapToResponseDto(createdUser));
        }



        // Admin-only: this lists every user's full record, so it can't be
        // left open to any authenticated caller.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();

            var response = users.Select(MapToResponseDto);

            return Ok(response);
        }

        // Admin-only: same reasoning as GetAllUsers - a regular user has no
        // legitimate reason to look up someone else's record by ID.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(user));
        }

<<<<<<< Updated upstream
        // Updates an existing user's information.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateDto dto)
        {
=======
        // Admin-only: this is how an admin changes someone's Role or
        // Location after the fact - the exact two fields a user must never
        // be able to set on themselves.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateDto dto)
        {
            var before = await _userService.GetUserByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            // Email and EntraObjectId are left as placeholders here on
            // purpose - UserService.UpdateUserAsync only ever copies
            // FirstName/LastName/Role/LocationId across (see that method),
            // so these two never actually get persisted. This just
            // satisfies the entity's `required` properties so the object
            // compiles; it's not a bug, but don't rely on this object for
            // anything beyond the four fields UpdateUserAsync reads.
>>>>>>> Stashed changes
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

<<<<<<< Updated upstream
=======
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

>>>>>>> Stashed changes
            return NoContent();
        }

        // Admin-only: activating/deactivating other people's accounts.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, UserStatusDto dto)
        {
            var updated = await _userService.UpdateUserStatusAsync(id, dto.IsActive);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

<<<<<<< Updated upstream
        // Converts a User entity into a UserResponseDto.
=======
        // Records who (the authenticated admin) did what to which User row.
        // Falls back gracefully if, for some reason, the current user
        // can't be resolved (e.g. local dev with [Authorize] bypassed per
        // Tino's handover) rather than throwing and losing the actual
        // change that already succeeded.
        private async Task LogAdminActionAsync(
            AuditAction action,
            int targetUserId,
            string? oldValues,
            string? newValues)
        {
            var actingUserId = await _currentUserService.GetCurrentUserIdAsync();

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

        // Converts a User entity into a UserResponseDto - keeps the raw
        // EF entity (and its navigation properties) from ever leaking
        // straight out over the API.
>>>>>>> Stashed changes
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