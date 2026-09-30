using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Services
{
    public interface IBookingService
    {
        // Retrieve all bookings the current caller is allowed to see:
        // Administrators see everything, Centre Managers see their own
        // location, everyone else sees only their own bookings.
        Task<IEnumerable<Booking>> GetAllBookingsAsync();

        // Retrieve a specific booking. Returns null both when it doesn't
        // exist and when the current caller isn't allowed to see it.
        Task<Booking?> GetBookingByIdAsync(int id);

        // Create a new booking. The booker is always the authenticated
        // caller (never taken from the request), and the booking is
        // Confirmed immediately (no approval step). Every active Centre
        // Manager at the boardroom's location is notified (in-app, plus
        // email where configured) and, where an Outlook calendar account
        // is configured for that location, an Outlook event is created -
        // none of these side effects can fail the booking itself; failures
        // are logged, not thrown.
        // Throws ForbiddenException if the caller isn't a recognized/active user.
        // Throws NotFoundException if BoardroomId does not exist.
        // Throws BusinessRuleException if validation fails (invalid times,
        // booking in the past, attendees over capacity, boardroom not
        // bookable, unknown/inactive/duplicate equipment or catering items,
        // a quantity under 1, or an overlapping booking already exists for
        // the same boardroom).
        Task<Booking> CreateBookingAsync(Booking booking);

        // Update an existing booking. Only the booking's own owner, a
        // Centre Manager at that boardroom's location, or an Administrator
        // may edit it. Who performed the edit is resolved internally from
        // the current caller and recorded on Booking.ModifiedById - not
        // taken as a parameter, so it can't be spoofed. The "booking in
        // the past" check only applies when the date/time is actually
        // changing, so editing a booking that has already started (e.g.
        // fixing a typo in the notes) doesn't get rejected for that
        // reason. If the boardroom moves to a different location, that
        // location's Centre Managers are notified.
        // Returns false if the booking does not exist.
        // Throws ForbiddenException if the caller isn't allowed to edit this booking.
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
        // to Cancelled so booking history is preserved). Only the booking's
        // own owner, a Centre Manager at that location, or an Administrator
        // may cancel it. Who cancelled it is resolved internally from the
        // current caller and recorded on Booking.CancelledById.
        // Returns false if the booking does not exist.
        // Throws ForbiddenException if the caller isn't allowed to cancel this booking.
        // Throws BusinessRuleException if the booking is already Completed.
        Task<bool> DeleteBookingAsync(int id);

        // Updates the booking status (Cancel, mark Completed) - there is no
        // Approve step; bookings are created directly as Confirmed. Only
        // the booking's own owner, a Centre Manager at that location, or an
        // Administrator may change it.
        // Returns false if the booking does not exist.
        // Throws ForbiddenException if the caller isn't allowed to change this booking.
        // Throws BusinessRuleException if the requested status transition is
        // not a valid next step from the booking's current status, or if
        // marking Completed before the booking has actually ended.
        Task<bool> UpdateBookingStatusAsync(
            int id,
            BookingStatus status);

        // Filters, sorts (by date/time) and paginates bookings, scoped to
        // what the current caller is allowed to see (same rule as
        // GetAllBookingsAsync). Page/PageSize are clamped to sane bounds
        // rather than throwing.
        Task<PagedResult<Booking>> SearchBookingsAsync(BookingQueryParameters query);

        // Retrieves boardrooms that are available for the requested
        // booking date and time. The availability check considers the
        // boardroom's active status, its current Status, and existing
        // bookings (including conjoined-room conflicts) that occupy the
        // requested time slot. Open to any authenticated user - it doesn't
        // expose whose booking occupies a slot, so it isn't
        // visibility-scoped the way booking details are.
        Task<IEnumerable<Boardroom>> GetAvailableBoardroomsAsync(
            DateOnly bookingDate,
            TimeOnly startTime,
            TimeOnly endTime);
    }
}
