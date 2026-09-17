namespace Flexispace.Core.Models;

public class BookingRequest
{
    public string RoomId { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Company { get; set; } = string.Empty;
    public int Attendees { get; set; } = 1;
    public List<string> Equipment { get; set; } = [];
    public List<string> Catering { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
}

public class BookingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Booking? Booking { get; set; }
    public List<DateTime> SuggestedSlots { get; set; } = [];
}

