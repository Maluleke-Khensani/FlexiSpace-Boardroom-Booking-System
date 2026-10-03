namespace FlexiSpace.Core.DTOs.Location
{
    public class LocationCalendarAccountCreateDto
    {
        // The calendar account email to receive bookings (e.g. centre manager account)
        public string Email { get; set; } = string.Empty;

        // Display name for the calendar account
        public string DisplayName { get; set; } = string.Empty;

        // Whether this calendar is the primary calendar for the location
        public bool IsPrimary { get; set; } = false;

        // Whether the calendar account is active
        public bool IsActive { get; set; } = true;

        // The location to associate with this calendar account
        public int LocationId { get; set; }
    }
}
