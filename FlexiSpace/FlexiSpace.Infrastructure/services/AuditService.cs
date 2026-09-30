using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            int userId,
            AuditAction action,
            string entityName,
            string entityId,
            string? oldValues = null,
            string? newValues = null)
        {
            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues
                // Timestamp defaults to DateTime.UtcNow on the entity.
            };

            _context.AuditLogs.Add(log);

            // Deliberately a separate SaveChangesAsync from whatever
            // change is being audited, rather than trying to piggyback on
            // the caller's own SaveChanges. Simpler to reason about, and
            // it means a failed audit write doesn't roll back the actual
            // business operation it was describing (or vice versa).
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId)
        {
            return await _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50)
        {
            if (count <= 0)
            {
                count = 50;
            }

            return await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync();
        }
    }
}
