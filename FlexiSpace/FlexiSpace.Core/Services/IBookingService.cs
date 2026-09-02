using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Services
{
    public interface IBookingService
    {
        // Retrieve all bookings
        Task<IEnumerable<Booking>> GetAllBookingsAsync();

        // Retrieve a specific booking
        Task<Booking?> GetBookingByIdAsync(int id);

        // Create a new booking.
        // Throws NotFoundException if BoardroomId or UserId does not exist.
        // Throws BusinessRuleException if validation fails (invalid times,
        // booking in the past, attendees over capacity, boardroom not
        // bookable, unknown/inactive equipment or catering items, or an
        // overlapping booking already exists for the same boardroom).
        Task<Booking> CreateBookingAsync(Booking booking);

        // Update an existing booking.
        // Returns false if the booking does not exist.
        // Throws NotFoundException if BoardroomId does not exist.
        // Throws BusinessRuleException if validation fails (including an
        // overlap with another booking), or if the booking is already
        // Cancelled/Completed and can no longer be edited.
        Task<bool> UpdateBookingAsync(
            int id,
            Booking booking,
            List<BookingEquipment> equipment,
            List<BookingCatering> catering);

        // Cancels a booking (soft delete - the row is kept and Status is set
        // to Cancelled so booking history is preserved).
        // Returns false if the booking does not exist.
        // Throws BusinessRuleException if the booking is already Completed.
        Task<bool> DeleteBookingAsync(int id);

        // Updates the booking status (Approve, Cancel, mark Completed, etc.).
        // Returns false if the booking does not exist.
        // Throws BusinessRuleException if the requested status transition is
        // not a valid next step from the booking's current status, or if
        // approving without an ApprovedById.
        // Throws NotFoundException if ApprovedById does not exist.
        Task<bool> UpdateBookingStatusAsync(
            int id,
            BookingStatus status,
            int? approvedById);

        // Filters, sorts (by date/time) and paginates bookings.
        // Page/PageSize are clamped to sane bounds rather than throwing.
        Task<PagedResult<Booking>> SearchBookingsAsync(BookingQueryParameters query);
    }
}
