using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Tests
{
    public class NotificationServiceTests
    {
        // A fresh, isolated in-memory database per test (unique DB name),
        // matching the pattern used in BookingServiceTests.
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static async Task<(ApplicationDbContext Context, User UserA, User UserB)> SeedAsync()
        {
            var context = CreateContext();

            var location = new Location { Name = "Centurion", Address = "1 Example Rd" };

            var userA = new User
            {
                FirstName = "Alice",
                LastName = "A",
                Email = "alice@flexispace.net.za",
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            var userB = new User
            {
                FirstName = "Bongani",
                LastName = "B",
                Email = "bongani@flexispace.net.za",
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            context.Locations.Add(location);
            context.Users.AddRange(userA, userB);
            await context.SaveChangesAsync();

            return (context, userA, userB);
        }

        [Fact]
        public async Task CreateNotificationAsync_Throws_WhenUserDoesNotExist()
        {
            var context = CreateContext();
            var service = new NotificationService(context);

            var act = async () => await service.CreateNotificationAsync(
                999, "Title", "Message", NotificationType.BookingCreated);

            await act.Should().ThrowAsync<NotFoundException>();
        }

      

        [Fact]
        public async Task GetNotificationsForUserAsync_OnlyReturnsThatUsersNotifications_NewestFirst()
        {
            var (context, userA, userB) = await SeedAsync();
            var service = new NotificationService(context);

            await service.CreateNotificationAsync(userA.Id, "First", "msg", NotificationType.BookingCreated);
            await service.CreateNotificationAsync(userB.Id, "Not yours", "msg", NotificationType.BookingCreated);
            var second = await service.CreateNotificationAsync(userA.Id, "Second", "msg", NotificationType.BookingReminder);

            var results = await service.GetNotificationsForUserAsync(userA.Id);

            results.Should().HaveCount(2);
            results.Should().OnlyContain(n => n.UserId == userA.Id);
            results.First().Id.Should().Be(second.Id); // newest first
        }

        [Fact]
        public async Task GetUnreadCountAsync_CountsOnlyUnread()
        {
            var (context, userA, _) = await SeedAsync();
            var service = new NotificationService(context);

            var n1 = await service.CreateNotificationAsync(userA.Id, "A", "msg", NotificationType.BookingCreated);
            await service.CreateNotificationAsync(userA.Id, "B", "msg", NotificationType.BookingCreated);

            await service.MarkAsReadAsync(n1.Id, userA.Id);

            var unread = await service.GetUnreadCountAsync(userA.Id);

            unread.Should().Be(1);
        }

        [Fact]
        public async Task MarkAsReadAsync_ReturnsFalse_WhenNotificationBelongsToAnotherUser()
        {
            var (context, userA, userB) = await SeedAsync();
            var service = new NotificationService(context);

            var notification = await service.CreateNotificationAsync(
                userA.Id, "Private", "msg", NotificationType.BookingCreated);

            // userB should not be able to mark userA's notification as read
            // just by guessing its id.
            var result = await service.MarkAsReadAsync(notification.Id, userB.Id);

            result.Should().BeFalse();

            var stored = await context.Notifications.FindAsync(notification.Id);
            stored!.IsRead.Should().BeFalse();
        }

        [Fact]
        public async Task MarkAllAsReadAsync_MarksOnlyThatUsersUnreadNotifications()
        {
            var (context, userA, userB) = await SeedAsync();
            var service = new NotificationService(context);

            await service.CreateNotificationAsync(userA.Id, "A1", "msg", NotificationType.BookingCreated);
            await service.CreateNotificationAsync(userA.Id, "A2", "msg", NotificationType.BookingCreated);
            await service.CreateNotificationAsync(userB.Id, "B1", "msg", NotificationType.BookingCreated);

            var markedCount = await service.MarkAllAsReadAsync(userA.Id);

            markedCount.Should().Be(2);

            var userANotifications = await service.GetNotificationsForUserAsync(userA.Id);
            userANotifications.Should().OnlyContain(n => n.IsRead);

            var userBNotifications = await service.GetNotificationsForUserAsync(userB.Id);
            userBNotifications.Should().OnlyContain(n => !n.IsRead);
        }
    }
}
