using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Entities;

public class Booking
{
    public int Id { get; set; }


    // Boardroom Relationship
    public int BoardroomId { get; set; }
    public Boardroom? Boardroom { get; set; }
    

    // Booker Relationship
    public int UserId { get; set; }
    public User? User { get; set; }


    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string? Company { get; set; }

    public int NumberOfAttendees { get; set; }

    public string? Notes { get; set; }

    public string? OutlookEventId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }




    // Optional Approval Relationship
    public int? ApprovedById { get; set; }

    public User? ApprovedBy { get; set; }


    // One Booking -> Many BookingEquipment
    public ICollection<BookingEquipment> BookingEquipments { get; set; }
        = new List<BookingEquipment>();

    // One Booking -> Many BookingCatering
    public ICollection<BookingCatering> BookingCaterings { get; set; }
        = new List<BookingCatering>();
}