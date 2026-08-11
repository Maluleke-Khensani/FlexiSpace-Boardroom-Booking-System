namespace Flexispace.Core.Models;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public Guid BookerId { get; set; }
    public string BookerName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public int Attendees { get; set; }
    public List<string> Equipment { get; set; } = [];
    public List<string> Catering { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? OutlookEventId { get; set; }
}

