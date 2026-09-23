using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Core.Helpers;

/// <summary>
/// Eagle Canyon partition: Thingamajik and Whachamacallit can be opened into one room.
/// </summary>
public static class RoomCombinations
{
    public static Boardroom? GetCombinationFor(string roomId) =>
        SeedData.Rooms.FirstOrDefault(r => r.CombinedRoomIds.Contains(roomId));

    public static string? PartnerRoomId(string roomId) =>
        GetCombinationFor(roomId)?.CombinedRoomIds.FirstOrDefault(id => id != roomId);

    public static IReadOnlyList<string> GetConflictRoomIds(string roomId)
    {
        var room = SeedData.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null)
            return [roomId];

        if (room.IsCombined)
            return [room.Id, .. room.CombinedRoomIds];

        var combinedIds = SeedData.Rooms
            .Where(r => r.CombinedRoomIds.Contains(roomId))
            .Select(r => r.Id);

        return [roomId, .. combinedIds];
    }
}
