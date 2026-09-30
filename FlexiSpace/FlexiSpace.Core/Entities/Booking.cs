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

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    public string? Company { get; set; }

    public int NumberOfAttendees { get; set; }

    public string? Notes { get; set; }

    public string? OutlookEventId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }


    public int? ModifiedById { get; set; }
    public User? ModifiedBy { get; set; }

    public int? CancelledById { get; set; }
    public User? CancelledBy { get; set; }

    // Set by BookingReminderHostedService the moment the "1 hour before"
    // reminder email/notification goes out for this booking, so the
    // background job never sends it twice no matter how often it polls.
    // Null means no reminder has been sent yet (including for bookings
    // that are cancelled before their reminder window - the job skips
    // those, see BookingReminderHostedService).
    public DateTime? ReminderSentAt { get; set; }

    // One Booking -> Many BookingEquipment
    public ICollection<BookingEquipment> BookingEquipments { get; set; }
        = new List<BookingEquipment>();

    // One Booking -> Many BookingCatering
    public ICollection<BookingCatering> BookingCaterings { get; set; }
        = new List<BookingCatering>();
}
