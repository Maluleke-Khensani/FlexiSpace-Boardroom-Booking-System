using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Booking
{
    public class BookingStatusDto
    {
        public BookingStatus Status { get; set; }

        public int? ApprovedById { get; set; }
    }
}