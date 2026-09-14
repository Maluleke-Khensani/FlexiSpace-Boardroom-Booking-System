using FlexiSpace.Core.Common;
using FlexiSpace.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FlexiSpace.API.Authorization
{
    // RBAC enforcement for controllers/actions.
    //
    // Why this instead of ASP.NET's built-in [Authorize(Roles = "...")]:
    // the built-in version checks role *claims on the JWT*, which would
    // mean roles have to be configured as Entra ID app roles. In this
    // system, Role lives on our own User row (see the data model
    // handover), so authorization has to be a DB lookup keyed off the
    // token's object id - that's exactly what ICurrentUserService does.
    //
    // Usage: stack this alongside the existing [Authorize] on a
    // controller/action. [Authorize] proves "this is a valid Entra ID
    // token" (authentication); this attribute proves "this person's role
    // is allowed to do this" (authorization). Order doesn't matter -
    // ASP.NET Core always runs authentication before authorization
    // filters.
    //
    //   [Authorize]
    //   [AuthorizeRoles(UserRole.Administrator)]
    //   [HttpDelete("{id}")]
    //   public async Task<IActionResult> DeleteBoardroom(int id) { ... }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AuthorizeRolesAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly UserRole[] _allowedRoles;

        public AuthorizeRolesAttribute(params UserRole[] allowedRoles)
        {
            if (allowedRoles == null || allowedRoles.Length == 0)
            {
                throw new ArgumentException(
                    "AuthorizeRoles requires at least one UserRole.",
                    nameof(allowedRoles));
            }

            _allowedRoles = allowedRoles;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // If [Authorize] already rejected the request (no/invalid
            // token), don't do extra work or override its result.
            if (context.Result != null)
            {
                return;
            }

            var currentUserService = context.HttpContext.RequestServices
                .GetRequiredService<ICurrentUserService>();

            var currentUser = await currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                // No matching (or an inactive) User record for this token.
                // 401, not 403: from this endpoint's point of view we
                // don't know who this caller is at all.
                context.Result = new UnauthorizedObjectResult(
                    new { message = "No active account found for this token." });
                return;
            }

            if (!_allowedRoles.Contains(currentUser.Role))
            {
                context.Result = new ObjectResult(
                    new
                    {
                        message = "You do not have permission to perform this action.",
                        requiredRoles = _allowedRoles.Select(r => r.ToString()),
                        yourRole = currentUser.Role.ToString()
                    })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
