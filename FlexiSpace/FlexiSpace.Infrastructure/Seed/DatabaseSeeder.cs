using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Seed
{
    public static class DatabaseSeeder
    {
        public static async Task SeedUsersAsync(ApplicationDbContext context)
        {
            // Don't create duplicate users
            if (await context.Users.AnyAsync())
            {
                return;
            }

            // Users require an existing location
            var location = await context.Locations.FirstOrDefaultAsync();

            if (location == null)
            {
                return;
            }

            var users = new List<User>
            {
                new User
                {
                    EntraObjectId = Guid.NewGuid(),
                    FirstName = "John",
                    LastName = "Smith",
                    Email = "john.smith@flexispace.co.za",
                    PhoneNumber = "0821234567",
                    Role = UserRole.CentreManager,
                    LocationId = location.Id,
                    Location = location
                },

                new User
                {
                    EntraObjectId = Guid.NewGuid(),
                    FirstName = "Sarah",
                    LastName = "Jacobs",
                    Email = "sarah.jacobs@flexispace.co.za",
                    PhoneNumber = "0839876543",
                    Role = UserRole.Staff,
                    LocationId = location.Id,
                    Location = location
                },

                // Previously nobody was seeded with Administrator, so
                // there was no way to test the [AuthorizeRoles(Administrator)]
                // endpoints (UserController) without manually editing the
                // Role column in SSMS after the fact.
                new User
                {
                    EntraObjectId = Guid.NewGuid(),
                    FirstName = "Denzel",
                    LastName = "Admin",
                    Email = "denzel.admin@flexispace.co.za",
                    PhoneNumber = "0821112222",
                    Role = UserRole.Administrator,
                    LocationId = location.Id,
                    Location = location
                }
            };

            context.Users.AddRange(users);

            await context.SaveChangesAsync();
        }
    }
}