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
    // This is the admin "manage users" screen's backing controller - view,
    // edit, activate/deactivate, and change a user's role. Every action
    // requires Administrator (see AuthorizeRolesAttribute for why this is
    // a custom RBAC check rather than [Authorize(Roles = "...")]), and
    // every mutation writes an AuditLog row so "who changed what, when" is
    // reconstructable later from the admin dashboard.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Every endpoint in this controller requires an authenticated user.
    [AuthorizeRoles(UserRole.Administrator)]
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

        // Retrieves all users in the system.
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();

            var response = users.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a specific user using their unique ID.
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

        // Updates an existing user's information (including their Role -
        // this is how an admin changes someone's role from this screen).
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateDto dto)
        {
            var before = await _userService.GetUserByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PhoneNumber = dto.PhoneNumber,
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

            await LogAdminActionAsync(
                AuditAction.Update,
                id,
                oldValues: JsonSerializer.Serialize(new
                {
                    before.FirstName,
                    before.LastName,
                    before.PhoneNumber,
                    Role = before.Role.ToString(),
                    before.LocationId
                }),
                newValues: JsonSerializer.Serialize(new
                {
                    dto.FirstName,
                    dto.LastName,
                    dto.PhoneNumber,
                    Role = dto.Role.ToString(),
                    dto.LocationId
                }));

            return NoContent();
        }

        // Activates or deactivates a user account.
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, UserStatusDto dto)
        {
            var before = await _userService.GetUserByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var updated = await _userService.UpdateUserStatusAsync(id, dto.IsActive);

            if (!updated)
            {
                return NotFound();
            }

            await LogAdminActionAsync(
                AuditAction.Update,
                id,
                oldValues: JsonSerializer.Serialize(new { before.IsActive }),
                newValues: JsonSerializer.Serialize(new { dto.IsActive }));

            return NoContent();
        }

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

        // Converts a User entity into a UserResponseDto.
        private static UserResponseDto MapToResponseDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                EntraObjectId = user.EntraObjectId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                LocationId = user.LocationId,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
