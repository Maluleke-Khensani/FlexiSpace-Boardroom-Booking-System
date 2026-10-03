using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        // Entra ID puts the user's object id in this claim type on the
        // validated token (Microsoft.Identity.Web maps it here). "oid" is
        // kept as a fallback for tokens/tests that use the short claim name.
        private const string ObjectIdClaimType =
            "http://schemas.microsoft.com/identity/claims/objectidentifier";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;

        // Cached per-instance: CurrentUserService is registered Scoped (see
        // Program.cs), so this cache lives for one request and saves a
        // repeat DB round-trip if something calls GetCurrentUserAsync more
        // than once while handling the same request (e.g. the RBAC filter
        // and the controller both asking "who is this?").
        private User? _cachedUser;
        private bool _lookedUp;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (_lookedUp)
            {
                return _cachedUser;
            }

            _lookedUp = true;

            var claimsPrincipal = _httpContextAccessor.HttpContext?.User;

            if (claimsPrincipal == null)
            {
                return null;
            }

            // Check standard Entra ID object ID claim variations
            var oidClaim = claimsPrincipal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")
                        ?? claimsPrincipal.FindFirst("oid")
                        ?? claimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (oidClaim == null || !Guid.TryParse(oidClaim.Value, out var entraObjectId))
            {
                return null;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId);

            _cachedUser = user is { IsActive: true } ? user : null;

            return _cachedUser;
        }

        public async Task<int?> GetCurrentUserIdAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Id;
        }


    }
}
