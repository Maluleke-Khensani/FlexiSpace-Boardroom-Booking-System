using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;

        public BookingService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Retrieves all bookings together with their equipment and catering.
        public async Task<IEnumerable<Booking>> GetAllBookingsAsync()
        {
            return await _context.Bookings
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .ToListAsync();
        }

        // Retrieves a single booking by its ID.
        public async Task<Booking?> GetBookingByIdAsync(int id)
        {
            return await _context.Bookings
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        // Creates a new booking.
        public async Task<Booking> CreateBookingAsync(Booking booking)
        {
            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync();

            return booking;
        }

        // Updates an existing booking.
        public async Task<bool> UpdateBookingAsync(
            int id,
            Booking booking,
            List<BookingEquipment> equipment,
            List<BookingCatering> catering)
        {
            var existingBooking = await _context.Bookings
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBooking == null)
            {
                return false;
            }

            existingBooking.BoardroomId = booking.BoardroomId;
            existingBooking.BookingDate = booking.BookingDate;
            existingBooking.StartTime = booking.StartTime;
            existingBooking.EndTime = booking.EndTime;
            existingBooking.Company = booking.Company;
            existingBooking.NumberOfAttendees = booking.NumberOfAttendees;
            existingBooking.Notes = booking.Notes;

            // Replace Equipment
            existingBooking.BookingEquipments.Clear();

            foreach (var item in equipment)
            {
                existingBooking.BookingEquipments.Add(item);
            }

            // Replace Catering
            existingBooking.BookingCaterings.Clear();

            foreach (var item in catering)
            {
                existingBooking.BookingCaterings.Add(item);
            }

            await _context.SaveChangesAsync();

            return true;
        }

        // Deletes a booking.
        public async Task<bool> DeleteBookingAsync(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return false;
            }

            _context.Bookings.Remove(booking);

            await _context.SaveChangesAsync();

            return true;
        }

        // Updates the booking status.
        public async Task<bool> UpdateBookingStatusAsync(
            int id,
            BookingStatus status,
            int? approvedById)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return false;
            }

            booking.Status = status;
            booking.ApprovedById = approvedById;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}