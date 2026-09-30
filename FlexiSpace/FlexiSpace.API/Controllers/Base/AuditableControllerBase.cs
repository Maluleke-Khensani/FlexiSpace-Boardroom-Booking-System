using System.Text.Json;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers.Base
{
    // Shared audit-logging helper for controllers that need to record who
    // did what (Boardroom/Booking/Catering/Equipment/Location - the
    // catalogue and booking controllers). UserController predates this and
    // still has its own private LogAdminActionAsync doing the same thing
    // inline; this base class exists so the newer controllers don't each
    // repeat that boilerplate.
    //
    // LogActionAsync mirrors IAuditService.LogAsync but takes oldValues/
    // newValues as plain objects and JSON-serializes them here, so call
    // sites can pass an anonymous object directly instead of serializing
    // it themselves at every call site.
    public abstract class AuditableControllerBase : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        protected AuditableControllerBase(
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        protected async Task LogActionAsync(
            AuditAction action,
            string entityType,
            string entityId,
            object? oldValues = null,
            object? newValues = null)
        {
            // Same rule UserController's LogAdminActionAsync already
            // follows: if the caller can't be identified locally, don't
            // create an audit record with an invalid/missing user id.
            var actingUserId = await _currentUserService.GetCurrentUserIdAsync();

            if (actingUserId == null)
            {
                return;
            }

            await _auditService.LogAsync(
                actingUserId.Value,
                action,
                entityType,
                entityId,
                oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                newValues != null ? JsonSerializer.Serialize(newValues) : null);
        }
    }
}
