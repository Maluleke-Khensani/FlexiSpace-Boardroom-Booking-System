namespace FlexiSpace.Core.DTOs.BlockedPeriod
{
    public class BlockedPeriodCreateDto
    {
        public int BoardroomId { get; set; }

        // South African local time, ISO 8601 without an offset,
        // e.g. "2026-10-05T09:00:00".
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public required string Reason { get; set; }
    }
}
