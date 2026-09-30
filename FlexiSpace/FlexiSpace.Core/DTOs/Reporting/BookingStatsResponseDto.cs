namespace FlexiSpace.Core.DTOs.Reporting
{
    // One row of "how many bookings" broken down by some dimension
    // (status, location, or boardroom) - reused for all three breakdowns
    // below so the front end renders them with the same table/chart
    // component.
    public class BookingCountBreakdownDto
    {
        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    // Backs the admin/manager reporting dashboard. Deliberately a single
    // response covering every breakdown the dashboard needs, rather than
    // one endpoint per chart - it's all the same underlying date-ranged
    // query over Bookings, so one round trip is enough.
    public class BookingStatsResponseDto
    {
        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }

        public int TotalBookings { get; set; }

        public double AverageAttendees { get; set; }

        public List<BookingCountBreakdownDto> ByStatus { get; set; } = new();

        public List<BookingCountBreakdownDto> ByLocation { get; set; } = new();

        public List<BookingCountBreakdownDto> ByBoardroom { get; set; } = new();
    }
}
