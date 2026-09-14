using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Services
{
    // AuditLog is system-managed (see data model handover) - there is
    // deliberately no CRUD controller for it. Other services call LogAsync
    // as a side effect of the action actually being audited; nothing
    // creates an AuditLog row directly.
    public interface IAuditService
    {
        Task LogAsync(
            int userId,
            AuditAction action,
            string entityName,
            string entityId,
            string? oldValues = null,
            string? newValues = null);

        // For the reporting/admin-dashboard work coming in the next
        // sub-phase - kept here now since it's a one-line query and admin
        // endpoints are the natural place it'll first be surfaced from.
        Task<IReadOnlyList<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId);

        Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50);
    }
}
