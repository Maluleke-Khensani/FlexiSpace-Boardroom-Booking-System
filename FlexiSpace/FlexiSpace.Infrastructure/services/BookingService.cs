using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class BookingService : IBookingService
    {
        // South Africa has a single, fixed UTC+2 offset with no daylight
        // saving, but the server (e.g. Azure App Service on Linux) may run
        // in UTC. BookingDate/StartTime/EndTime are entered and displayed as
        // South African local time, so "is this booking in the past" is
        // computed against a fixed offset rather than the server's own
        // DateTime.Now - that keeps the check correct regardless of what
        // timezone the host machine happens to be set to.
        private static readonly TimeSpan SouthAfricaUtcOffset =
            TimeSpan.FromHours(2);

        // Valid next statuses for a booking, keyed by its current status.
        // Pending -> Cancelled covers "rejecting" a booking, since the
        // current workflow uses cancellation for rejection.
        private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedStatusTransitions = new()
        {
            [BookingStatus.Pending] = new[]
            {
                BookingStatus.Cancelled,
                BookingStatus.Completed
            },

            [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),

            [BookingStatus.Completed] = Array.Empty<BookingStatus>()
        };

        // Statuses that hold a boardroom's time slot and therefore block
        // other bookings from overlapping it.
        // Cancelled/Completed bookings no longer occupy the slot.
        private static readonly BookingStatus[] SlotHoldingStatuses =
        {
            BookingStatus.Pending
        };

        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

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

        // Retrieves boardrooms that can currently be considered for a booking
        // during the requested date and time.
        //
        // A boardroom must:
        // - Be active.
        // - Have an Available status.
        // - Not have an overlapping booking.
        //
        // This method is also used by the AI recommendation service.
        // The AI therefore receives only boardrooms that have already passed
        // the backend availability check.
        public async Task<IEnumerable<Boardroom>> GetAvailableBoardroomsAsync(
            DateOnly bookingDate,
            TimeOnly startTime,
            TimeOnly endTime)
        {
            // Get active boardrooms that are currently marked as Available.
            //
            // Equipment is included because the AI needs to know what
            // equipment each available boardroom provides when deciding
            // which room best matches the user's requirements.
            var boardrooms = await _context.Boardrooms
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .Where(b =>
                    b.IsActive &&
                    b.Status == BoardroomStatus.Available)
                .ToListAsync();

            // Find boardrooms that already have a booking occupying
            // the requested time period.
            var bookedBoardroomIds = await _context.Bookings
                .Where(b =>
                    b.BookingDate == bookingDate &&
                    SlotHoldingStatuses.Contains(b.Status) &&
                    startTime < b.EndTime &&
                    b.StartTime < endTime)
                .Select(b => b.BoardroomId)
                .Distinct()
                .ToListAsync();

            // Remove boardrooms that already have a conflicting booking.
            return boardrooms
                .Where(b => !bookedBoardroomIds.Contains(b.Id))
                .ToList();
        }

        // Creates a new booking.
        public async Task<Booking> CreateBookingAsync(Booking booking)
        {
            var boardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == booking.BoardroomId);

            if (boardroom == null)
            {
                throw new NotFoundException(
                    $"Boardroom {booking.BoardroomId} was not found.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == booking.UserId);

            if (user == null)
            {
                throw new NotFoundException(
                    $"User {booking.UserId} was not found.");
            }

            var errors = new List<string>();

            ValidateBookingRules(
                booking,
                boardroom,
                errors);

            await ValidateEquipmentAndCateringAsync(
                booking.BookingEquipments
                    .Select(e => e.EquipmentId)
                    .ToList(),

                booking.BookingCaterings
                    .Select(c => c.CateringId)
                    .ToList(),

                errors);

            // Conflict detection: does another active booking
            // on this boardroom already hold an overlapping slot?
            //
            // KNOWN LIMITATION:
            // This is a check-then-act read followed by a separate write.
            // Two requests arriving at almost the same instant could both
            // pass the check before either one commits.
            //
            // Closing that gap fully needs either a DB-level guard
            // (unique filtered index / concurrency token) or a
            // serializable transaction.
            if (await HasConflictAsync(
                booking,
                excludeBookingId: null))
            {
                errors.Add(
                    $"{boardroom.Name} is already booked for an overlapping time on " +
                    $"{booking.BookingDate:yyyy-MM-dd}.");
            }

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

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

            if (existingBooking.Status == BookingStatus.Cancelled
                || existingBooking.Status == BookingStatus.Completed)
            {
                throw new BusinessRuleException(
                    $"Booking {id} is {existingBooking.Status} and can no longer be edited.");
            }

            var boardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == booking.BoardroomId);

            if (boardroom == null)
            {
                throw new NotFoundException(
                    $"Boardroom {booking.BoardroomId} was not found.");
            }

            var errors = new List<string>();

            ValidateBookingRules(
                booking,
                boardroom,
                errors);

            await ValidateEquipmentAndCateringAsync(
                equipment
                    .Select(e => e.EquipmentId)
                    .ToList(),

                catering
                    .Select(c => c.CateringId)
                    .ToList(),

                errors);

            // Exclude this booking's own current slot from the conflict
            // check, otherwise every update would conflict with itself.
            if (await HasConflictAsync(
                booking,
                excludeBookingId: id))
            {
                errors.Add(
                    $"{boardroom.Name} is already booked for an overlapping time on " +
                    $"{booking.BookingDate:yyyy-MM-dd}.");
            }

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
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

        // Cancels a booking.
        //
        // This is a soft delete: the row stays in the database and its
        // Status moves to Cancelled, so booking history is preserved.
        public async Task<bool> DeleteBookingAsync(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return false;
            }

            if (booking.Status == BookingStatus.Completed)
            {
                throw new BusinessRuleException(
                    $"Booking {id} has already been completed and cannot be cancelled.");
            }

            booking.Status = BookingStatus.Cancelled;

            await _context.SaveChangesAsync();

            return true;
        }

        // Updates the booking status while enforcing the allowed
        // status transitions.
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

            if (booking.Status == status)
            {
                return true;
            }

            var allowedNextStatuses =
                AllowedStatusTransitions[booking.Status];

            if (!allowedNextStatuses.Contains(status))
            {
                throw new BusinessRuleException(
                    $"Booking {id} cannot move from {booking.Status} to {status}.");
            }

            booking.Status = status;

            await _context.SaveChangesAsync();

            return true;
        }

        // Filters, sorts and paginates bookings.
        public async Task<PagedResult<Booking>> SearchBookingsAsync(
            BookingQueryParameters query)
        {
            var page = query.Page < 1
                ? 1
                : query.Page;

            var pageSize = query.PageSize switch
            {
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => query.PageSize
            };

            var bookingsQuery = _context.Bookings
                .Include(b => b.Boardroom)
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .AsQueryable();

            if (query.BoardroomId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.BoardroomId == query.BoardroomId);
            }

            if (query.LocationId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.Boardroom!.LocationId == query.LocationId);
            }

            if (query.UserId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.UserId == query.UserId);
            }

            if (query.Status.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.Status == query.Status);
            }

            if (query.FromDate.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.BookingDate >= query.FromDate);
            }

            if (query.ToDate.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(
                    b => b.BookingDate <= query.ToDate);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();

                bookingsQuery = bookingsQuery.Where(b =>
                    (b.Company != null &&
                     b.Company.Contains(term))
                    ||
                    (b.Notes != null &&
                     b.Notes.Contains(term)));
            }

            var totalCount = await bookingsQuery.CountAsync();

            var items = await bookingsQuery
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Booking>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // Checks the standalone business rules that do not require
        // additional database queries.
        //
        // These rules cover:
        // - End time must be after start time.
        // - Booking cannot be in the past.
        // - Attendee count must be within boardroom capacity.
        // - Boardroom must be active.
        // - Boardroom must currently be Available.
        private static void ValidateBookingRules(
            Booking booking,
            Boardroom boardroom,
            List<string> errors)
        {
            if (booking.EndTime <= booking.StartTime)
            {
                errors.Add(
                    "End time must be after start time.");
            }

            var bookingStartUtc =
                booking.BookingDate.ToDateTime(booking.StartTime)
                - SouthAfricaUtcOffset;

            if (bookingStartUtc < DateTime.UtcNow)
            {
                errors.Add(
                    "Bookings cannot be made for a date/time that has already passed.");
            }

            if (booking.NumberOfAttendees <= 0)
            {
                errors.Add(
                    "Number of attendees must be at least 1.");
            }
            else if (booking.NumberOfAttendees > boardroom.Capacity)
            {
                errors.Add(
                    $"Number of attendees ({booking.NumberOfAttendees}) exceeds " +
                    $"{boardroom.Name}'s capacity ({boardroom.Capacity}).");
            }

            if (!boardroom.IsActive)
            {
                errors.Add(
                    $"{boardroom.Name} is not currently active and cannot be booked.");
            }
            else if (boardroom.Status != BoardroomStatus.Available)
            {
                errors.Add(
                    $"{boardroom.Name} is currently {boardroom.Status} and cannot be booked.");
            }
        }

        // Confirms every requested Equipment/Catering ID actually exists
        // and is still active.
        private async Task ValidateEquipmentAndCateringAsync(
            List<int> equipmentIds,
            List<int> cateringIds,
            List<string> errors)
        {
            if (equipmentIds.Count > 0)
            {
                var distinctIds = equipmentIds
                    .Distinct()
                    .ToList();

                var validIds = await _context.Equipments
                    .Where(e =>
                        distinctIds.Contains(e.Id)
                        && e.IsActive)
                    .Select(e => e.Id)
                    .ToListAsync();

                foreach (var invalidId in distinctIds.Except(validIds))
                {
                    errors.Add(
                        $"Equipment {invalidId} does not exist or is not currently available.");
                }
            }

            if (cateringIds.Count > 0)
            {
                var distinctIds = cateringIds
                    .Distinct()
                    .ToList();

                var validIds = await _context.Caterings
                    .Where(c =>
                        distinctIds.Contains(c.Id)
                        && c.IsActive)
                    .Select(c => c.Id)
                    .ToListAsync();

                foreach (var invalidId in distinctIds.Except(validIds))
                {
                    errors.Add(
                        $"Catering item {invalidId} does not exist or is not currently available.");
                }
            }
        }

        // Returns true if an active booking already occupies an
        // overlapping time range on the same boardroom and date.
        //
        // excludeBookingId allows UpdateBookingAsync to ignore the
        // booking's own existing row while checking for conflicts.
        private async Task<bool> HasConflictAsync(
            Booking booking,
            int? excludeBookingId)
        {
            return await _context.Bookings.AnyAsync(b =>
                b.BoardroomId == booking.BoardroomId
                && b.BookingDate == booking.BookingDate
                && SlotHoldingStatuses.Contains(b.Status)
                && (!excludeBookingId.HasValue
                    || b.Id != excludeBookingId.Value)
                && booking.StartTime < b.EndTime
                && b.StartTime < booking.EndTime);
        }
    }
}