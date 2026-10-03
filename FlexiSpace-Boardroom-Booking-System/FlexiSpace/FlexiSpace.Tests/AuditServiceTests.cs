using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Tests
{
    public class AuditServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static async Task<(ApplicationDbContext Context, User Admin)> SeedAsync()
        {
            var context = CreateContext();

            var location = new Location { Name = "Centurion", Address = "1 Example Rd" };

            var admin = new User
            {
                FirstName = "Denzel",
                LastName = "Admin",
                Email = "denzel.admin@flexispace.net.za",
                Role = UserRole.Administrator,
                Location = location,
                EntraObjectId = Guid.NewGuid()
            };

            context.Locations.Add(location);
            context.Users.Add(admin);
            await context.SaveChangesAsync();

            return (context, admin);
        }

        [Fact]
        public async Task LogAsync_PersistsAllFields()
        {
            var (context, admin) = await SeedAsync();
            var service = new AuditService(context);

            await service.LogAsync(
                admin.Id,
                AuditAction.Update,
                entityName: "User",
                entityId: "42",
                oldValues: "{\"IsActive\":true}",
                newValues: "{\"IsActive\":false}");

            var log = await context.AuditLogs.SingleAsync();

            log.UserId.Should().Be(admin.Id);
            log.Action.Should().Be(AuditAction.Update);
            log.EntityName.Should().Be("User");
            log.EntityId.Should().Be("42");
            log.OldValues.Should().Contain("true");
            log.NewValues.Should().Contain("false");
        }

        [Fact]
        public async Task GetLogsForEntityAsync_FiltersByEntityNameAndId_NewestFirst()
        {
            var (context, admin) = await SeedAsync();
            var service = new AuditService(context);

            await service.LogAsync(admin.Id, AuditAction.Create, "Booking", "1");
            await service.LogAsync(admin.Id, AuditAction.Update, "Booking", "1");
            await service.LogAsync(admin.Id, AuditAction.Update, "Booking", "2"); // different entity id
            await service.LogAsync(admin.Id, AuditAction.Update, "User", "1"); // different entity name

            var logs = await service.GetLogsForEntityAsync("Booking", "1");

            logs.Should().HaveCount(2);
            logs.Should().OnlyContain(l => l.EntityName == "Booking" && l.EntityId == "1");
            logs.First().Action.Should().Be(AuditAction.Update); // most recent logged second, so first
        }

        [Fact]
        public async Task GetRecentLogsAsync_RespectsCountAndOrdering()
        {
            var (context, admin) = await SeedAsync();
            var service = new AuditService(context);

            for (var i = 0; i < 5; i++)
            {
                await service.LogAsync(admin.Id, AuditAction.Create, "Booking", i.ToString());
            }

            var recent = await service.GetRecentLogsAsync(count: 3);

            recent.Should().HaveCount(3);
            recent.Select(l => l.EntityId).Should().ContainInOrder("4", "3", "2");
        }
    }
}
