using System.Security.Claims;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FlexiSpace.Tests
{
    public class CurrentUserServiceTests
    {
        private const string ObjectIdClaimType =
            "http://schemas.microsoft.com/identity/claims/objectidentifier";

        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        // Builds an IHttpContextAccessor whose HttpContext.User carries the
        // given object-id claim, the way Microsoft.Identity.Web would
        // populate it from a validated Entra ID token.
        private static IHttpContextAccessor AccessorWithObjectId(Guid? objectId)
        {
            var httpContext = new DefaultHttpContext();

            if (objectId.HasValue)
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ObjectIdClaimType, objectId.Value.ToString())
                });

                httpContext.User = new ClaimsPrincipal(identity);
            }

            var accessorMock = new Mock<IHttpContextAccessor>();
            accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

            return accessorMock.Object;
        }

        [Fact]
        public async Task GetCurrentUserAsync_ReturnsNull_WhenNoObjectIdClaimPresent()
        {
            var context = CreateContext();
            var service = new CurrentUserService(AccessorWithObjectId(null), context);

            var result = await service.GetCurrentUserAsync();

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentUserAsync_ReturnsNull_WhenNoUserMatchesTheObjectId()
        {
            var context = CreateContext();
            var service = new CurrentUserService(AccessorWithObjectId(Guid.NewGuid()), context);

            var result = await service.GetCurrentUserAsync();

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentUserAsync_ReturnsMatchingUser_WhenObjectIdMatchesAndActive()
        {
            var context = CreateContext();
            var entraId = Guid.NewGuid();

            var location = new Location { Name = "Centurion", Address = "1 Example Rd" };
            var user = new User
            {
                FirstName = "Denzel",
                LastName = "Admin",
                Email = "denzel@flexispace.net.za",
                Role = UserRole.Administrator,
                Location = location,
                EntraObjectId = entraId
            };

            context.Locations.Add(location);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new CurrentUserService(AccessorWithObjectId(entraId), context);

            var result = await service.GetCurrentUserAsync();

            result.Should().NotBeNull();
            result!.Id.Should().Be(user.Id);
            result.Role.Should().Be(UserRole.Administrator);
        }

        [Fact]
        public async Task GetCurrentUserAsync_ReturnsNull_WhenMatchingUserIsDeactivated()
        {
            var context = CreateContext();
            var entraId = Guid.NewGuid();

            var location = new Location { Name = "Centurion", Address = "1 Example Rd" };
            var user = new User
            {
                FirstName = "Former",
                LastName = "Employee",
                Email = "former@flexispace.net.za",
                Role = UserRole.Staff,
                Location = location,
                EntraObjectId = entraId,
                IsActive = false
            };

            context.Locations.Add(location);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new CurrentUserService(AccessorWithObjectId(entraId), context);

            var result = await service.GetCurrentUserAsync();

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentUserIdAsync_ReturnsNull_WhenGetCurrentUserAsyncReturnsNull()
        {
            var context = CreateContext();
            var service = new CurrentUserService(AccessorWithObjectId(null), context);

            var result = await service.GetCurrentUserIdAsync();

            result.Should().BeNull();
        }
    }
}
