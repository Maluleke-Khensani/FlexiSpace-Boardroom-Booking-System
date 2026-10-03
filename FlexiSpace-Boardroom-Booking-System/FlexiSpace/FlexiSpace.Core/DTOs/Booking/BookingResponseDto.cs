using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Booking
{
    public class BookingResponseDto
    {
        public int Id { get; set; }

        public int BoardroomId { get; set; }

        public int UserId { get; set; }

        public DateOnly BookingDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public BookingStatus Status { get; set; }

        public string? Company { get; set; }

        public int NumberOfAttendees { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<BookingEquipmentDto> Equipment { get; set; } = new();

        public List<BookingCateringDto> Catering { get; set; } = new();
    }
}