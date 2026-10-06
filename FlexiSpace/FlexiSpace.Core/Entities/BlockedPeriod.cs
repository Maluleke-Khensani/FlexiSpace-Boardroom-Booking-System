namespace FlexiSpace.Core.Entities;

/// <summary>
/// A Centre Manager / Admin block that prevents bookings in a boardroom
/// for a date/time window. Column names match the existing FlexiSpaceDB table
/// (Start, End, CreatedById) created by 20260929091423_AddBlockedPeriods.
/// </summary>
public class BlockedPeriod
{
    public int Id { get; set; }

    public int BoardroomId { get; set; }
    public Boardroom? Boardroom { get; set; }

    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int CreatedById { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
