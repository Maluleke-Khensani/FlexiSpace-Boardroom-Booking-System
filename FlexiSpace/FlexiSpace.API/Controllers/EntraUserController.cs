using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]

    // Only FlexiSpace Administrators can access Entra user management.
    [AuthorizeRoles(UserRole.Administrator)]
    public class EntraUserController : ControllerBase
    {
        private readonly IEntraUserService _entraUserService;

        public EntraUserController(
            IEntraUserService entraUserService)
        {
            _entraUserService = entraUserService;
        }

        // Returns active Microsoft Entra users.
        // This is used by an Administrator when provisioning
        // a user into the FlexiSpace system.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EntraUserResponseDto>>> GetUsers()
        {
            var users = await _entraUserService.GetUsersAsync();

            return Ok(users);
        }

        // Returns one Microsoft Entra user by their Entra Object ID.
        // Only Administrators can access this endpoint.
        [HttpGet("{entraObjectId:guid}")]
        public async Task<ActionResult<EntraUserResponseDto>> GetUser(
            Guid entraObjectId)
        {
            var user = await _entraUserService
                .GetUserByIdAsync(entraObjectId);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }
    }
}