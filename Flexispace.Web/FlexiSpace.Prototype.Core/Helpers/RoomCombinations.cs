using Flexispace.Core.Models;

namespace Flexispace.Core.Helpers;

/// <summary>
/// Which rooms can be opened into one (e.g. Thingamajik + Whachamacallit at
/// Eagle Canyon). Works from the real rooms the room service last loaded
/// (see RealRoomService.GetRoomsAsync, which calls <see cref="UseRooms"/>).
/// Rooms are the same for every user, so one shared list is fine.
/// </summary>
public static class RoomCombinations
{
    private static IReadOnlyList<Boardroom> _rooms = [];

    public static void UseRooms(IEnumerable<Boardroom> rooms) => _rooms = rooms.ToList();

    public static Boardroom? FindRoom(string roomId) =>
        _rooms.FirstOrDefault(r => r.Id == roomId);

    public static Boardroom? GetCombinationFor(string roomId) =>
        _rooms.FirstOrDefault(r => r.CombinedRoomIds.Contains(roomId));

    public static string? PartnerRoomId(string roomId) =>
        GetCombinationFor(roomId)?.CombinedRoomIds.FirstOrDefault(id => id != roomId);

    public static IReadOnlyList<string> GetConflictRoomIds(string roomId)
    {
        var room = FindRoom(roomId);
        if (room is null)
            return [roomId];

        if (room.IsCombined)
            return [room.Id, .. room.CombinedRoomIds];

        var combinedIds = _rooms
            .Where(r => r.CombinedRoomIds.Contains(roomId))
            .Select(r => r.Id);

        return [roomId, .. combinedIds];
    }
}
