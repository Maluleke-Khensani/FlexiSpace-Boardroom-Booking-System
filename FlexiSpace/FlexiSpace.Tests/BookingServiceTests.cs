using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Tests
{
    public class BookingServiceTests
    {
        // A fresh, isolated in-memory database per test (unique DB name),
        // so tests can't leak state into one another.
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        // Seeds one Location, one Boardroom and one User, and returns the
        // context plus the saved (so they have real IDs) Boardroom and User.
        private static async Task<(ApplicationDbContext Context, Boardroom Boardroom, User User)> SeedAsync(
            int capacity = 10,
            bool boardroomActive = true,
            BoardroomStatus boardroomStatus = BoardroomStatus.Available)
        {
            var context = CreateContext();

            var location = new Location { Name = "Centurion", Address = "1 Example Rd" };

            var boardroom = new Boardroom
            {
                Name = "Boardroom A",
                Capacity = capacity,
                IsActive = boardroomActive,
                Status = boardroomStatus,
                Location = location
            };

            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "user@flexispace.net.za",
                PhoneNumber = "0000000000",
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            context.Locations.Add(location);
            context.Boardrooms.Add(boardroom);
            context.Users.Add(user);

            await context.SaveChangesAsync();

            return (context, boardroom, user);
        }

        // A booking 7 days out by default - comfortably in the future
        // regardless of the SAST/UTC offset used by the "in the past" check.
        private static Booking ValidBooking(
            int boardroomId,
            int userId,
            DateOnly? date = null,
            TimeOnly? start = null,
            TimeOnly? end = null,
            int attendees = 4)
        {
            return new Booking
            {
                BoardroomId = boardroomId,
                UserId = userId,
                BookingDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                StartTime = start ?? new TimeOnly(10, 0),
                EndTime = end ?? new TimeOnly(11, 0),
                NumberOfAttendees = attendees
            };
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomDoesNotExist()
        {
            var (context, _, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(boardroomId: 999, userId: user.Id);

            var act = async () => await service.CreateBookingAsync(booking);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenUserDoesNotExist()
        {
            var (context, boardroom, _) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(boardroomId: boardroom.Id, userId: 999);

            var act = async () => await service.CreateBookingAsync(booking);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenEndTimeIsNotAfterStartTime()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(
                boardroom.Id,
                user.Id,
                start: new TimeOnly(10, 0),
                end: new TimeOnly(9, 0));

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("End time"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenDateIsInThePast()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(
                boardroom.Id,
                user.Id,
                date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already passed"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenAttendeesExceedCapacity()
        {
            var (context, boardroom, user) = await SeedAsync(capacity: 5);
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id, attendees: 6);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("capacity"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenAttendeesIsZero()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id, attendees: 0);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("at least 1"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomIsUnderMaintenance()
        {
            var (context, boardroom, user) = await SeedAsync(boardroomStatus: BoardroomStatus.Maintenance);
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("Maintenance"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomIsInactive()
        {
            var (context, boardroom, user) = await SeedAsync(boardroomActive: false);
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("not currently active"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenRequestedEquipmentDoesNotExist()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id);
            booking.BookingEquipments.Add(new BookingEquipment { EquipmentId = 999, Quantity = 1 });

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("Equipment 999"));
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WithValidData()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var booking = ValidBooking(boardroom.Id, user.Id);

            var created = await service.CreateBookingAsync(booking);

            created.Id.Should().BeGreaterThan(0);
            created.Status.Should().Be(BookingStatus.Pending);
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenOverlappingBookingExistsForSameBoardroom()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            var overlapping = ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 30), new TimeOnly(11, 30));

            var act = async () => await service.CreateBookingAsync(overlapping);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenBookingsAreBackToBack()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            // Starts exactly when the first one ends - not an overlap.
            var backToBack = ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(11, 0), new TimeOnly(12, 0));

            var created = await service.CreateBookingAsync(backToBack);

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenExistingConflictingBookingIsCancelled()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            var first = await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            await service.DeleteBookingAsync(first.Id); // soft-cancels it

            var sameSlot = ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0));

            var created = await service.CreateBookingAsync(sameSlot);

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateBookingAsync_ReturnsFalse_WhenBookingDoesNotExist()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var result = await service.UpdateBookingAsync(
                999,
                ValidBooking(boardroom.Id, user.Id),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateBookingAsync_DoesNotConflictWithItself()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            var created = await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            // Same slot, just re-saving - shouldn't conflict with its own existing row.
            var updated = ValidBooking(boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0));

            var result = await service.UpdateBookingAsync(
                created.Id, updated, new List<BookingEquipment>(), new List<BookingCatering>());

            result.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateBookingAsync_Throws_WhenMovedOntoAnotherBookingsSlot()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            var second = await service.CreateBookingAsync(ValidBooking(
                boardroom.Id, user.Id, date, new TimeOnly(13, 0), new TimeOnly(14, 0)));

            // Try to move the second booking onto the first one's slot.
            var moved = ValidBooking(boardroom.Id, user.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0));

            var act = async () => await service.UpdateBookingAsync(
                second.Id, moved, new List<BookingEquipment>(), new List<BookingCatering>());

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task UpdateBookingAsync_Throws_WhenBookingIsAlreadyCancelled()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id, user.Id));
            await service.DeleteBookingAsync(created.Id);

            var act = async () => await service.UpdateBookingAsync(
                created.Id,
                ValidBooking(boardroom.Id, user.Id),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task DeleteBookingAsync_SoftCancels_RatherThanRemovingTheRow()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id, user.Id));

            var result = await service.DeleteBookingAsync(created.Id);

            result.Should().BeTrue();

            var stillThere = await service.GetBookingByIdAsync(created.Id);
            stillThere.Should().NotBeNull();
            stillThere!.Status.Should().Be(BookingStatus.Cancelled);
        }

        [Fact]
        public async Task DeleteBookingAsync_ReturnsFalse_WhenBookingDoesNotExist()
        {
            var (context, _, _) = await SeedAsync();
            var service = new BookingService(context);

            var result = await service.DeleteBookingAsync(999);

            result.Should().BeFalse();
        }

     
       
        [Fact]
        public async Task SearchBookingsAsync_Paginates()
        {
            var (context, boardroom, user) = await SeedAsync();
            var service = new BookingService(context);

            for (var i = 0; i < 5; i++)
            {
                await service.CreateBookingAsync(ValidBooking(
                    boardroom.Id,
                    user.Id,
                    date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7 + i)),
                    start: new TimeOnly(9, 0),
                    end: new TimeOnly(10, 0)));
            }

            var page1 = await service.SearchBookingsAsync(new BookingQueryParameters { Page = 1, PageSize = 2 });
            var page2 = await service.SearchBookingsAsync(new BookingQueryParameters { Page = 2, PageSize = 2 });

            page1.TotalCount.Should().Be(5);
            page1.TotalPages.Should().Be(3);
            page1.Items.Should().HaveCount(2);
            page2.Items.Should().HaveCount(2);
            page1.Items.Select(b => b.Id).Should().NotIntersectWith(page2.Items.Select(b => b.Id));
        }
    }
}
