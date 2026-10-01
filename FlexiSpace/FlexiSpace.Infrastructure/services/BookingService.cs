using System.Data;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        private static readonly TimeSpan SouthAfricaUtcOffset = TimeSpan.FromHours(2);

        // Valid next statuses for a booking, keyed by its current status.
        // Bookings are created directly as Confirmed (no approval step -
        // matches the automated flow the mobile app already ships).
        private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedStatusTransitions = new()
        {
            [BookingStatus.Confirmed] = new[] { BookingStatus.Cancelled, BookingStatus.Completed },
            [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),
            [BookingStatus.Completed] = Array.Empty<BookingStatus>()
        };

        // Statuses that hold a boardroom's time slot and therefore block
        // other bookings from overlapping it. Cancelled/Completed bookings
        // no longer occupy the slot.
        private static readonly BookingStatus[] SlotHoldingStatuses = { BookingStatus.Confirmed };

        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly ICalendarService _calendarService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            ApplicationDbContext context,
            INotificationService notificationService,
            IEmailService emailService,
            ICalendarService calendarService,
            ICurrentUserService currentUserService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
            _calendarService = calendarService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        // Retrieves every booking the current caller is allowed to see -
        // see ApplyVisibilityScope. Administrators see everything, Centre
        // Managers see their own location, everyone else sees only their
        // own bookings. This mirrors SearchBookingsAsync's scoping so the
        // plain list can't be used to bypass it.
        public async Task<IEnumerable<Booking>> GetAllBookingsAsync()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            var query = _context.Bookings
                .Include(b => b.Boardroom)
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .AsQueryable();

            query = ApplyVisibilityScope(query, currentUser);

            return await query.ToListAsync();
        }

        // Retrieves a single booking by its ID. Returns null both when the
        // booking doesn't exist and when the current caller isn't allowed
        // to see it - deliberately the same response either way, so a 404
        // doesn't leak whether a booking someone can't view actually
        // exists.
        public async Task<Booking?> GetBookingByIdAsync(int id)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            var query = _context.Bookings
                .Include(b => b.Boardroom)
                .Include(b => b.BookingEquipments)
                .Include(b => b.BookingCaterings)
                .AsQueryable();

            query = ApplyVisibilityScope(query, currentUser);

            return await query.FirstOrDefaultAsync(b => b.Id == id);
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
        // the backend availability check. Availability is a discovery
        // action open to any authenticated user - it doesn't expose whose
        // booking is occupying a slot, so it isn't visibility-scoped the
        // way booking details are.
        public async Task<IEnumerable<Boardroom>> GetAvailableBoardroomsAsync(
            DateOnly bookingDate,
            TimeOnly startTime,
            TimeOnly endTime)
        {
            var boardrooms = await _context.Boardrooms
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .Where(b => b.IsActive && b.Status == BoardroomStatus.Available)
                .ToListAsync();

            var unavailableBoardroomIds = new HashSet<int>();

            foreach (var boardroom in boardrooms)
            {
                var candidate = new Booking
                {
                    BoardroomId = boardroom.Id,
                    BookingDate = bookingDate,
                    StartTime = startTime,
                    EndTime = endTime
                };

                // Unavailable if another booking overlaps, or a Centre
                // Manager / Administrator has blocked the room for that time.
                if (await HasConflictAsync(candidate, excludeBookingId: null)
                    || await HasBlockConflictAsync(candidate))
                {
                    unavailableBoardroomIds.Add(boardroom.Id);
                }
            }

            return boardrooms
                .Where(b => !unavailableBoardroomIds.Contains(b.Id))
                .ToList();
        }

        // Creates a new booking. The booker is always the authenticated
        // caller - never taken from the request body, so nobody can book
        // (or later edit/cancel) as someone else.
        public async Task<Booking> CreateBookingAsync(Booking booking)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            booking.UserId = currentUser.Id;

            var boardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == booking.BoardroomId);

            if (boardroom == null)
            {
                throw new NotFoundException($"Boardroom {booking.BoardroomId} was not found.");
            }

            var errors = new List<string>();

            ValidateBookingRules(booking, boardroom, errors, checkPastDate: true);

            await ValidateEquipmentAndCateringAsync(
                booking.BookingEquipments.ToList(),
                booking.BookingCaterings.ToList(),
                errors);

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            async Task InsertAsync()
            {
                // Conflict detection happens inside the transaction (where
                // supported - see IsRelational below), immediately before
                // the insert, to narrow the window where two
                // near-simultaneous requests could both pass the check
                // before either commits. Not a full guarantee without a
                // DB-level unique constraint, but a real improvement over
                // checking outside any transaction at all.
                if (await HasBlockConflictAsync(booking))
                {
                    throw new BusinessRuleException(
                        $"{boardroom.Name} is blocked for that period and cannot be booked.");
                }

                if (await HasConflictAsync(booking, excludeBookingId: null))
                {
                    throw new BusinessRuleException(
                        $"{boardroom.Name} is already booked for an overlapping time on " +
                        $"{booking.BookingDate:yyyy-MM-dd}.");
                }

                _context.Bookings.Add(booking);

                await _context.SaveChangesAsync();
            }

            await RunInSerializableTransactionIfSupportedAsync(InsertAsync);

            // Best-effort side effects from here on - none of them are
            // allowed to turn an already-successful booking into a failed
            // request. Failures are logged, not thrown.
            await SyncOutlookEventOnCreateAsync(booking, boardroom, currentUser);
            await NotifyCentreManagersAsync(booking, boardroom, currentUser, isLocationChange: false);

            // Tell the booker themselves - in-app plus the formatted HTML
            // confirmation email (with the location's name and address),
            // sent immediately after the booking is created.
            var location = await _context.Locations.FindAsync(boardroom.LocationId);

            await NotifyBookerAsync(
                currentUser,
                boardroom,
                booking,
                inAppTitle: "Booking confirmed",
                inAppMessage: BuildShortSummary(booking, boardroom, "Your booking for"),
                type: NotificationType.BookingCreated,
                sendEmail: () => _emailService.SendBookingConfirmationAsync(
                    currentUser.Email,
                    $"{currentUser.FirstName} {currentUser.LastName}".Trim(),
                    boardroom.Name,
                    location?.Name ?? string.Empty,
                    location?.Address ?? string.Empty,
                    booking.BookingDate,
                    booking.StartTime,
                    booking.EndTime,
                    booking.NumberOfAttendees,
                    booking.Company,
                    booking.Notes));

            return booking;
        }

        // Updates an existing booking. Only the booking's own owner, a
        // Centre Manager at that boardroom's location, or an Administrator
        // may edit it.
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

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            var currentBoardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == existingBooking.BoardroomId);

            if (currentBoardroom == null || !CanManageBooking(currentUser, existingBooking, currentBoardroom))
            {
                throw new ForbiddenException("You don't have permission to edit this booking.");
            }

            if (existingBooking.Status == BookingStatus.Cancelled
                || existingBooking.Status == BookingStatus.Completed)
            {
                throw new BusinessRuleException(
                    $"Booking {id} is {existingBooking.Status} and can no longer be edited.");
            }

            var newBoardroom = await _context.Boardrooms
                .FirstOrDefaultAsync(b => b.Id == booking.BoardroomId);

            if (newBoardroom == null)
            {
                throw new NotFoundException($"Boardroom {booking.BoardroomId} was not found.");
            }

            var errors = new List<string>();

            // Editing a booking that's already in progress (e.g. fixing a
            // typo in the notes) shouldn't fail the "not in the past"
            // check just because the meeting has already started - only
            // check it when the date/time is actually changing.
            var dateOrTimeChanging =
                existingBooking.BookingDate != booking.BookingDate
                || existingBooking.StartTime != booking.StartTime;

            ValidateBookingRules(booking, newBoardroom, errors, checkPastDate: dateOrTimeChanging);

            await ValidateEquipmentAndCateringAsync(equipment, catering, errors);

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            var isMovingLocation = newBoardroom.LocationId != currentBoardroom.LocationId;

            async Task ApplyUpdateAsync()
            {
                if (await HasBlockConflictAsync(booking))
                {
                    throw new BusinessRuleException(
                        $"{newBoardroom.Name} is blocked for that period and cannot be booked.");
                }

                if (await HasConflictAsync(booking, excludeBookingId: id))
                {
                    throw new BusinessRuleException(
                        $"{newBoardroom.Name} is already booked for an overlapping time on " +
                        $"{booking.BookingDate:yyyy-MM-dd}.");
                }

                existingBooking.BoardroomId = booking.BoardroomId;
                existingBooking.BookingDate = booking.BookingDate;
                existingBooking.StartTime = booking.StartTime;
                existingBooking.EndTime = booking.EndTime;
                existingBooking.Company = booking.Company;
                existingBooking.NumberOfAttendees = booking.NumberOfAttendees;
                existingBooking.Notes = booking.Notes;
                existingBooking.ModifiedAt = DateTime.UtcNow;
                existingBooking.ModifiedById = currentUser.Id;

                // The time/room changed, so reminders already sent for the
                // old slot no longer apply - clear all three windows (24h,
                // 2h, 1h) so the reminder job re-evaluates this booking
                // against its new time.
                if (dateOrTimeChanging)
                {
                    existingBooking.Reminder24hSentAt = null;
                    existingBooking.Reminder2hSentAt = null;
                    existingBooking.ReminderSentAt = null;
                }

                existingBooking.BookingEquipments.Clear();

                foreach (var item in equipment)
                {
                    existingBooking.BookingEquipments.Add(item);
                }

                existingBooking.BookingCaterings.Clear();

                foreach (var item in catering)
                {
                    existingBooking.BookingCaterings.Add(item);
                }

                await _context.SaveChangesAsync();
            }

            await RunInSerializableTransactionIfSupportedAsync(ApplyUpdateAsync);

            await SyncOutlookEventOnUpdateAsync(existingBooking, newBoardroom);

            // The old location's Centre Managers already know about this
            // booking (they were notified on creation); the new location's
            // managers have never heard of it, so they're the ones who
            // need telling.
            if (isMovingLocation)
            {
                await NotifyCentreManagersAsync(existingBooking, newBoardroom, currentUser, isLocationChange: true);
            }

            // Tell the booking's owner it changed - in-app plus email,
            // regardless of who made the edit (the owner themself, a
            // Centre Manager, or an Administrator).
            var owner = await _context.Users.FindAsync(existingBooking.UserId);

            if (owner != null)
            {
                await NotifyBookerAsync(
                    owner,
                    newBoardroom,
                    existingBooking,
                    inAppTitle: "Booking updated",
                    inAppMessage: BuildShortSummary(existingBooking, newBoardroom, "Your booking has been changed to"),
                    emailSubject: $"Booking updated - {newBoardroom.Name}",
                    emailBody: BuildBookingDetailsEmailBody(
                        existingBooking,
                        newBoardroom,
                        "Your boardroom booking has been updated. Here are the current details:"),
                    type: NotificationType.BookingModified);
            }

            return true;
        }

        // Cancels a booking (soft delete - the row stays, Status moves to
        // Cancelled, so booking history is preserved). Only the booking's
        // own owner, a Centre Manager at that location, or an
        // Administrator may cancel it.
        public async Task<bool> DeleteBookingAsync(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return false;
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            var boardroom = await _context.Boardrooms.FindAsync(booking.BoardroomId);

            if (boardroom == null || !CanManageBooking(currentUser, booking, boardroom))
            {
                throw new ForbiddenException("You don't have permission to cancel this booking.");
            }

            if (booking.Status == BookingStatus.Completed)
            {
                throw new BusinessRuleException(
                    $"Booking {id} has already been completed and cannot be cancelled.");
            }

            booking.Status = BookingStatus.Cancelled;
            booking.ModifiedAt = DateTime.UtcNow;
            booking.ModifiedById = currentUser.Id;
            booking.CancelledById = currentUser.Id;

            await _context.SaveChangesAsync();

            await SyncOutlookEventOnCancelAsync(booking, boardroom);

            var owner = await _context.Users.FindAsync(booking.UserId);

            if (owner != null)
            {
                await NotifyBookerAsync(
                    owner,
                    boardroom,
                    booking,
                    inAppTitle: "Booking cancelled",
                    inAppMessage: BuildShortSummary(booking, boardroom, "Your booking for"),
                    emailSubject: $"Booking cancelled - {boardroom.Name}",
                    emailBody: BuildBookingDetailsEmailBody(
                        booking,
                        boardroom,
                        "Your boardroom booking has been cancelled. It was for:"),
                    type: NotificationType.BookingCancelled);
            }

            return true;
        }

        // Updates the booking status (Cancel, mark Completed) while
        // enforcing the allowed transitions. Only the booking's own owner,
        // a Centre Manager at that location, or an Administrator may
        // change it.
        public async Task<bool> UpdateBookingStatusAsync(
            int id,
            BookingStatus status)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return false;
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("Your account is not recognized or has been deactivated.");
            }

            var boardroom = await _context.Boardrooms.FindAsync(booking.BoardroomId);

            if (boardroom == null || !CanManageBooking(currentUser, booking, boardroom))
            {
                throw new ForbiddenException("You don't have permission to change this booking's status.");
            }

            if (booking.Status == status)
            {
                return true;
            }

            var allowedNextStatuses = AllowedStatusTransitions[booking.Status];

            if (!allowedNextStatuses.Contains(status))
            {
                throw new BusinessRuleException(
                    $"Booking {id} cannot move from {booking.Status} to {status}.");
            }

            if (status == BookingStatus.Completed)
            {
                var bookingEndUtc = booking.BookingDate.ToDateTime(booking.EndTime) - SouthAfricaUtcOffset;

                if (bookingEndUtc > DateTime.UtcNow)
                {
                    throw new BusinessRuleException(
                        $"Booking {id} can't be marked Completed before it has actually ended " +
                        $"({booking.BookingDate:yyyy-MM-dd} {booking.EndTime:HH\\:mm}).");
                }
            }

            booking.Status = status;
            booking.ModifiedAt = DateTime.UtcNow;
            booking.ModifiedById = currentUser.Id;

            if (status == BookingStatus.Cancelled)
            {
                booking.CancelledById = currentUser.Id;
            }

            await _context.SaveChangesAsync();

            if (status == BookingStatus.Cancelled)
            {
                await SyncOutlookEventOnCancelAsync(booking, boardroom);

                var owner = await _context.Users.FindAsync(booking.UserId);

                if (owner != null)
                {
                    await NotifyBookerAsync(
                        owner,
                        boardroom,
                        booking,
                        inAppTitle: "Booking cancelled",
                        inAppMessage: BuildShortSummary(booking, boardroom, "Your booking for"),
                        emailSubject: $"Booking cancelled - {boardroom.Name}",
                        emailBody: BuildBookingDetailsEmailBody(
                            booking,
                            boardroom,
                            "Your boardroom booking has been cancelled. It was for:"),
                        type: NotificationType.BookingCancelled);
                }
            }

            return true;
        }

        // Filters, sorts and paginates bookings, scoped to what the
        // current caller is allowed to see (see ApplyVisibilityScope).
        // Page/PageSize are clamped to sane bounds instead of throwing,
        // since this is a read/search endpoint rather than a write.
        public async Task<PagedResult<Booking>> SearchBookingsAsync(BookingQueryParameters query)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            var page = query.Page < 1 ? 1 : query.Page;

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

            bookingsQuery = ApplyVisibilityScope(bookingsQuery, currentUser);

            if (query.BoardroomId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.BoardroomId == query.BoardroomId);
            }

            if (query.LocationId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Boardroom!.LocationId == query.LocationId);
            }

            if (query.UserId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.UserId == query.UserId);
            }

            if (query.Status.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Status == query.Status);
            }

            if (query.FromDate.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= query.FromDate);
            }

            if (query.ToDate.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate <= query.ToDate);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();

                bookingsQuery = bookingsQuery.Where(b =>
                    (b.Company != null && b.Company.Contains(term))
                    || (b.Notes != null && b.Notes.Contains(term)));
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

        // Who can see which bookings: Administrators see everything,
        // Centre Managers see bookings at their own location, everyone
        // else sees only their own bookings. A null currentUser (token is
        // valid per [Authorize] but doesn't map to a recognized/active
        // FlexiSpace user) sees nothing, rather than leaking every
        // booking in the system.
        private static IQueryable<Booking> ApplyVisibilityScope(IQueryable<Booking> query, User? currentUser)
        {
            if (currentUser == null)
            {
                return query.Where(b => false);
            }

            return currentUser.Role switch
            {
                UserRole.Administrator => query,
                UserRole.CentreManager => query.Where(b => b.Boardroom!.LocationId == currentUser.LocationId),
                _ => query.Where(b => b.UserId == currentUser.Id)
            };
        }

        // Who may edit, cancel, or change the status of a booking: its own
        // owner, a Centre Manager at the boardroom's location, or an
        // Administrator.
        private static bool CanManageBooking(User currentUser, Booking booking, Boardroom boardroom)
        {
            if (currentUser.Role == UserRole.Administrator)
            {
                return true;
            }

            if (currentUser.Id == booking.UserId)
            {
                return true;
            }

            return currentUser.Role == UserRole.CentreManager
                && currentUser.LocationId == boardroom.LocationId;
        }

        // Wraps a write in a Serializable transaction on a real relational
        // provider, to narrow (not fully close without a DB-level unique
        // constraint) the window where two near-simultaneous requests
        // could both pass a conflict check before either commits. The
        // InMemory provider used by the test suite doesn't behave the same
        // way under transactions, so this runs the action directly there
        // instead of gambling on undocumented behavior - the plain
        // check-then-act is still correct, just without the extra guard.
        private async Task RunInSerializableTransactionIfSupportedAsync(Func<Task> action)
        {
            if (!_context.Database.IsRelational())
            {
                await action();
                return;
            }

            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Checks the standalone business rules that don't require extra DB
        // round trips beyond the boardroom already loaded by the caller.
        // checkPastDate is false on updates that aren't actually changing
        // the date/time, so editing a booking that's already started
        // (e.g. fixing a typo in the notes) doesn't get rejected for being
        // "in the past".
        private static void ValidateBookingRules(
            Booking booking,
            Boardroom boardroom,
            List<string> errors,
            bool checkPastDate)
        {
            if (booking.EndTime <= booking.StartTime)
            {
                errors.Add("End time must be after start time.");
            }

            if (checkPastDate)
            {
                var bookingStartUtc = booking.BookingDate.ToDateTime(booking.StartTime) - SouthAfricaUtcOffset;

                if (bookingStartUtc < DateTime.UtcNow)
                {
                    errors.Add("Bookings cannot be made for a date/time that has already passed.");
                }
            }

            if (booking.NumberOfAttendees <= 0)
            {
                errors.Add("Number of attendees must be at least 1.");
            }
            else if (booking.NumberOfAttendees > boardroom.Capacity)
            {
                errors.Add(
                    $"Number of attendees ({booking.NumberOfAttendees}) exceeds " +
                    $"{boardroom.Name}'s capacity ({boardroom.Capacity}).");
            }

            if (!boardroom.IsActive)
            {
                errors.Add($"{boardroom.Name} is not currently active and cannot be booked.");
            }
            else if (boardroom.Status != BoardroomStatus.Available)
            {
                errors.Add($"{boardroom.Name} is currently {boardroom.Status} and cannot be booked.");
            }
        }

        // Confirms the requested equipment/catering is well-formed: no
        // item requested twice, every quantity at least 1, and every ID
        // actually exists and is still active.
        private async Task ValidateEquipmentAndCateringAsync(
            List<BookingEquipment> equipment,
            List<BookingCatering> catering,
            List<string> errors)
        {
            foreach (var id in equipment.GroupBy(e => e.EquipmentId).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add(
                    $"Equipment {id} was requested more than once - list each item only once, " +
                    "with its total quantity.");
            }

            foreach (var id in catering.GroupBy(c => c.CateringId).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add(
                    $"Catering item {id} was requested more than once - list each item only once, " +
                    "with its total quantity.");
            }

            foreach (var item in equipment)
            {
                if (item.Quantity < 1)
                {
                    errors.Add($"Equipment {item.EquipmentId} must have a quantity of at least 1.");
                }
            }

            foreach (var item in catering)
            {
                if (item.Quantity < 1)
                {
                    errors.Add($"Catering item {item.CateringId} must have a quantity of at least 1.");
                }
            }

            if (equipment.Count > 0)
            {
                var equipmentIds = equipment.Select(e => e.EquipmentId).Distinct().ToList();

                var validIds = await _context.Equipments
                    .Where(e => equipmentIds.Contains(e.Id) && e.IsActive)
                    .Select(e => e.Id)
                    .ToListAsync();

                foreach (var invalidId in equipmentIds.Except(validIds))
                {
                    errors.Add($"Equipment {invalidId} does not exist or is not currently available.");
                }
            }

            if (catering.Count > 0)
            {
                var cateringIds = catering.Select(c => c.CateringId).Distinct().ToList();

                var validIds = await _context.Caterings
                    .Where(c => cateringIds.Contains(c.Id) && c.IsActive)
                    .Select(c => c.Id)
                    .ToListAsync();

                foreach (var invalidId in cateringIds.Except(validIds))
                {
                    errors.Add($"Catering item {invalidId} does not exist or is not currently available.");
                }
            }
        }

        // Returns true if an active booking already occupies an overlapping
        // time range on this boardroom, on a boardroom this one combines
        // with, or - if this boardroom is itself a combined space - on any
        // of its component boardrooms.
        private async Task<bool> HasConflictAsync(Booking booking, int? excludeBookingId)
        {
            var conflictingBoardroomIds = await GetConflictingBoardroomIdsAsync(booking.BoardroomId);

            return await _context.Bookings.AnyAsync(b =>
                conflictingBoardroomIds.Contains(b.BoardroomId)
                && b.BookingDate == booking.BookingDate
                && SlotHoldingStatuses.Contains(b.Status)
                && (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value)
                && booking.StartTime < b.EndTime
                && b.StartTime < booking.EndTime);
        }

        // Returns true if a Centre Manager / Administrator has blocked this
        // boardroom (or a boardroom it's physically linked to - same rule
        // as HasConflictAsync) for any part of the requested time. Block
        // Start/End are South African local time, like BookingDate and
        // StartTime/EndTime, so the two compare directly.
        private async Task<bool> HasBlockConflictAsync(Booking booking)
        {
            var conflictingBoardroomIds = await GetConflictingBoardroomIdsAsync(booking.BoardroomId);

            var bookingStart = booking.BookingDate.ToDateTime(booking.StartTime);
            var bookingEnd = booking.BookingDate.ToDateTime(booking.EndTime);

            return await _context.BlockedPeriods.AnyAsync(bp =>
                conflictingBoardroomIds.Contains(bp.BoardroomId)
                && bookingStart < bp.End
                && bp.Start < bookingEnd);
        }

        // Some boardrooms can be physically conjoined into one bigger space
        // (e.g. Eagle Canyon's Thingamajik + Whachamacallit). That
        // relationship is asymmetric for conflict purposes: booking the
        // combined room blocks every component; booking a single component
        // blocks the combined room but not its sibling component(s).
        private async Task<List<int>> GetConflictingBoardroomIdsAsync(int boardroomId)
        {
            var ids = new HashSet<int> { boardroomId };

            var componentIds = await _context.BoardroomComponents
                .Where(bc => bc.CombinedBoardroomId == boardroomId)
                .Select(bc => bc.ComponentBoardroomId)
                .ToListAsync();

            foreach (var componentId in componentIds)
            {
                ids.Add(componentId);
            }

            var combinedIds = await _context.BoardroomComponents
                .Where(bc => bc.ComponentBoardroomId == boardroomId)
                .Select(bc => bc.CombinedBoardroomId)
                .ToListAsync();

            foreach (var combinedId in combinedIds)
            {
                ids.Add(combinedId);
            }

            return ids.ToList();
        }

        // Finds which Outlook mailbox a booking at this location should
        // sync to: the location's primary active calendar account, or any
        // active one if none is marked primary. Null if the location has
        // no active calendar account configured - sync is then simply
        // skipped (same fail-open reasoning as notifications/email).
        private async Task<string?> GetSyncCalendarEmailAsync(int locationId)
        {
            var accounts = await _context.LocationCalendarAccounts
                .Where(a => a.LocationId == locationId && a.IsActive)
                .ToListAsync();

            return accounts.FirstOrDefault(a => a.IsPrimary)?.Email
                ?? accounts.FirstOrDefault()?.Email;
        }

        private async Task SyncOutlookEventOnCreateAsync(Booking booking, Boardroom boardroom, User bookedByUser)
        {
            var calendarEmail = await GetSyncCalendarEmailAsync(boardroom.LocationId);

            if (calendarEmail == null)
            {
                return;
            }

            try
            {
                var start = booking.BookingDate.ToDateTime(booking.StartTime);
                var end = booking.BookingDate.ToDateTime(booking.EndTime);

                var eventId = await _calendarService.CreateCalendarEventAsync(
                    calendarEmail,
                    $"{boardroom.Name} - {bookedByUser.FirstName} {bookedByUser.LastName}",
                    start,
                    end,
                    booking.Notes);

                if (!string.IsNullOrEmpty(eventId))
                {
                    booking.OutlookEventId = eventId;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync booking {BookingId} to Outlook.", booking.Id);
            }
        }

        private async Task SyncOutlookEventOnUpdateAsync(Booking booking, Boardroom boardroom)
        {
            if (string.IsNullOrEmpty(booking.OutlookEventId))
            {
                return;
            }

            var calendarEmail = await GetSyncCalendarEmailAsync(boardroom.LocationId);

            if (calendarEmail == null)
            {
                return;
            }

            try
            {
                var start = booking.BookingDate.ToDateTime(booking.StartTime);
                var end = booking.BookingDate.ToDateTime(booking.EndTime);

                await _calendarService.UpdateCalendarEventAsync(
                    calendarEmail,
                    booking.OutlookEventId,
                    boardroom.Name,
                    start,
                    end,
                    booking.Notes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Outlook event for booking {BookingId}.", booking.Id);
            }
        }

        private async Task SyncOutlookEventOnCancelAsync(Booking booking, Boardroom boardroom)
        {
            if (string.IsNullOrEmpty(booking.OutlookEventId))
            {
                return;
            }

            var calendarEmail = await GetSyncCalendarEmailAsync(boardroom.LocationId);

            if (calendarEmail == null)
            {
                return;
            }

            try
            {
                await _calendarService.DeleteCalendarEventAsync(calendarEmail, booking.OutlookEventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Outlook event for booking {BookingId}.", booking.Id);
            }
        }

        // Notifies every active Centre Manager at the boardroom's location
        // (except the acting user themself, if they happen to be one) that
        // a booking was created or moved here. Sends both an in-app
        // notification and an email via Graph - neither is allowed to fail
        // the request itself; failures are logged, not thrown.
        private async Task NotifyCentreManagersAsync(
            Booking booking,
            Boardroom boardroom,
            User actingUser,
            bool isLocationChange)
        {
            var centreManagers = await _context.Users
                .Where(u =>
                    u.Role == UserRole.CentreManager
                    && u.LocationId == boardroom.LocationId
                    && u.IsActive
                    && u.Id != actingUser.Id)
                .ToListAsync();

            var subject = isLocationChange
                ? $"Booking moved here - {boardroom.Name}"
                : $"New booking - {boardroom.Name}";

            var message = isLocationChange
                ? $"A booking was moved to {boardroom.Name} on {booking.BookingDate:yyyy-MM-dd} " +
                  $"{booking.StartTime:HH\\:mm}-{booking.EndTime:HH\\:mm} " +
                  $"(moved by {actingUser.FirstName} {actingUser.LastName})."
                : $"New booking: {boardroom.Name} on {booking.BookingDate:yyyy-MM-dd} " +
                  $"{booking.StartTime:HH\\:mm}-{booking.EndTime:HH\\:mm} " +
                  $"(booked by {actingUser.FirstName} {actingUser.LastName}).";

            foreach (var centreManager in centreManagers)
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        centreManager.Id,
                        subject,
                        message,
                        NotificationType.BookingCreated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to create in-app notification for Centre Manager {UserId} for booking {BookingId}.",
                        centreManager.Id,
                        booking.Id);
                }

                try
                {
                    await _emailService.SendEmailAsync(centreManager.Email, subject, message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to email Centre Manager {UserId} for booking {BookingId}.",
                        centreManager.Id,
                        booking.Id);
                }
            }
        }

        // Notifies the booking's own owner (the person who actually holds
        // the room) - in-app plus email - for create/update/cancel and the
        // 1-hour-before reminder (see BookingReminderHostedService, which
        // calls the same email-body builder directly). Best-effort, same
        // as NotifyCentreManagersAsync above: a failure here is logged,
        // never allowed to fail the booking operation itself.
        private async Task NotifyBookerAsync(
            User booker,
            Boardroom boardroom,
            Booking booking,
            string inAppTitle,
            string inAppMessage,
            NotificationType type,
            string? emailSubject = null,
            string? emailBody = null,
            Func<Task>? sendEmail = null)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    booker.Id,
                    inAppTitle,
                    inAppMessage,
                    type);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to create in-app notification for booker {UserId} for booking {BookingId}.",
                    booker.Id,
                    booking.Id);
            }

            try
            {
                // A caller can supply its own email (e.g. the HTML booking
                // confirmation); otherwise the plain-text subject/body is sent.
                if (sendEmail != null)
                {
                    await sendEmail();
                }
                else if (emailSubject != null && emailBody != null)
                {
                    await _emailService.SendEmailAsync(booker.Email, emailSubject, emailBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to email booker {UserId} for booking {BookingId}.",
                    booker.Id,
                    booking.Id);
            }
        }

        // Short one-line summary used for the in-app notification, which
        // doesn't have room for the full detail dump the email gets.
        private static string BuildShortSummary(Booking booking, Boardroom boardroom, string leadIn)
        {
            return $"{leadIn} {boardroom.Name} on {booking.BookingDate:yyyy-MM-dd} " +
                   $"{booking.StartTime:HH\\:mm}-{booking.EndTime:HH\\:mm}.";
        }

        // Builds the full "necessary info about their booking" email body:
        // room, date/time, company and attendee count, and any notes -
        // shared by create/update/cancel notifications above and by
        // BookingReminderHostedService's 1-hour-before reminder.
        internal static string BuildBookingDetailsEmailBody(Booking booking, Boardroom boardroom, string leadLine)
        {
            var lines = new List<string>
            {
                leadLine,
                string.Empty,
                $"Room: {boardroom.Name}",
                $"Date: {booking.BookingDate:yyyy-MM-dd}",
                $"Time: {booking.StartTime:HH\\:mm} - {booking.EndTime:HH\\:mm}",
                $"Attendees: {booking.NumberOfAttendees}"
            };

            if (!string.IsNullOrWhiteSpace(booking.Company))
            {
                lines.Add($"Company: {booking.Company}");
            }

            if (!string.IsNullOrWhiteSpace(booking.Notes))
            {
                lines.Add($"Notes: {booking.Notes}");
            }

            lines.Add(string.Empty);
            lines.Add($"Booking reference: #{booking.Id}");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
