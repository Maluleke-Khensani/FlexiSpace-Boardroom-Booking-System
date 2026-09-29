using System.Text.Json;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    public class BlockedPeriodService : IBlockedPeriodService
    {
        // Same fixed UTC+2 South Africa offset BookingService uses, so
        // "is this block already over" is judged in local time no matter
        // what timezone the host is set to.
        private static readonly TimeSpan SouthAfricaUtcOffset = TimeSpan.FromHours(2);

        // Only bookings that actually hold their slot get in the way of a
        // new block (same rule BookingService uses for booking-vs-booking).
        private static readonly BookingStatus[] SlotHoldingStatuses = { BookingStatus.Confirmed };

        private const int MaxReasonLength = 500;
        private const int MaxConflictsListed = 5;

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuditService _auditService;
        private readonly ILogger<BlockedPeriodService> _logger;

        public BlockedPeriodService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IAuditService auditService,
            ILogger<BlockedPeriodService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<IEnumerable<BlockedPeriod>> GetBlockedPeriodsAsync(
            int? boardroomId,
            DateTime? from,
            DateTime? to)
        {
            var query = _context.BlockedPeriods
                .Include(bp => bp.CreatedBy)
                .AsQueryable();

            if (boardroomId.HasValue)
            {
                query = query.Where(bp => bp.BoardroomId == boardroomId.Value);
            }

            // Keep any block that overlaps the requested window.
            if (from.HasValue)
            {
                query = query.Where(bp => bp.End > from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(bp => bp.Start < to.Value);
            }

            return await query
                .OrderBy(bp => bp.Start)
                .ToListAsync();
        }

        public async Task<BlockedPeriod?> GetBlockedPeriodByIdAsync(int id)
        {
            return await _context.BlockedPeriods
                .Include(bp => bp.CreatedBy)
                .FirstOrDefaultAsync(bp => bp.Id == id);
        }

        public async Task<BlockedPeriod> CreateBlockedPeriodAsync(BlockedPeriod blockedPeriod)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            var boardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == blockedPeriod.BoardroomId);

            if (boardroom == null)
            {
                throw new NotFoundException($"Boardroom {blockedPeriod.BoardroomId} was not found.");
            }

            if (!CanManageBlocks(currentUser, boardroom))
            {
                throw new ForbiddenException(
                    "Only an Administrator, or a Centre Manager at this boardroom's location, can block it.");
            }

            var errors = new List<string>();

            if (blockedPeriod.End <= blockedPeriod.Start)
            {
                errors.Add("End must be after start.");
            }

            var nowSouthAfrica = DateTime.UtcNow + SouthAfricaUtcOffset;

            if (blockedPeriod.End <= nowSouthAfrica)
            {
                errors.Add("A block can't end in the past.");
            }

            var reason = blockedPeriod.Reason?.Trim() ?? string.Empty;

            if (reason.Length == 0)
            {
                errors.Add("A reason is required.");
            }
            else if (reason.Length > MaxReasonLength)
            {
                errors.Add($"Reason can't be longer than {MaxReasonLength} characters.");
            }

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            // Refuse to block time that's already been promised to someone.
            // The caller has to cancel those bookings first, so nobody
            // loses a room without being told.
            var conflicts = await FindConflictingBookingsAsync(
                blockedPeriod.BoardroomId,
                blockedPeriod.Start,
                blockedPeriod.End);

            if (conflicts.Count > 0)
            {
                var listed = conflicts
                    .Take(MaxConflictsListed)
                    .Select(b => $"#{b.Id} ({b.BookingDate:yyyy-MM-dd} {FormatTime(b.StartTime)}-{FormatTime(b.EndTime)})");

                var more = conflicts.Count > MaxConflictsListed
                    ? $" and {conflicts.Count - MaxConflictsListed} more"
                    : string.Empty;

                throw new BusinessRuleException(
                    $"{boardroom.Name} already has confirmed bookings in that period: " +
                    $"{string.Join(", ", listed)}{more}. Cancel them first, then block the room.");
            }

            blockedPeriod.Reason = reason;
            blockedPeriod.CreatedById = currentUser.Id;
            blockedPeriod.CreatedAt = DateTime.UtcNow;

            _context.BlockedPeriods.Add(blockedPeriod);

            await _context.SaveChangesAsync();

            await TryAuditAsync(
                currentUser.Id,
                AuditAction.Create,
                blockedPeriod.Id,
                oldValues: null,
                newValues: Describe(blockedPeriod));

            // Reload with CreatedBy so the response can show who set it.
            return await _context.BlockedPeriods
                .Include(bp => bp.CreatedBy)
                .FirstAsync(bp => bp.Id == blockedPeriod.Id);
        }

        public async Task<bool> DeleteBlockedPeriodAsync(int id)
        {
            var blockedPeriod = await _context.BlockedPeriods
                .Include(bp => bp.Boardroom)
                .FirstOrDefaultAsync(bp => bp.Id == id);

            if (blockedPeriod == null)
            {
                return false;
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            if (blockedPeriod.Boardroom == null || !CanManageBlocks(currentUser, blockedPeriod.Boardroom))
            {
                throw new ForbiddenException(
                    "Only an Administrator, or a Centre Manager at this boardroom's location, can remove this block.");
            }

            var oldValues = Describe(blockedPeriod);

            _context.BlockedPeriods.Remove(blockedPeriod);

            await _context.SaveChangesAsync();

            await TryAuditAsync(
                currentUser.Id,
                AuditAction.Delete,
                id,
                oldValues: oldValues,
                newValues: null);

            return true;
        }

        // Administrators can block any boardroom; Centre Managers only at
        // their own location. Everyone else can't block anything.
        private static bool CanManageBlocks(User currentUser, Boardroom boardroom)
        {
            if (currentUser.Role == UserRole.Administrator)
            {
                return true;
            }

            return currentUser.Role == UserRole.CentreManager
                && currentUser.LocationId == boardroom.LocationId;
        }

        // Confirmed bookings that overlap [start, end) on this boardroom,
        // or on a boardroom it's physically linked to (a combined room and
        // its components share the same physical space - same rule
        // BookingService applies when two bookings are compared).
        private async Task<List<Booking>> FindConflictingBookingsAsync(
            int boardroomId,
            DateTime start,
            DateTime end)
        {
            var boardroomIds = await GetLinkedBoardroomIdsAsync(boardroomId);

            var startDate = DateOnly.FromDateTime(start);
            var endDate = DateOnly.FromDateTime(end);

            // Narrow by date in SQL, then compare the exact times in
            // memory - DateOnly + TimeOnly can't be combined inside the
            // query itself.
            var candidates = await _context.Bookings
                .Where(b => boardroomIds.Contains(b.BoardroomId)
                    && SlotHoldingStatuses.Contains(b.Status)
                    && b.BookingDate >= startDate
                    && b.BookingDate <= endDate)
                .ToListAsync();

            return candidates
                .Where(b =>
                    b.BookingDate.ToDateTime(b.StartTime) < end
                    && start < b.BookingDate.ToDateTime(b.EndTime))
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .ToList();
        }

        // Same linkage BookingService.GetConflictingBoardroomIdsAsync
        // uses: booking the combined room blocks its components, and
        // booking a component blocks the combined room - but not a
        // sibling component.
        private async Task<List<int>> GetLinkedBoardroomIdsAsync(int boardroomId)
        {
            var ids = new HashSet<int> { boardroomId };

            var componentIds = await _context.BoardroomComponents
                .Where(bc => bc.CombinedBoardroomId == boardroomId)
                .Select(bc => bc.ComponentBoardroomId)
                .ToListAsync();

            foreach (var componentId in componentIds)
            {
                ids.Add(componentId);
            }

            var combinedIds = await _context.BoardroomComponents
                .Where(bc => bc.ComponentBoardroomId == boardroomId)
                .Select(bc => bc.CombinedBoardroomId)
                .ToListAsync();

            foreach (var combinedId in combinedIds)
            {
                ids.Add(combinedId);
            }

            return ids.ToList();
        }

        private static string FormatTime(TimeOnly time) => time.ToString("HH':'mm");

        private static string Describe(BlockedPeriod blockedPeriod)
        {
            return JsonSerializer.Serialize(new
            {
                blockedPeriod.BoardroomId,
                blockedPeriod.Start,
                blockedPeriod.End,
                blockedPeriod.Reason
            });
        }

        // The block has already been saved by the time this runs, so a
        // failure to write the audit row is logged, not thrown - it
        // shouldn't turn a successful request into a failed one.
        private async Task TryAuditAsync(
            int userId,
            AuditAction action,
            int blockedPeriodId,
            string? oldValues,
            string? newValues)
        {
            try
            {
                await _auditService.LogAsync(
                    userId,
                    action,
                    nameof(BlockedPeriod),
                    blockedPeriodId.ToString(),
                    oldValues,
                    newValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not write audit log for {Action} on BlockedPeriod {BlockedPeriodId}.",
                    action,
                    blockedPeriodId);
            }
        }
    }
}
