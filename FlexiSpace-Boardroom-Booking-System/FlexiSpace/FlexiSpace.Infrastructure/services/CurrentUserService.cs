using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlexiSpace.Infrastructure.services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IEntraUserService _entraUserService;

        private User? _cachedUser;
        private bool _lookedUp;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context,
            IEntraUserService entraUserService)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _entraUserService = entraUserService;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (_lookedUp)
                return _cachedUser;

            _lookedUp = true;

            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var oid = TryGetObjectId(principal);
            User? user = null;

            if (oid.HasValue)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EntraObjectId == oid.Value);
            }

            if (user is null)
            {
                var emails = CollectEmails(principal);

                // Access tokens often omit email — resolve UPN/mail from Graph by oid.
                if (emails.Count == 0 && oid.HasValue)
                {
                    try
                    {
                        var entra = await _entraUserService.GetUserByIdAsync(oid.Value);
                        if (entra is not null)
                        {
                            foreach (var key in EmailLookup.Candidates(entra.Email))
                                emails.Add(key);
                            if (!string.IsNullOrWhiteSpace(entra.DisplayName) && emails.Count == 0)
                                emails.Add(EmailLookup.Normalize(entra.Email));
                        }
                    }
                    catch
                    {
                        // Graph may lack User.Read.All — fall through to claim/auto-link.
                    }
                }

                user = await FindByEmailKeysAsync(emails);

                if (user is not null && oid.HasValue && user.EntraObjectId != oid.Value)
                {
                    user.EntraObjectId = oid.Value;
                    await _context.SaveChangesAsync();
                }
            }

            // First Microsoft sign-in: create a Client directory row so the app is usable.
            if (user is null && oid.HasValue)
            {
                user = await AutoProvisionAsync(principal, oid.Value);
            }

            _cachedUser = user is { IsActive: true } ? user : null;
            return _cachedUser;
        }

        public async Task<int?> GetCurrentUserIdAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Id;
        }

        /// <summary>
        /// Links the authenticated Entra oid to a directory row by email (from MSAL Account.Username).
        /// </summary>
        public async Task<User?> LinkByEmailAsync(string? emailHint)
        {
            _lookedUp = false;
            _cachedUser = null;

            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var oid = TryGetObjectId(principal);

            var keys = EmailLookup.Candidates(emailHint).ToList();
            foreach (var claimEmail in CollectEmails(principal))
                keys.Add(claimEmail);

            // Prefer directory match by email/UPN (admin seed row), then attach real oid.
            var byEmail = await FindByEmailKeysAsync(keys);
            if (byEmail is not null && oid.HasValue)
            {
                if (byEmail.EntraObjectId != oid.Value)
                {
                    // Unique index on EntraObjectId: clear any other row that already holds this oid
                    // (e.g. a prior auto-provisioned Client with a synthetic email).
                    var holders = await _context.Users
                        .Where(u => u.EntraObjectId == oid.Value && u.Id != byEmail.Id)
                        .ToListAsync();
                    foreach (var other in holders)
                    {
                        other.EntraObjectId = Guid.NewGuid();
                        if (other.Email.EndsWith("@entra.flexispace.local", StringComparison.OrdinalIgnoreCase))
                            other.IsActive = false;
                    }

                    byEmail.EntraObjectId = oid.Value;
                    await _context.SaveChangesAsync();
                }

                _lookedUp = true;
                _cachedUser = byEmail.IsActive ? byEmail : null;
                return _cachedUser;
            }

            if (byEmail is not null)
            {
                _lookedUp = true;
                _cachedUser = byEmail.IsActive ? byEmail : null;
                return _cachedUser;
            }

            if (oid.HasValue)
            {
                var existing = await _context.Users
                    .FirstOrDefaultAsync(u => u.EntraObjectId == oid.Value && u.IsActive);
                if (existing is not null)
                {
                    _lookedUp = true;
                    _cachedUser = existing;
                    return existing;
                }
            }

            return await GetCurrentUserAsync();
        }

        private async Task<User?> FindByEmailKeysAsync(IReadOnlyCollection<string> keys)
        {
            if (keys.Count == 0)
                return null;

            var candidates = await _context.Users.Where(u => u.IsActive).ToListAsync();
            return candidates.FirstOrDefault(u =>
            {
                var stored = EmailLookup.Normalize(u.Email);
                if (keys.Any(k => string.Equals(stored, k, StringComparison.OrdinalIgnoreCase)))
                    return true;
                return keys.Any(k =>
                    !k.Contains('@', StringComparison.Ordinal) &&
                    stored.StartsWith(k + "#ext#@", StringComparison.OrdinalIgnoreCase));
            });
        }

        private async Task<User> AutoProvisionAsync(ClaimsPrincipal principal, Guid oid)
        {
            var emails = CollectEmails(principal);
            var email = emails.FirstOrDefault(e => e.Contains('@'))
                        ?? $"{oid:N}@entra.flexispace.local";
            var name = principal.FindFirst("name")?.Value
                       ?? principal.FindFirst(ClaimTypes.Name)?.Value
                       ?? "Microsoft User";
            var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var first = parts.ElementAtOrDefault(0) ?? "Microsoft";
            var last = parts.ElementAtOrDefault(1) ?? "User";

            // Prefer linking a pre-seeded directory row that still has a placeholder oid.
            var byEmail = await FindByEmailKeysAsync(emails);
            if (byEmail is not null)
            {
                byEmail.EntraObjectId = oid;
                await _context.SaveChangesAsync();
                return byEmail;
            }

            var normalizedEmail = EmailLookup.Normalize(email);
            var user = new User
            {
                EntraObjectId = oid,
                FirstName = first,
                LastName = last,
                Email = normalizedEmail,
                PhoneNumber = string.Empty,
                Role = InferRoleFromEmail(normalizedEmail),
                LocationId = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Best-effort role when a tenant user signs in before an admin seeds them.
        /// </summary>
        internal static UserRole InferRoleFromEmail(string email)
        {
            var local = email;
            var at = email.IndexOf('@');
            if (at > 0)
                local = email[..at];
            // Guest UPN: local_domain.com#ext#@tenant
            var ext = local.IndexOf("#ext#", StringComparison.OrdinalIgnoreCase);
            if (ext > 0)
                local = local[..ext];

            if (local.Contains("admin", StringComparison.OrdinalIgnoreCase))
                return UserRole.Administrator;
            if (local.StartsWith("manager", StringComparison.OrdinalIgnoreCase) ||
                local.Contains("centre", StringComparison.OrdinalIgnoreCase) ||
                local.Contains("center", StringComparison.OrdinalIgnoreCase))
                return UserRole.CentreManager;
            if (local.StartsWith("staff", StringComparison.OrdinalIgnoreCase))
                return UserRole.Staff;
            return UserRole.Client;
        }

        private static Guid? TryGetObjectId(ClaimsPrincipal principal)
        {
            foreach (var type in new[]
                     {
                         "http://schemas.microsoft.com/identity/claims/objectidentifier",
                         "oid"
                     })
            {
                var value = principal.FindFirst(type)?.Value;
                if (Guid.TryParse(value, out var oid))
                    return oid;
            }

            // NameIdentifier is sometimes the oid — only accept real GUIDs.
            var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(nameId, out var fromNameId))
                return fromNameId;

            return null;
        }

        private static List<string> CollectEmails(ClaimsPrincipal principal)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in new[]
                     {
                         "preferred_username",
                         "upn",
                         "unique_name",
                         "email",
                         "emails",
                         ClaimTypes.Email,
                         ClaimTypes.Upn
                     })
            {
                foreach (var claim in principal.FindAll(type))
                {
                    foreach (var key in EmailLookup.Candidates(claim.Value))
                        set.Add(key);
                }
            }

            return set.ToList();
        }
    }
}
