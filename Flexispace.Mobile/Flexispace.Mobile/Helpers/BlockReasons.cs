using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Helpers;

public static class BlockReasons
{
    public static RoomStatus ToStatus(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return RoomStatus.Blocked;

        if (reason.Contains("clean", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Cleaning;

        if (reason.Contains("maint", StringComparison.OrdinalIgnoreCase))
            return RoomStatus.Maintenance;

        return RoomStatus.Blocked;
    }

    public static bool CoversDay(BlockedPeriod block, DateTime day) =>
        block.Start.Date <= day.Date && block.End > day.Date;

    public static RoomStatus? StatusForRooms(
        IEnumerable<BlockedPeriod> blocks,
        IEnumerable<string> roomIds,
        DateTime day)
    {
        var ids = roomIds as IReadOnlyCollection<string> ?? roomIds.ToList();
        var covering = blocks
            .Where(b => ids.Contains(b.RoomId) && CoversDay(b, day))
            .ToList();
        if (covering.Count == 0)
            return null;

        var now = DateTime.Now;
        var chosen = covering
            .Where(b => day.Date == now.Date && b.Start <= now && b.End > now)
            .OrderByDescending(b => b.Start)
            .FirstOrDefault()
            ?? covering.OrderByDescending(b => b.Start).First();

        return ToStatus(chosen.Reason);
    }
}
