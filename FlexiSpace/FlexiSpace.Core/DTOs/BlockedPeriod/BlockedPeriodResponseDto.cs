namespace FlexiSpace.Core.DTOs.BlockedPeriod
{
    public class BlockedPeriodResponseDto
    {
        public int Id { get; set; }

        public int BoardroomId { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string Reason { get; set; } = string.Empty;

        public int CreatedById { get; set; }

        public string CreatedByName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
