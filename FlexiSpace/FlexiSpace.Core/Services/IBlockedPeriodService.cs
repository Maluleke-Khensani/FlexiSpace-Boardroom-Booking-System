using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface IBlockedPeriodService
    {
        // Lists blocks, soonest first. All filters are optional:
        // boardroomId limits to one boardroom; from/to keep only blocks
        // that overlap that window. Open to any authenticated user -
        // like availability, it's a discovery action.
        Task<IEnumerable<BlockedPeriod>> GetBlockedPeriodsAsync(
            int? boardroomId,
            DateTime? from,
            DateTime? to);

        Task<BlockedPeriod?> GetBlockedPeriodByIdAsync(int id);

        // Creates a block. The creator is always the authenticated caller.
        // Throws ForbiddenException if the caller isn't an Administrator or
        // a Centre Manager at the boardroom's location, NotFoundException
        // if the boardroom doesn't exist, and BusinessRuleException if the
        // period is invalid (end not after start, already over, no reason)
        // or if confirmed bookings already occupy part of it - those must
        // be cancelled first.
        Task<BlockedPeriod> CreateBlockedPeriodAsync(BlockedPeriod blockedPeriod);

        // Removes a block. Returns false if it doesn't exist. Throws
        // ForbiddenException under the same rule as creating one.
        Task<bool> DeleteBlockedPeriodAsync(int id);
    }
}
