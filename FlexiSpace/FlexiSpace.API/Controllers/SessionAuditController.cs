using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/sessionaudit")]
    [Authorize]
    public sealed class SessionAuditController : ControllerBase
    {
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUser;

        public SessionAuditController(IAuditService audit, ICurrentUserService currentUser)
        {
            _audit = audit;
            _currentUser = currentUser;
        }

        [HttpPost("login")]
        public Task RecordLogin() => RecordAsync(AuditAction.Login);

        [HttpPost("logout")]
        public Task RecordLogout() => RecordAsync(AuditAction.Logout);

        private async Task RecordAsync(AuditAction action)
        {
            var userId = await _currentUser.GetCurrentUserIdAsync();
            if (userId.HasValue)
                await _audit.LogAsync(userId.Value, action, "Session", userId.Value.ToString());
        }
    }
}
