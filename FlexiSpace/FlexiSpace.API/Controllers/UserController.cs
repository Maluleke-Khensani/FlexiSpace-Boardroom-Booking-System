using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] 
    [Authorize] // Every endpoint in this controller requires an authenticated user.
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
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

        // Updates an existing user's information.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateDto dto)
        {
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

            return NoContent();
        }

        // Activates or deactivates a user account.
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