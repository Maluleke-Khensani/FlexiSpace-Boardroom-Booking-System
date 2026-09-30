using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

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

        // BookingService depends on INotificationService/IEmailService/
        // ICalendarService (all best-effort, failures logged not thrown),
        // ICurrentUserService (who's making this call - drives ownership
        // checks and visibility scoping) and ILogger. actingAsUser is
        // required since almost every write now needs a recognized caller;
        // the other dependencies default to harmless mocks unless a test
        // needs to Verify() against one directly.
        private static BookingService CreateService(
            ApplicationDbContext context,
            User actingAsUser,
            Mock<INotificationService>? notificationService = null,
            Mock<IEmailService>? emailService = null,
            Mock<ICalendarService>? calendarService = null)
        {
            var currentUserService = new Mock<ICurrentUserService>();
            currentUserService.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(actingAsUser);

            return new BookingService(
                context,
                (notificationService ?? new Mock<INotificationService>()).Object,
                (emailService ?? new Mock<IEmailService>()).Object,
                (calendarService ?? new Mock<ICalendarService>()).Object,
                currentUserService.Object,
                Mock.Of<ILogger<BookingService>>());
        }

        // Seeds one Location, one Boardroom, one ordinary User, one Centre
        // Manager and one Administrator, all at that same location.
        private static async Task<(ApplicationDbContext Context, Boardroom Boardroom, User User, User CentreManager, User Administrator)> SeedAsync(
            int capacity = 10,
            bool boardroomActive = true,
            BoardroomStatus boardroomStatus = BoardroomStatus.Available,
            bool centreManagerActive = true)
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
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            var centreManager = new User
            {
                FirstName = "Manager",
                LastName = "Test",
                Email = "manager@flexispace.net.za",
                Role = UserRole.CentreManager,
                Location = location,
                EntraObjectId = Guid.NewGuid(),
                IsActive = centreManagerActive
            };

            var administrator = new User
            {
                FirstName = "Admin",
                LastName = "Test",
                Email = "admin@flexispace.net.za",
                Role = UserRole.Administrator,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            context.Locations.Add(location);
            context.Boardrooms.Add(boardroom);
            context.Users.AddRange(user, centreManager, administrator);

            await context.SaveChangesAsync();

            return (context, boardroom, user, centreManager, administrator);
        }

        // Same as SeedAsync, but also seeds a second and third boardroom
        // (componentA, componentB) plus a combined boardroom whose
        // components are componentA and componentB - mirroring Eagle
        // Canyon's Thingamajik + Whachamacallit setup, and matching the
        // room combination feature already shipped in the mobile app.
        private static async Task<(ApplicationDbContext Context, User User, Boardroom ComponentA, Boardroom ComponentB, Boardroom Combined)> SeedCombinedBoardroomsAsync()
        {
            var context = CreateContext();

            var location = new Location { Name = "Eagle Canyon", Address = "1 Example Rd" };

            var componentA = new Boardroom { Name = "Thingamajik", Capacity = 6, Location = location };
            var componentB = new Boardroom { Name = "Whachamacallit", Capacity = 6, Location = location };
            var combined = new Boardroom { Name = "Thingamajik + Whachamacallit", Capacity = 12, Location = location };

            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "user@flexispace.net.za",
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            context.Locations.Add(location);
            context.Boardrooms.AddRange(componentA, componentB, combined);
            context.Users.Add(user);

            await context.SaveChangesAsync();

            context.BoardroomComponents.AddRange(
                new BoardroomComponent { CombinedBoardroomId = combined.Id, ComponentBoardroomId = componentA.Id },
                new BoardroomComponent { CombinedBoardroomId = combined.Id, ComponentBoardroomId = componentB.Id });

            await context.SaveChangesAsync();

            return (context, user, componentA, componentB, combined);
        }

        // A booking 7 days out by default - comfortably in the future
        // regardless of the SAST/UTC offset used by the "in the past" check.
        private static Booking ValidBooking(
            int boardroomId,
            DateOnly? date = null,
            TimeOnly? start = null,
            TimeOnly? end = null,
            int attendees = 4)
        {
            return new Booking
            {
                BoardroomId = boardroomId,
                BookingDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                StartTime = start ?? new TimeOnly(10, 0),
                EndTime = end ?? new TimeOnly(11, 0),
                NumberOfAttendees = attendees
            };
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomDoesNotExist()
        {
            var (context, _, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroomId: 999);

            var act = async () => await service.CreateBookingAsync(booking);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateBookingAsync_SetsUserIdFromTheAuthenticatedCaller_NotFromTheBookingArgument()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            // Even if something upstream tried to set a different UserId,
            // CreateBookingAsync must always use the authenticated caller.
            var booking = ValidBooking(boardroom.Id);
            booking.UserId = 999999;

            var created = await service.CreateBookingAsync(booking);

            created.UserId.Should().Be(user.Id);
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenEndTimeIsNotAfterStartTime()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id, start: new TimeOnly(10, 0), end: new TimeOnly(9, 0));

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("End time"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenDateIsInThePast()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id, date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already passed"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenAttendeesExceedCapacity()
        {
            var (context, boardroom, user, _, _) = await SeedAsync(capacity: 5);
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id, attendees: 6);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("capacity"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenAttendeesIsZero()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id, attendees: 0);

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("at least 1"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomIsUnderMaintenance()
        {
            var (context, boardroom, user, _, _) = await SeedAsync(boardroomStatus: BoardroomStatus.Maintenance);
            var service = CreateService(context, user);

            var act = async () => await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("Maintenance"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenBoardroomIsInactive()
        {
            var (context, boardroom, user, _, _) = await SeedAsync(boardroomActive: false);
            var service = CreateService(context, user);

            var act = async () => await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("not currently active"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenRequestedEquipmentDoesNotExist()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id);
            booking.BookingEquipments.Add(new BookingEquipment { EquipmentId = 999, Quantity = 1 });

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("Equipment 999"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenSameEquipmentIsRequestedTwice()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id);
            booking.BookingEquipments.Add(new BookingEquipment { EquipmentId = 1, Quantity = 1 });
            booking.BookingEquipments.Add(new BookingEquipment { EquipmentId = 1, Quantity = 2 });

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("more than once"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenEquipmentQuantityIsZero()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var booking = ValidBooking(boardroom.Id);
            booking.BookingEquipments.Add(new BookingEquipment { EquipmentId = 1, Quantity = 0 });

            var act = async () => await service.CreateBookingAsync(booking);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("quantity of at least 1"));
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WithValidData()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            created.Id.Should().BeGreaterThan(0);
            created.Status.Should().Be(BookingStatus.Confirmed);
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenOverlappingBookingExistsForSameBoardroom()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            var act = async () => await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 30), new TimeOnly(11, 30)));

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenBookingsAreBackToBack()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            var created = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, date, new TimeOnly(11, 0), new TimeOnly(12, 0)));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenExistingConflictingBookingIsCancelled()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            var first = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            await service.DeleteBookingAsync(first.Id);

            var created = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            created.Id.Should().BeGreaterThan(0);
        }

        // --- Authorization ---

        [Fact]
        public async Task UpdateBookingAsync_Throws_WhenSomeoneElseTriesToEditAnotherUsersBooking()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var strangerLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var stranger = new User
            {
                FirstName = "Stranger", LastName = "User", Email = "stranger@flexispace.net.za",
                Role = UserRole.Staff, Location = strangerLocation, EntraObjectId = Guid.NewGuid()
            };
            context.Locations.Add(strangerLocation);
            context.Users.Add(stranger);
            await context.SaveChangesAsync();

            var strangerService = CreateService(context, stranger);

            var act = async () => await strangerService.UpdateBookingAsync(
                created.Id, ValidBooking(boardroom.Id), new List<BookingEquipment>(), new List<BookingCatering>());

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task UpdateBookingAsync_Succeeds_WhenCentreManagerAtTheSameLocationEditsSomeoneElsesBooking()
        {
            var (context, boardroom, user, centreManager, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var managerService = CreateService(context, centreManager);

            var result = await managerService.UpdateBookingAsync(
                created.Id, ValidBooking(boardroom.Id, attendees: 5), new List<BookingEquipment>(), new List<BookingCatering>());

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteBookingAsync_Succeeds_WhenAdministratorCancelsAnyonesBooking()
        {
            var (context, boardroom, user, _, administrator) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var adminService = CreateService(context, administrator);

            var result = await adminService.DeleteBookingAsync(created.Id);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteBookingAsync_Throws_WhenCentreManagerAtADifferentLocationTries()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var otherLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var otherManager = new User
            {
                FirstName = "Other", LastName = "Manager", Email = "other-manager@flexispace.net.za",
                Role = UserRole.CentreManager, Location = otherLocation, EntraObjectId = Guid.NewGuid()
            };
            context.Locations.Add(otherLocation);
            context.Users.Add(otherManager);
            await context.SaveChangesAsync();

            var otherManagerService = CreateService(context, otherManager);

            var act = async () => await otherManagerService.DeleteBookingAsync(created.Id);

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task UpdateBookingAsync_Succeeds_WhenTheOwnerEditsTheirOwnBooking()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var result = await service.UpdateBookingAsync(
                created.Id, ValidBooking(boardroom.Id, attendees: 6), new List<BookingEquipment>(), new List<BookingCatering>());

            result.Should().BeTrue();
        }

        // --- Visibility scoping ---

        [Fact]
        public async Task GetAllBookingsAsync_Administrator_SeesEverything()
        {
            var (context, boardroom, user, _, administrator) = await SeedAsync();
            var service = CreateService(context, user);
            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var adminService = CreateService(context, administrator);
            var result = await adminService.GetAllBookingsAsync();

            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetAllBookingsAsync_CentreManager_OnlySeesTheirOwnLocation()
        {
            var (context, boardroom, user, centreManager, _) = await SeedAsync();
            var service = CreateService(context, user);
            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var otherLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var otherBoardroom = new Boardroom { Name = "Boardroom B", Capacity = 10, Location = otherLocation };
            var otherUser = new User
            {
                FirstName = "Other", LastName = "User", Email = "other-user@flexispace.net.za",
                Role = UserRole.Staff, Location = otherLocation, EntraObjectId = Guid.NewGuid()
            };
            context.Locations.Add(otherLocation);
            context.Boardrooms.Add(otherBoardroom);
            context.Users.Add(otherUser);
            await context.SaveChangesAsync();

            var otherUserService = CreateService(context, otherUser);
            await otherUserService.CreateBookingAsync(ValidBooking(otherBoardroom.Id));

            var managerService = CreateService(context, centreManager);
            var result = await managerService.GetAllBookingsAsync();

            result.Should().ContainSingle(b => b.BoardroomId == boardroom.Id);
        }

        [Fact]
        public async Task GetAllBookingsAsync_OrdinaryUser_OnlySeesTheirOwnBookings()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);
            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var otherLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var otherUser = new User
            {
                FirstName = "Other", LastName = "User", Email = "other-user@flexispace.net.za",
                Role = UserRole.Staff, Location = boardroom.Location!, EntraObjectId = Guid.NewGuid()
            };
            context.Users.Add(otherUser);
            await context.SaveChangesAsync();

            var otherUserService = CreateService(context, otherUser);
            await otherUserService.CreateBookingAsync(ValidBooking(boardroom.Id, start: new TimeOnly(13, 0), end: new TimeOnly(14, 0)));

            var result = await service.GetAllBookingsAsync();

            result.Should().ContainSingle(b => b.UserId == user.Id);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsNull_WhenCallerCannotSeeIt()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var strangerLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var stranger = new User
            {
                FirstName = "Stranger", LastName = "User", Email = "stranger@flexispace.net.za",
                Role = UserRole.Staff, Location = strangerLocation, EntraObjectId = Guid.NewGuid()
            };
            context.Locations.Add(strangerLocation);
            context.Users.Add(stranger);
            await context.SaveChangesAsync();

            var strangerService = CreateService(context, stranger);
            var result = await strangerService.GetBookingByIdAsync(created.Id);

            result.Should().BeNull();
        }

        // --- Status workflow ---

        [Fact]
        public async Task UpdateBookingStatusAsync_Throws_WhenMarkingCompletedBeforeTheBookingHasEnded()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var act = async () => await service.UpdateBookingStatusAsync(created.Id, BookingStatus.Completed);

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("Completed"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_Throws_OnInvalidTransition()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));
            await service.DeleteBookingAsync(created.Id); // now Cancelled

            var act = async () => await service.UpdateBookingStatusAsync(created.Id, BookingStatus.Confirmed);

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_Cancels_AndSetsCancelledById()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var result = await service.UpdateBookingStatusAsync(created.Id, BookingStatus.Cancelled);

            result.Should().BeTrue();

            var updated = await service.GetBookingByIdAsync(created.Id);
            updated!.Status.Should().Be(BookingStatus.Cancelled);
            updated.CancelledById.Should().Be(user.Id);
            updated.ModifiedById.Should().Be(user.Id);
        }

        [Fact]
        public async Task DeleteBookingAsync_SoftCancels_AndSetsCancelledById()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            var result = await service.DeleteBookingAsync(created.Id);

            result.Should().BeTrue();

            var stillThere = await service.GetBookingByIdAsync(created.Id);
            stillThere.Should().NotBeNull();
            stillThere!.Status.Should().Be(BookingStatus.Cancelled);
            stillThere.CancelledById.Should().Be(user.Id);
        }

        [Fact]
        public async Task DeleteBookingAsync_ReturnsFalse_WhenBookingDoesNotExist()
        {
            var (context, _, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var result = await service.DeleteBookingAsync(999);

            result.Should().BeFalse();
        }

        // --- Update edge cases ---

        [Fact]
        public async Task UpdateBookingAsync_DoesNotConflictWithItself()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            var created = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));

            var result = await service.UpdateBookingAsync(
                created.Id,
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            result.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateBookingAsync_Throws_WhenMovedOntoAnotherBookingsSlot()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)));
            var second = await service.CreateBookingAsync(ValidBooking(boardroom.Id, date, new TimeOnly(13, 0), new TimeOnly(14, 0)));

            var act = async () => await service.UpdateBookingAsync(
                second.Id,
                ValidBooking(boardroom.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0)),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task UpdateBookingAsync_Throws_WhenBookingIsAlreadyCancelled()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));
            await service.DeleteBookingAsync(created.Id);

            var act = async () => await service.UpdateBookingAsync(
                created.Id, ValidBooking(boardroom.Id), new List<BookingEquipment>(), new List<BookingCatering>());

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task UpdateBookingAsync_DoesNotRejectAsInThePast_WhenDateAndTimeAreNotChanging()
        {
            // Regression guard: editing a booking that has already started
            // (e.g. fixing a typo in the notes) shouldn't fail the
            // "not in the past" check when the date/time isn't changing.
            var (context, boardroom, user, _, _) = await SeedAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
            var start = new TimeOnly(10, 0);
            var end = new TimeOnly(11, 0);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id, date, start, end));

            // Directly backdate the stored booking to simulate it already
            // being in progress, without going through validation again.
            var trackedBooking = await context.Bookings.FindAsync(created.Id);
            trackedBooking!.BookingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await context.SaveChangesAsync();

            var sameDateTime = ValidBooking(boardroom.Id, trackedBooking.BookingDate, start, end);
            sameDateTime.Notes = "Fixed a typo";

            var result = await service.UpdateBookingAsync(
                created.Id, sameDateTime, new List<BookingEquipment>(), new List<BookingCatering>());

            result.Should().BeTrue();
        }

        // --- Search / pagination ---

        [Fact]
        public async Task SearchBookingsAsync_FiltersByStatus()
        {
            var (context, boardroom, user, _, administrator) = await SeedAsync();
            var service = CreateService(context, user);

            var confirmed = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0)));

            var toCancel = await service.CreateBookingAsync(
                ValidBooking(boardroom.Id, start: new TimeOnly(11, 0), end: new TimeOnly(12, 0)));

            await service.DeleteBookingAsync(toCancel.Id);

            var adminService = CreateService(context, administrator);
            var result = await adminService.SearchBookingsAsync(new BookingQueryParameters { Status = BookingStatus.Confirmed });

            result.TotalCount.Should().Be(1);
            result.Items.Single().Id.Should().Be(confirmed.Id);
        }

        [Fact]
        public async Task SearchBookingsAsync_Paginates()
        {
            var (context, boardroom, user, _, administrator) = await SeedAsync();
            var service = CreateService(context, user);

            for (var i = 0; i < 5; i++)
            {
                await service.CreateBookingAsync(ValidBooking(
                    boardroom.Id,
                    date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7 + i)),
                    start: new TimeOnly(9, 0),
                    end: new TimeOnly(10, 0)));
            }

            var adminService = CreateService(context, administrator);
            var page1 = await adminService.SearchBookingsAsync(new BookingQueryParameters { Page = 1, PageSize = 2 });
            var page2 = await adminService.SearchBookingsAsync(new BookingQueryParameters { Page = 2, PageSize = 2 });

            page1.TotalCount.Should().Be(5);
            page1.TotalPages.Should().Be(3);
            page1.Items.Should().HaveCount(2);
            page2.Items.Should().HaveCount(2);
            page1.Items.Select(b => b.Id).Should().NotIntersectWith(page2.Items.Select(b => b.Id));
        }

        // --- Conjoined boardroom conflict detection ---
        // (matches the room combination feature already shipped in the
        // mobile app - Thingamajik + Whachamacallit)

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenCombinedRoomOverlapsAnExistingComponentBooking()
        {
            var (context, user, componentA, _, combined) = await SeedCombinedBoardroomsAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(componentA.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 4));

            var act = async () => await service.CreateBookingAsync(
                ValidBooking(combined.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 10));

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task CreateBookingAsync_Throws_WhenComponentOverlapsAnExistingCombinedRoomBooking()
        {
            var (context, user, componentA, _, combined) = await SeedCombinedBoardroomsAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(combined.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 10));

            var act = async () => await service.CreateBookingAsync(
                ValidBooking(componentA.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 4));

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already booked"));
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenSiblingComponentIsBookedSeparately()
        {
            var (context, user, componentA, componentB, _) = await SeedCombinedBoardroomsAsync();
            var service = CreateService(context, user);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            await service.CreateBookingAsync(ValidBooking(componentA.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 4));

            var created = await service.CreateBookingAsync(
                ValidBooking(componentB.Id, date, new TimeOnly(10, 0), new TimeOnly(11, 0), attendees: 4));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task SetBoardroomComponentsAsync_Throws_WhenComponentIsAlreadyPartOfAnotherCombination()
        {
            var (context, _, componentA, _, _) = await SeedCombinedBoardroomsAsync();
            var boardroomService = new BoardroomService(context);

            var otherCombined = new Boardroom { Name = "Confuzzled + Fiddlestix", Capacity = 12, LocationId = componentA.LocationId };
            context.Boardrooms.Add(otherCombined);
            await context.SaveChangesAsync();

            var act = async () => await boardroomService.SetBoardroomComponentsAsync(
                otherCombined.Id, new List<int> { componentA.Id });

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("already part of a different combination"));
        }

        [Fact]
        public async Task SetBoardroomComponentsAsync_Throws_WhenComponentIsItselfACombinedRoom()
        {
            var (context, _, componentA, _, combined) = await SeedCombinedBoardroomsAsync();
            var boardroomService = new BoardroomService(context);

            var thirdRoom = new Boardroom { Name = "Meeting Room", Capacity = 4, LocationId = componentA.LocationId };
            context.Boardrooms.Add(thirdRoom);
            await context.SaveChangesAsync();

            var act = async () => await boardroomService.SetBoardroomComponentsAsync(
                thirdRoom.Id, new List<int> { combined.Id });

            var exception = await act.Should().ThrowAsync<BusinessRuleException>();
            exception.Which.Errors.Should().Contain(e => e.Contains("itself a combined boardroom"));
        }

        // --- Centre-Manager notification on booking creation ---

        [Fact]
        public async Task CreateBookingAsync_NotifiesCentreManager_ButNotTheBooker()
        {
            var (context, boardroom, user, centreManager, _) = await SeedAsync();

            var notificationMock = new Mock<INotificationService>();
            var emailMock = new Mock<IEmailService>();

            var service = CreateService(context, user, notificationMock, emailMock);

            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            notificationMock.Verify(n => n.CreateNotificationAsync(
                centreManager.Id, It.IsAny<string>(), It.IsAny<string>(), NotificationType.BookingCreated),
                Times.Once);

            emailMock.Verify(e => e.SendEmailAsync(centreManager.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            notificationMock.Verify(n => n.CreateNotificationAsync(
                user.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateBookingAsync_DoesNotNotifyAnInactiveCentreManager()
        {
            var (context, boardroom, user, centreManager, _) = await SeedAsync(centreManagerActive: false);

            var notificationMock = new Mock<INotificationService>();
            var service = CreateService(context, user, notificationMock);

            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            notificationMock.Verify(n => n.CreateNotificationAsync(
                centreManager.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateBookingAsync_DoesNotNotifyTheCentreManagerWhoBookedItThemself()
        {
            var (context, boardroom, _, centreManager, _) = await SeedAsync();

            var notificationMock = new Mock<INotificationService>();
            var service = CreateService(context, centreManager, notificationMock);

            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            notificationMock.Verify(n => n.CreateNotificationAsync(
                centreManager.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateBookingAsync_DoesNotNotifyCentreManagerAtADifferentLocation()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();

            var otherLocation = new Location { Name = "Houghton", Address = "2 Example Rd" };
            var otherCentreManager = new User
            {
                FirstName = "Other", LastName = "Manager", Email = "other-manager@flexispace.net.za",
                Role = UserRole.CentreManager, Location = otherLocation, EntraObjectId = Guid.NewGuid()
            };
            context.Locations.Add(otherLocation);
            context.Users.Add(otherCentreManager);
            await context.SaveChangesAsync();

            var notificationMock = new Mock<INotificationService>();
            var service = CreateService(context, user, notificationMock);

            await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            notificationMock.Verify(n => n.CreateNotificationAsync(
                otherCentreManager.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_EvenWhenNotificationAndEmailBothThrow()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();

            var notificationMock = new Mock<INotificationService>();
            notificationMock
                .Setup(n => n.CreateNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .ThrowsAsync(new InvalidOperationException("simulated notification failure"));

            var emailMock = new Mock<IEmailService>();
            emailMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("simulated email failure"));

            var service = CreateService(context, user, notificationMock, emailMock);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            created.Id.Should().BeGreaterThan(0);
        }

        // --- Outlook calendar sync ---

        [Fact]
        public async Task CreateBookingAsync_CreatesAnOutlookEvent_WhenALocationCalendarAccountIsConfigured()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();

            context.LocationCalendarAccounts.Add(new LocationCalendarAccount
            {
                Email = "centurion@flexispace.net.za",
                DisplayName = "Centurion",
                IsPrimary = true,
                Location = boardroom.Location!
            });
            await context.SaveChangesAsync();

            var calendarMock = new Mock<ICalendarService>();
            calendarMock
                .Setup(c => c.CreateCalendarEventAsync(
                    "centurion@flexispace.net.za", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                .ReturnsAsync("outlook-event-123");

            var service = CreateService(context, user, calendarService: calendarMock);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            created.OutlookEventId.Should().Be("outlook-event-123");
            calendarMock.Verify(c => c.CreateCalendarEventAsync(
                "centurion@flexispace.net.za", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_WhenNoCalendarAccountIsConfiguredForTheLocation()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();
            var calendarMock = new Mock<ICalendarService>();

            var service = CreateService(context, user, calendarService: calendarMock);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            created.OutlookEventId.Should().BeNullOrEmpty();
            calendarMock.Verify(c => c.CreateCalendarEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task DeleteBookingAsync_DeletesTheOutlookEvent_WhenOneExists()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();

            context.LocationCalendarAccounts.Add(new LocationCalendarAccount
            {
                Email = "centurion@flexispace.net.za",
                DisplayName = "Centurion",
                IsPrimary = true,
                Location = boardroom.Location!
            });
            await context.SaveChangesAsync();

            var calendarMock = new Mock<ICalendarService>();
            calendarMock
                .Setup(c => c.CreateCalendarEventAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                .ReturnsAsync("outlook-event-123");

            var service = CreateService(context, user, calendarService: calendarMock);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            await service.DeleteBookingAsync(created.Id);

            calendarMock.Verify(c => c.DeleteCalendarEventAsync("centurion@flexispace.net.za", "outlook-event-123"), Times.Once);
        }

        [Fact]
        public async Task CreateBookingAsync_Succeeds_EvenWhenCalendarSyncThrows()
        {
            var (context, boardroom, user, _, _) = await SeedAsync();

            context.LocationCalendarAccounts.Add(new LocationCalendarAccount
            {
                Email = "centurion@flexispace.net.za",
                DisplayName = "Centurion",
                IsPrimary = true,
                Location = boardroom.Location!
            });
            await context.SaveChangesAsync();

            var calendarMock = new Mock<ICalendarService>();
            calendarMock
                .Setup(c => c.CreateCalendarEventAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("simulated Graph failure"));

            var service = CreateService(context, user, calendarService: calendarMock);

            var created = await service.CreateBookingAsync(ValidBooking(boardroom.Id));

            created.Id.Should().BeGreaterThan(0);
        }
    }
}
