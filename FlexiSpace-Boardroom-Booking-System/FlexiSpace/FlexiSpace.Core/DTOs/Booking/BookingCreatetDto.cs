using FlexiSpace.Core.DTOs.Equipment;

namespace FlexiSpace.Core.DTOs.Booking
{
    public class BookingCreateDto
    {
        public int BoardroomId { get; set; }

        public int UserId { get; set; }

        public DateOnly BookingDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public string? Company { get; set; }

        public int NumberOfAttendees { get; set; }

        public string? Notes { get; set; }

        // Equipment and Catering may be omitted by the client (null).
        public List<BookingEquipmentDto>? Equipment { get; set; }

        public List<BookingCateringDto>? Catering { get; set; }
    }
}