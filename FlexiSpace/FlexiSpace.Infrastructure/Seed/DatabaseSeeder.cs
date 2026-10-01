using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FlexiSpace.Infrastructure.Seed
{
    public static class DatabaseSeeder
    {
        // Bootstrap problem this seeder exists to solve: every write to the
        // Users table (provisioning, role changes) is Administrator-only,
        // enforced by looking up the caller's Entra Object ID in this same
        // table (see AuthorizeRolesAttribute / CurrentUserService). With an
        // empty Users table nobody can pass that check, so at least one real,
        // sign-in-able Administrator has to be seeded.
        //
        // The seeded people are read from configuration, NOT hard-coded, so
        // real people's Entra Object IDs and emails never sit in the repo.
        // Put them in user secrets (dev) or App Service settings (Azure):
        //
        //   dotnet user-secrets set "SeedUsers:0:EntraObjectId" "<oid from your access token>"
        //   dotnet user-secrets set "SeedUsers:0:FirstName" "Jane"
        //   dotnet user-secrets set "SeedUsers:0:LastName"  "Doe"
        //   dotnet user-secrets set "SeedUsers:0:Email"     "jane@yourtenant.onmicrosoft.com"
        //   dotnet user-secrets set "SeedUsers:0:Role"      "Administrator"
        //
        // (SeedUsers:1:..., SeedUsers:2:... for more people.) Role is a
        // UserRole name: Administrator, CentreManager, Staff or Client.
        // Only runs when the Users table is empty.
        public static async Task SeedUsersAsync(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            if (await context.Users.AnyAsync())
            {
                return;
            }

            var seedUsers = configuration.GetSection("SeedUsers").GetChildren().ToList();
            if (seedUsers.Count == 0)
            {
                return;
            }

            // ReferenceDataSeeder runs first and creates the real sites, so a
            // location always exists here. Houghton Estate is the default
            // home location for seeded people.
            var locations = await context.Locations.ToListAsync();
            var location = locations.FirstOrDefault(l =>
                    string.Equals(l.Name, "Houghton Estate", StringComparison.OrdinalIgnoreCase))
                ?? locations.FirstOrDefault();

            if (location == null)
            {
                return;
            }

            var users = new List<User>();
            foreach (var entry in seedUsers)
            {
                if (!Guid.TryParse(entry["EntraObjectId"], out var objectId)
                    || string.IsNullOrWhiteSpace(entry["Email"])
                    || !Enum.TryParse<UserRole>(entry["Role"], ignoreCase: true, out var role))
                {
                    continue; // skip incomplete entries rather than failing startup
                }

                users.Add(new User
                {
                    EntraObjectId = objectId,
                    FirstName = entry["FirstName"] ?? string.Empty,
                    LastName = entry["LastName"] ?? string.Empty,
                    Email = entry["Email"]!,
                    Role = role,
                    LocationId = location.Id,
                    Location = location
                });
            }

            if (users.Count == 0)
            {
                return;
            }

            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }
    }
}
