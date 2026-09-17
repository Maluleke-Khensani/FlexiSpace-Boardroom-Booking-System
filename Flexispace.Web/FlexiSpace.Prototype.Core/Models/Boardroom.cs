namespace Flexispace.Core.Models;

public class Boardroom
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public List<string> Equipment { get; set; } = [];
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string ImageKey { get; set; } = "room_meeting";
}

