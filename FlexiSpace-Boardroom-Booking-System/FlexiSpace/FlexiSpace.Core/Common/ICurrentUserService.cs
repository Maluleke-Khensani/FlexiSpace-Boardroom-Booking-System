using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Common
{
    // Resolves the FlexiSpace User row that corresponds to the caller's
    // bearer token. Roles live in our own database (User.Role), not as
    // Entra ID app role claims, so RBAC and audit logging both need a way
    // to turn "the token on this request" into "the User row for that
    // person". This is that single lookup, shared by both.
    public interface ICurrentUserService
    {
        // Returns null if there is no authenticated caller, the token's
        // object id doesn't match any User, or that User has been
        // deactivated (IsActive = false).
        Task<User?> GetCurrentUserAsync();

        Task<int?> GetCurrentUserIdAsync();

        /// <summary>
        /// Links the caller's Entra object id to a FlexiSpace directory row using an email/UPN hint
        /// (e.g. MSAL Account.Username). Access tokens often omit email claims.
        /// </summary>
        Task<User?> LinkByEmailAsync(string? emailHint);
    }
}
