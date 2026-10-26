using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(int userId, AuditAction action, string entityName, string entityId, string? oldValues = null, string? newValues = null)
        {
            var log = new AuditLog { UserId = userId, Action = action, EntityName = entityName, EntityId = entityId, OldValues = oldValues, NewValues = newValues };
            _context.AuditLogs.Add(log);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit persistence must not make an already-completed business action appear to fail.
                _context.Entry(log).State = EntityState.Detached;
                _logger.LogError(ex, "Failed to record audit entry {Action} for {EntityName} {EntityId}", action, entityName, entityId);
            }
        }

        public async Task<IReadOnlyList<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId) =>
            await _context.AuditLogs.Include(a => a.User)
                .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp).ToListAsync();

        public async Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50)
        {
            if (count <= 0) count = 50;
            return await _context.AuditLogs.Include(a => a.User)
                .OrderByDescending(a => a.Timestamp).Take(count).ToListAsync();
        }
    }
}
