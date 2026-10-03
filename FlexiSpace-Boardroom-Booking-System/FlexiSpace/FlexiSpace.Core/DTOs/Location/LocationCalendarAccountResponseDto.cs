namespace FlexiSpace.Core.DTOs.Location
{
    public class LocationCalendarAccountResponseDto
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }

        public bool IsActive { get; set; }

        public int LocationId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
