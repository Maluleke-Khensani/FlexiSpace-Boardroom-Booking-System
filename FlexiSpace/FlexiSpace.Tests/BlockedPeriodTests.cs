using FlexiSpace.Core.Common;
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
    // Shared setup for the room-blocking tests below. Block Start/End are
    // South African local time, so "today" here is UTC + 2h, matching what
    // the services use.
    internal static class BlockingTestData
    {
        // A day comfortably in the future regardless of the SAST/UTC offset.
        public static DateTime Day => DateTime.UtcNow.Date.AddDays(7);

        public static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        public static Mock<ICurrentUserService> CurrentUser(User? user)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(user);
            return mock;
        }

        public static BlockedPeriodService CreateBlockedPeriodService(
            ApplicationDbContext context,
            User actingAsUser,
            Mock<IAuditService>? auditService = null)
        {
            return new BlockedPeriodService(
                context,
                CurrentUser(actingAsUser).Object,
                (auditService ?? new Mock<IAuditService>()).Object,
                Mock.Of<ILogger<BlockedPeriodService>>());
        }

        public static BookingService CreateBookingService(ApplicationDbContext context, User actingAsUser)
        {
            return new BookingService(
                context,
                new Mock<INotificationService>().Object,
                new Mock<IEmailService>().Object,
                new Mock<ICalendarService>().Object,
                CurrentUser(actingAsUser).Object,
                Mock.Of<ILogger<BookingService>>());
        }

        public static Booking Booking(int boardroomId, DateTime day, int startHour, int endHour) => new()
        {
            BoardroomId = boardroomId,
            BookingDate = DateOnly.FromDateTime(day),
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0),
            NumberOfAttendees = 4
        };

        public static BlockedPeriod Block(int boardroomId, DateTime start, DateTime end, string reason = "Maintenance") => new()
        {
            BoardroomId = boardroomId,
            Start = start,
            End = end,
            Reason = reason
        };

        public record World(
            ApplicationDbContext Context,
            Location Centurion,
            Location Houghton,
            Boardroom RoomA,          // Centurion
            Boardroom RoomB,          // Centurion
            User Staff,               // Centurion
            User CentreManager,       // Centurion
            User OtherCentreManager,  // Houghton
            User Administrator);

        // Two locations, two boardrooms at Centurion, and one user of each
        // kind needed to exercise the permission rules.
        public static async Task<World> SeedAsync()
        {
            var context = CreateContext();

            var centurion = new Location { Name = "Centurion", Address = "1 Example Rd" };
            var houghton = new Location { Name = "Houghton", Address = "2 Example Rd" };

            var roomA = new Boardroom { Name = "Room A", Capacity = 10, Location = centurion };
            var roomB = new Boardroom { Name = "Room B", Capacity = 10, Location = centurion };

            User NewUser(string first, UserRole role, Location location) => new()
            {
                FirstName = first,
                LastName = "Test",
                Email = $"{first.ToLower()}@flexispace.net.za",
                Role = role,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            var staff = NewUser("Staff", UserRole.Staff, centurion);
            var centreManager = NewUser("Manager", UserRole.CentreManager, centurion);
            var otherManager = NewUser("Othermanager", UserRole.CentreManager, houghton);
            var admin = NewUser("Admin", UserRole.Administrator, houghton);

            context.Locations.AddRange(centurion, houghton);
            context.Boardrooms.AddRange(roomA, roomB);
            context.Users.AddRange(staff, centreManager, otherManager, admin);
            await context.SaveChangesAsync();

            return new World(context, centurion, houghton, roomA, roomB, staff, centreManager, otherManager, admin);
        }

        // Adds a combined room made of the two Centurion rooms and returns it.
        public static async Task<Boardroom> AddCombinedRoomAsync(World w)
        {
            var combined = new Boardroom { Name = "Room A + B", Capacity = 20, Location = w.Centurion };
            w.Context.Boardrooms.Add(combined);
            await w.Context.SaveChangesAsync();

            w.Context.BoardroomComponents.AddRange(
                new BoardroomComponent { CombinedBoardroomId = combined.Id, ComponentBoardroomId = w.RoomA.Id },
                new BoardroomComponent { CombinedBoardroomId = combined.Id, ComponentBoardroomId = w.RoomB.Id });
            await w.Context.SaveChangesAsync();

            return combined;
        }
    }

    public class BlockedPeriodServiceTests
    {
        private static DateTime Day => BlockingTestData.Day;

        // ----- who can block -----

        [Fact]
        public async Task Create_Succeeds_ForAdministrator_AndRecordsWhoCreatedIt()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            created.Id.Should().BeGreaterThan(0);
            created.CreatedById.Should().Be(w.Administrator.Id);
            created.CreatedBy!.FirstName.Should().Be("Admin");
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Create_Succeeds_ForCentreManagerAtTheBoardroomsLocation()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.CentreManager);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            created.CreatedById.Should().Be(w.CentreManager.Id);
        }

        [Fact]
        public async Task Create_Throws_Forbidden_ForCentreManagerAtAnotherLocation()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.OtherCentreManager);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            await act.Should().ThrowAsync<ForbiddenException>();
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Create_Throws_Forbidden_ForStaff()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Staff);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task Create_Throws_Forbidden_WhenCallerIsNotRecognized()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = new BlockedPeriodService(
                w.Context,
                BlockingTestData.CurrentUser(null).Object,
                new Mock<IAuditService>().Object,
                Mock.Of<ILogger<BlockedPeriodService>>());

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task Create_Throws_NotFound_WhenBoardroomDoesNotExist()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(9999, Day.AddHours(9), Day.AddHours(12)));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        // ----- validation -----

        [Fact]
        public async Task Create_Throws_WhenEndIsNotAfterStart()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(12), Day.AddHours(12)));

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .Which.Errors.Should().Contain("End must be after start.");
        }

        [Fact]
        public async Task Create_Throws_WhenBlockAlreadyEnded()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, yesterday.AddHours(9), yesterday.AddHours(12)));

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .Which.Errors.Should().Contain("A block can't end in the past.");
        }

        [Fact]
        public async Task Create_Allows_ABlockThatIsAlreadyUnderway()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);
            var nowSa = DateTime.UtcNow.AddHours(2);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, nowSa.AddHours(-1), nowSa.AddHours(3)));

            created.Id.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Create_Throws_WhenReasonIsBlank(string reason)
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12), reason));

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .Which.Errors.Should().Contain("A reason is required.");
        }

        [Fact]
        public async Task Create_Throws_WhenReasonIsTooLong()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12), new string('x', 501)));

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task Create_ReportsEveryValidationErrorAtOnce()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(12), Day.AddHours(9), reason: " "));

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .Which.Errors.Should().HaveCount(2);
        }

        [Fact]
        public async Task Create_TrimsTheReason()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12), "  Deep clean  "));

            created.Reason.Should().Be("Deep clean");
        }

        // ----- existing bookings must be cancelled first -----

        [Fact]
        public async Task Create_Throws_WhenAConfirmedBookingOverlaps_AndNamesIt()
        {
            var w = await BlockingTestData.SeedAsync();
            var existing = BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11);
            existing.UserId = w.Staff.Id;
            w.Context.Bookings.Add(existing);
            await w.Context.SaveChangesAsync();

            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            (await act.Should().ThrowAsync<BusinessRuleException>())
                .WithMessage($"*#{existing.Id}*Cancel them first*");
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Create_Succeeds_WhenOnlyACancelledBookingOverlaps()
        {
            var w = await BlockingTestData.SeedAsync();
            var cancelled = BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11);
            cancelled.UserId = w.Staff.Id;
            cancelled.Status = BookingStatus.Cancelled;
            w.Context.Bookings.Add(cancelled);
            await w.Context.SaveChangesAsync();

            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Create_Succeeds_WhenABookingOnlyTouchesTheEdgeOfTheBlock()
        {
            var w = await BlockingTestData.SeedAsync();
            var before = BlockingTestData.Booking(w.RoomA.Id, Day, 9, 10);
            var after = BlockingTestData.Booking(w.RoomA.Id, Day, 12, 13);
            before.UserId = after.UserId = w.Staff.Id;
            w.Context.Bookings.AddRange(before, after);
            await w.Context.SaveChangesAsync();

            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(10), Day.AddHours(12)));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Create_Succeeds_WhenTheOverlappingBookingIsOnADifferentBoardroom()
        {
            var w = await BlockingTestData.SeedAsync();
            var other = BlockingTestData.Booking(w.RoomB.Id, Day, 10, 11);
            other.UserId = w.Staff.Id;
            w.Context.Bookings.Add(other);
            await w.Context.SaveChangesAsync();

            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Create_Throws_WhenABookingOnAComponentRoomOverlapsABlockOfTheCombinedRoom()
        {
            var w = await BlockingTestData.SeedAsync();
            var combined = await BlockingTestData.AddCombinedRoomAsync(w);
            var booking = BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11);
            booking.UserId = w.Staff.Id;
            w.Context.Bookings.Add(booking);
            await w.Context.SaveChangesAsync();

            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var act = () => service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(combined.Id, Day.AddHours(9), Day.AddHours(12)));

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        // ----- audit -----

        [Fact]
        public async Task Create_WritesAnAuditEntry()
        {
            var w = await BlockingTestData.SeedAsync();
            var audit = new Mock<IAuditService>();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator, audit);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            audit.Verify(a => a.LogAsync(
                w.Administrator.Id,
                AuditAction.Create,
                nameof(BlockedPeriod),
                created.Id.ToString(),
                null,
                It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Create_StillSucceeds_WhenTheAuditLogFails()
        {
            var w = await BlockingTestData.SeedAsync();
            var audit = new Mock<IAuditService>();
            audit.Setup(a => a.LogAsync(
                    It.IsAny<int>(), It.IsAny<AuditAction>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("audit down"));
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator, audit);

            var created = await service.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            created.Id.Should().BeGreaterThan(0);
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(1);
        }

        // ----- deleting -----

        [Fact]
        public async Task Delete_ReturnsFalse_WhenTheBlockDoesNotExist()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            (await service.DeleteBlockedPeriodAsync(12345)).Should().BeFalse();
        }

        [Fact]
        public async Task Delete_RemovesTheBlock_ForCentreManagerAtThatLocation()
        {
            var w = await BlockingTestData.SeedAsync();
            var created = await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .CreateBlockedPeriodAsync(BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            var deleted = await BlockingTestData.CreateBlockedPeriodService(w.Context, w.CentreManager)
                .DeleteBlockedPeriodAsync(created.Id);

            deleted.Should().BeTrue();
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Delete_Throws_Forbidden_ForCentreManagerAtAnotherLocation()
        {
            var w = await BlockingTestData.SeedAsync();
            var created = await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .CreateBlockedPeriodAsync(BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            var act = () => BlockingTestData.CreateBlockedPeriodService(w.Context, w.OtherCentreManager)
                .DeleteBlockedPeriodAsync(created.Id);

            await act.Should().ThrowAsync<ForbiddenException>();
            (await w.Context.BlockedPeriods.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Delete_Throws_Forbidden_ForStaff()
        {
            var w = await BlockingTestData.SeedAsync();
            var created = await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .CreateBlockedPeriodAsync(BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));

            var act = () => BlockingTestData.CreateBlockedPeriodService(w.Context, w.Staff)
                .DeleteBlockedPeriodAsync(created.Id);

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        // ----- listing -----

        [Fact]
        public async Task Get_ReturnsSoonestFirst_AndFiltersByBoardroomAndWindow()
        {
            var w = await BlockingTestData.SeedAsync();
            var admin = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            var late = await admin.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddDays(5).AddHours(9), Day.AddDays(5).AddHours(12), "Late"));
            var soon = await admin.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12), "Soon"));
            var otherRoom = await admin.CreateBlockedPeriodAsync(
                BlockingTestData.Block(w.RoomB.Id, Day.AddHours(9), Day.AddHours(12), "Other room"));

            var all = (await admin.GetBlockedPeriodsAsync(null, null, null)).ToList();
            all.Select(b => b.Reason).Should().ContainInOrder("Soon", "Other room", "Late");

            var roomA = (await admin.GetBlockedPeriodsAsync(w.RoomA.Id, null, null)).ToList();
            roomA.Select(b => b.Id).Should().BeEquivalentTo(new[] { soon.Id, late.Id });

            // Window that only overlaps the first day.
            var window = (await admin.GetBlockedPeriodsAsync(null, Day, Day.AddDays(1))).ToList();
            window.Select(b => b.Id).Should().BeEquivalentTo(new[] { soon.Id, otherRoom.Id });
        }

        [Fact]
        public async Task Get_ById_ReturnsNull_WhenMissing()
        {
            var w = await BlockingTestData.SeedAsync();
            var service = BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator);

            (await service.GetBlockedPeriodByIdAsync(404)).Should().BeNull();
        }
    }

    // How a block changes what BookingService allows.
    public class BookingBlockingTests
    {
        private static DateTime Day => BlockingTestData.Day;

        private static async Task BlockAsync(
            BlockingTestData.World w, Boardroom room, DateTime start, DateTime end)
        {
            await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .CreateBlockedPeriodAsync(BlockingTestData.Block(room.Id, start, end));
        }

        [Fact]
        public async Task CreateBooking_Throws_WhenTheRoomIsBlocked()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var act = () => bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11));

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
            (await w.Context.Bookings.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task CreateBooking_Throws_WhenOnlyPartOfTheBookingOverlapsTheBlock()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(10), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var act = () => bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 9, 11));

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
        }

        [Theory]
        [InlineData(8, 10)]   // ends exactly when the block starts
        [InlineData(12, 13)]  // starts exactly when the block ends
        [InlineData(14, 15)]  // well after
        public async Task CreateBooking_Succeeds_WhenItDoesNotOverlapTheBlock(int startHour, int endHour)
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(10), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var created = await bookings.CreateBookingAsync(
                BlockingTestData.Booking(w.RoomA.Id, Day, startHour, endHour));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CreateBooking_Succeeds_OnAnotherRoomAtTheSameTime()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomB.Id, Day, 10, 11));

            created.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CreateBooking_Throws_OnTheMiddleDayOfAMultiDayBlock()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddDays(2).AddHours(17));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var act = () => bookings.CreateBookingAsync(
                BlockingTestData.Booking(w.RoomA.Id, Day.AddDays(1), 13, 14));

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
        }

        [Fact]
        public async Task CreateBooking_Succeeds_AfterTheBlockIsRemoved()
        {
            var w = await BlockingTestData.SeedAsync();
            var block = await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .CreateBlockedPeriodAsync(BlockingTestData.Block(w.RoomA.Id, Day.AddHours(9), Day.AddHours(12)));
            await BlockingTestData.CreateBlockedPeriodService(w.Context, w.Administrator)
                .DeleteBlockedPeriodAsync(block.Id);
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11));

            created.Id.Should().BeGreaterThan(0);
        }

        // ----- combined rooms share physical space -----

        [Fact]
        public async Task BlockingAComponentRoom_AlsoBlocksTheCombinedRoom()
        {
            var w = await BlockingTestData.SeedAsync();
            var combined = await BlockingTestData.AddCombinedRoomAsync(w);
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var booking = BlockingTestData.Booking(combined.Id, Day, 10, 11);
            booking.NumberOfAttendees = 12;
            var act = () => bookings.CreateBookingAsync(booking);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
        }

        [Fact]
        public async Task BlockingTheCombinedRoom_AlsoBlocksItsComponents_ButNotUnrelatedRooms()
        {
            var w = await BlockingTestData.SeedAsync();
            var combined = await BlockingTestData.AddCombinedRoomAsync(w);
            await BlockAsync(w, combined, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var act = () => bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11));
            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");

            var act2 = () => bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomB.Id, Day, 10, 11));
            await act2.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
        }

        [Fact]
        public async Task BlockingOneComponentRoom_DoesNotBlockItsSibling()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockingTestData.AddCombinedRoomAsync(w);
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomB.Id, Day, 10, 11));

            created.Id.Should().BeGreaterThan(0);
        }

        // ----- availability search -----

        [Fact]
        public async Task GetAvailableBoardrooms_LeavesOutABlockedRoom()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var available = await bookings.GetAvailableBoardroomsAsync(
                DateOnly.FromDateTime(Day), new TimeOnly(10, 0), new TimeOnly(11, 0));

            available.Select(b => b.Id).Should().Contain(w.RoomB.Id).And.NotContain(w.RoomA.Id);
        }

        [Fact]
        public async Task GetAvailableBoardrooms_IncludesTheRoomOutsideTheBlockedHours()
        {
            var w = await BlockingTestData.SeedAsync();
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);

            var available = await bookings.GetAvailableBoardroomsAsync(
                DateOnly.FromDateTime(Day), new TimeOnly(13, 0), new TimeOnly(14, 0));

            available.Select(b => b.Id).Should().Contain(new[] { w.RoomA.Id, w.RoomB.Id });
        }

        // ----- editing an existing booking -----

        [Fact]
        public async Task UpdateBooking_Throws_WhenMovedIntoABlockedPeriod()
        {
            var w = await BlockingTestData.SeedAsync();
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);
            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 14, 15));
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));

            var act = () => bookings.UpdateBookingAsync(
                created.Id,
                BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");

            var stored = await w.Context.Bookings.AsNoTracking().SingleAsync(b => b.Id == created.Id);
            stored.StartTime.Should().Be(new TimeOnly(14, 0));
        }

        [Fact]
        public async Task UpdateBooking_Throws_WhenMovedToABlockedRoom()
        {
            var w = await BlockingTestData.SeedAsync();
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);
            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 10, 11));
            await BlockAsync(w, w.RoomB, Day.AddHours(9), Day.AddHours(12));

            var act = () => bookings.UpdateBookingAsync(
                created.Id,
                BlockingTestData.Booking(w.RoomB.Id, Day, 10, 11),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*blocked*");
        }

        [Fact]
        public async Task UpdateBooking_Succeeds_WhenTheNewSlotIsOutsideTheBlock()
        {
            var w = await BlockingTestData.SeedAsync();
            var bookings = BlockingTestData.CreateBookingService(w.Context, w.Staff);
            var created = await bookings.CreateBookingAsync(BlockingTestData.Booking(w.RoomA.Id, Day, 14, 15));
            await BlockAsync(w, w.RoomA, Day.AddHours(9), Day.AddHours(12));

            var updated = await bookings.UpdateBookingAsync(
                created.Id,
                BlockingTestData.Booking(w.RoomA.Id, Day, 15, 16),
                new List<BookingEquipment>(),
                new List<BookingCatering>());

            updated.Should().BeTrue();
        }
    }
}
