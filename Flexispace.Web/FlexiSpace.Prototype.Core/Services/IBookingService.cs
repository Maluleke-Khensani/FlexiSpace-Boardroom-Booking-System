using Flexispace.Core.Models;

namespace Flexispace.Core.Services;

public interface IBookingService
{
    Task<IReadOnlyList<Booking>> GetBookingsAsync(string? locationId = null, Guid? userId = null, DateTime? day = null);
    Task<IReadOnlyList<Booking>> GetBookingsForCurrentUserScopeAsync(DateTime? day = null);
    Task<Booking?> GetBookingAsync(Guid bookingId);
    Task<BookingResult> CreateBookingAsync(BookingRequest request);
    Task<bool> CancelBookingAsync(Guid bookingId);
    Task<bool> ApproveBookingAsync(Guid bookingId);
    Task<bool> DeclineBookingAsync(Guid bookingId);
    Task<bool> UpdateBookingAsync(Guid bookingId, DateTime start, DateTime end, int attendees, string notes);
    Task<bool> BlockRoomAsync(string roomId, DateTime start, DateTime end, string reason);
    Task<IReadOnlyList<Booking>> GetTodaysBookingsAsync();

    // Equipment and catering the booking form can offer. These come from the
    // real catalogue (GET api/equipment / api/catering), not a canned list,
    // so the form never offers something the API can't book.
    Task<IReadOnlyList<string>> GetEquipmentOptionsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
    Task<IReadOnlyList<string>> GetCateringOptionsAsync() => Task.FromResult<IReadOnlyList<string>>([]);
}

