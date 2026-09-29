namespace FlexiSpace.Core.Entities
{
    // A period during which a boardroom cannot be booked (maintenance,
    // a private event, a deep clean, etc.). Set by a Centre Manager for
    // their own location, or by an Administrator for any location.
    //
    // Start and End are South African local time (the same convention
    // Booking.BookingDate/StartTime/EndTime already use), and a block may
    // span more than one day - unlike a booking, it isn't tied to a
    // single BookingDate.
    public class BlockedPeriod
    {
        public int Id { get; set; }

        public int BoardroomId { get; set; }

        public Boardroom? Boardroom { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public required string Reason { get; set; }

        // The FlexiSpace user who created the block.
        public int CreatedById { get; set; }

        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
