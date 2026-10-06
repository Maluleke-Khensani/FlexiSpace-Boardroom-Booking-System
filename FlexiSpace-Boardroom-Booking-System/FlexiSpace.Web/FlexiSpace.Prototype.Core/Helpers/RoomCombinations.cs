using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Core.Helpers;

/// <summary>
/// Eagle Canyon partition: Thingamajik and Whachamacallit can be opened into one room.
/// </summary>
public static class RoomCombinations
{
    private static IReadOnlyList<Boardroom> Catalog { get; set; } = SeedData.Rooms;

    public static void SetCatalog(IReadOnlyList<Boardroom> rooms) =>
        Catalog = rooms.Count > 0 ? rooms : SeedData.Rooms;

    public static Boardroom? GetCombinationFor(string roomId) =>
        Catalog.FirstOrDefault(r => r.CombinedRoomIds.Contains(roomId));

    public static string? PartnerRoomId(string roomId) =>
        GetCombinationFor(roomId)?.CombinedRoomIds.FirstOrDefault(id => id != roomId);

    public static IReadOnlyList<string> GetConflictRoomIds(string roomId)
    {
        var room = Catalog.FirstOrDefault(r => r.Id == roomId);
        if (room is null)
            return [roomId];

        if (room.IsCombined)
            return [room.Id, .. room.CombinedRoomIds];

        var combinedIds = Catalog
            .Where(r => r.CombinedRoomIds.Contains(roomId))
            .Select(r => r.Id);

        return [roomId, .. combinedIds];
    }
}
