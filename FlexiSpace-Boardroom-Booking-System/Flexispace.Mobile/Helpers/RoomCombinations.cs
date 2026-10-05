using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Eagle Canyon's Thingamajik and Whachamacallit open into one suite for larger groups.
/// </summary>
public static class RoomCombinations
{
    public const string ThingamajikId = "eag-thi";
    public const string WhachamacallitId = "eag-wha";
    public const string CombinedId = "eag-thi-wha";

    public static bool IsCombinedOption(string? roomId) =>
        string.Equals(roomId, CombinedId, StringComparison.Ordinal);

    public static bool IsPairMember(string? roomId) =>
        roomId is ThingamajikId or WhachamacallitId;

    public static string? PartnerId(string roomId) => roomId switch
    {
        ThingamajikId => WhachamacallitId,
        WhachamacallitId => ThingamajikId,
        _ => null
    };

    public static string? PartnerName(string roomId) => roomId switch
    {
        ThingamajikId => "Whachamacallit",
        WhachamacallitId => "Thingamajik",
        _ => null
    };

    /// <summary>Room IDs that conflict with a booking of <paramref name="roomId"/> (self + pair/combined).</summary>
    public static IReadOnlyList<string> ConflictRoomIds(string roomId)
    {
        if (IsCombinedOption(roomId))
            return [CombinedId, ThingamajikId, WhachamacallitId];
        if (IsPairMember(roomId))
            return [roomId, CombinedId];
        return [roomId];
    }

    public static Boardroom CreateCombinedOption()
    {
        var thi = SeedData.Rooms.First(r => r.Id == ThingamajikId);
        var wha = SeedData.Rooms.First(r => r.Id == WhachamacallitId);
        var seats = thi.Capacity + wha.Capacity;
        return new Boardroom
        {
            Id = CombinedId,
            Name = "Thingamajik + Whachamacallit",
            LocationId = "eagle",
            Capacity = seats,
            Equipment = thi.Equipment.Union(wha.Equipment).Distinct().ToList(),
            ImageKey = thi.ImageKey,
            IsCombined = true,
            CombineHint = "Two adjoining rooms opened as one suite",
            CapacityLabel = $"Seats {seats} combined"
        };
    }

    public static void ApplyPairHints(Boardroom room)
    {
        if (!IsPairMember(room.Id)) return;
        var partner = PartnerName(room.Id)!;
        var maxTogether = room.Capacity + PartnerCapacity(room.Id);
        room.CombinableWithRoomId = PartnerId(room.Id);
        room.CombineHint = $"Opens into {partner} for larger groups (up to {maxTogether} seats)";
        room.CapacityLabel = $"Seats {room.Capacity} · can combine";
    }

    private static int PartnerCapacity(string roomId)
    {
        var partnerId = PartnerId(roomId);
        return SeedData.Rooms.FirstOrDefault(r => r.Id == partnerId)?.Capacity ?? 0;
    }

    /// <summary>
    /// Annotates pair members and inserts the combined suite option first among them.
    /// </summary>
    public static List<Boardroom> WithCombineOptions(IEnumerable<Boardroom> rooms, string? locationId)
    {
        var list = rooms.Select(Clone).ToList();
        foreach (var room in list)
            ApplyPairHints(room);

        var includeCombined = locationId == "eagle" ||
                              (locationId is null && list.Any(r => IsPairMember(r.Id)));
        if (includeCombined && list.Any(r => IsPairMember(r.Id)) && list.All(r => r.Id != CombinedId))
        {
            var insertAt = list.FindIndex(r => IsPairMember(r.Id));
            if (insertAt < 0) insertAt = 0;
            list.Insert(insertAt, CreateCombinedOption());
        }

        return list;
    }

    private static Boardroom Clone(Boardroom r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        LocationId = r.LocationId,
        Capacity = r.Capacity,
        Equipment = [.. r.Equipment],
        Status = r.Status,
        ImageKey = r.ImageKey,
        IsCombined = r.IsCombined,
        CombinableWithRoomId = r.CombinableWithRoomId,
        CombineHint = r.CombineHint,
        CapacityLabel = r.CapacityLabel
    };
}
