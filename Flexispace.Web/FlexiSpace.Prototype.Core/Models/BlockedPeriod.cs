namespace Flexispace.Core.Models;

public class BlockedPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomId { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

