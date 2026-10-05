namespace Flexispace.Mobile.Models;

public class Boardroom
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public List<string> Equipment { get; set; } = [];
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string ImageKey { get; set; } = "room_meeting";

    /// <summary>True when this option books Thingamajik and Whachamacallit together.</summary>
    public bool IsCombined { get; set; }

    /// <summary>Partner room id when this room can open into an adjoining space.</summary>
    public string? CombinableWithRoomId { get; set; }

    public bool CanCombine => IsCombined || !string.IsNullOrEmpty(CombinableWithRoomId);

    /// <summary>Short UX line under the room name (combine tip or combined suite blurb).</summary>
    public string? CombineHint { get; set; }

    /// <summary>Seat chip text — falls back to "Seats {Capacity}" when unset.</summary>
    public string? CapacityLabel { get; set; }

    public string DisplayCapacity =>
        string.IsNullOrWhiteSpace(CapacityLabel) ? $"Seats {Capacity}" : CapacityLabel;
}
