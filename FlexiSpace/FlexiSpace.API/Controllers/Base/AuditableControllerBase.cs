using System.Text.Json;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers.Base
{
    // Shared audit-logging + role-check helper for controllers whose
    // actions must be recorded in the AuditLogs table (see
    // DATABASE_BACKEND_HANDOVER.md) and/or need to compare the caller's
    // role against a manager/owner rule.
    //
    // Before this, only UserController wrote audit entries - every other
    // mutating endpoint (boardrooms, equipment, catering, locations,
    // bookings) made changes with no trace of who did it or when. Pulling
    // that logic up here means every controller gets it the same way
    // instead of copy-pasting UserController's LogAdminActionAsync five
    // more times with five chances for the copies to drift apart.
    public abstract class AuditableControllerBase : ControllerBase
    {
        protected readonly IAuditService AuditService;
        protected readonly ICurrentUserService CurrentUserService;

        protected AuditableControllerBase(
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            AuditService = auditService;
            CurrentUserService = currentUserService;
        }

        // Records who performed a mutating action, on what entity, and
        // (optionally) what changed before/after. Silently does nothing if
        // the caller can't be resolved to a local User row - the same rule
        // UserController already followed - so we never write an audit
        // row with a bogus acting-user id.
        //
        // oldValues/newValues are plain objects (usually anonymous types)
        // rather than pre-serialized strings, so call sites read like
        // "log this data" instead of "remember to JsonSerializer.Serialize
        // this yourself every time".
        protected async Task LogActionAsync(
            AuditAction action,
            string entityName,
            string entityId,
            object? oldValues = null,
            object? newValues = null)
        {
            var actingUserId = await CurrentUserService.GetCurrentUserIdAsync();

            if (actingUserId == null)
            {
                return;
            }

            await AuditService.LogAsync(
                actingUserId.Value,
                action,
                entityName,
                entityId,
                oldValues == null ? null : JsonSerializer.Serialize(oldValues),
                newValues == null ? null : JsonSerializer.Serialize(newValues));
        }

        // The two roles that act on behalf of the system/other people
        // rather than just their own bookings or profile. Centralised here
        // because "who counts as a manager" is a decision that must stay
        // identical everywhere it's checked (BookingController ownership
        // checks, future screens, etc.) - defining it in two places is how
        // it quietly drifts.
        protected static bool IsManagerRole(UserRole role) =>
            role == UserRole.Administrator || role == UserRole.CentreManager;
    }
}
