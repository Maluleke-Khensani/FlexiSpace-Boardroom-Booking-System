using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.DeviceToken;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Called by the mobile app only - it registers/removes its own FCM
    // token against whoever is currently signed in. There's no admin
    // surface for this table; it's plumbing, not user-facing data.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeviceTokenController : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService;
        private readonly ICurrentUserService _currentUserService;

        public DeviceTokenController(
            IDeviceTokenService deviceTokenService,
            ICurrentUserService currentUserService)
        {
            _deviceTokenService = deviceTokenService;
            _currentUserService = currentUserService;
        }

        // Call on app start and whenever Firebase hands the app a new
        // token. Safe to call repeatedly with the same token.
        [HttpPost("register")]
        public async Task<IActionResult> Register(DeviceTokenRegisterDto dto)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();

            if (userId == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            await _deviceTokenService.RegisterTokenAsync(userId.Value, dto.Token, dto.Platform);

            return NoContent();
        }

        // Call on sign-out, so a device that's been signed out of stops
        // receiving pushes meant for the previous user.
        [HttpDelete("{token}")]
        public async Task<IActionResult> Unregister(string token)
        {
            var removed = await _deviceTokenService.RemoveTokenAsync(token);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
