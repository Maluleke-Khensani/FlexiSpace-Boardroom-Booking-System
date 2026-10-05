namespace FlexiSpace.Core.DTOs.BlockedPeriod;

public class CreateBlockedPeriodDto
{
    public int BoardroomId { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class BlockedPeriodResponseDto
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public string BoardroomName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}
