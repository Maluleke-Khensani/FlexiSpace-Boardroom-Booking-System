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

        // Create a new booking
        Task<Booking> CreateBookingAsync(Booking booking);

        // Update an existing booking
        Task<bool> UpdateBookingAsync(
            int id,
            Booking booking,
            List<BookingEquipment> equipment,
            List<BookingCatering> catering);

        // Delete a booking
        Task<bool> DeleteBookingAsync(int id);

        // Update the booking status (Approve, Reject, Cancel, etc.)
        Task<bool> UpdateBookingStatusAsync(
            int id,
            BookingStatus status,
            int? approvedById);
    }
}