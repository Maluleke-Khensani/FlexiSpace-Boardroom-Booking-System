using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Seed;

public static class DatabaseSeeder
{
    // Stable ids for local demo tiles only (not real Entra tenant accounts).
    public static readonly Guid OidCenturionCm = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000001");
    public static readonly Guid OidRebecca = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000002");
    public static readonly Guid OidAntoinette = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000003");
    public static readonly Guid OidLesego = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000004");
    public static readonly Guid OidStaff = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000005");
    public static readonly Guid OidAdmin = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000006");
    public static readonly Guid OidClient = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000007");

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await SeedLocationsAndRoomsAsync(context);
        await SeedUsersInternalAsync(context);
        await RemoveMistakenLocalEntraCopiesAsync(context);
        await EnsureEntraTeamDirectoryRowsAsync(context);
    }

    /// <summary>
    /// Directory rows for real fspace Entra accounts (no passwords — auth via Entra).
    /// Roles match what Khensani provisioned; oid is filled on first successful sign-in.
    /// </summary>
    private static async Task EnsureEntraTeamDirectoryRowsAsync(ApplicationDbContext context)
    {
        var houghton = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Houghton Estate");
        var centurion = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Centurion");

        await UpsertDirectoryUserAsync(
            context,
            email: "khensanimaluleke309_gmail.com#ext#@fspace.onmicrosoft.com",
            firstName: "Khensani",
            lastName: "Admin",
            role: UserRole.Administrator,
            locationId: null);

        // Live fspace tenant accounts (password lives in Entra only).
        await UpsertDirectoryUserAsync(
            context,
            email: "manager1@fspace.onmicrosoft.com",
            firstName: "Manager",
            lastName: "One",
            role: UserRole.CentreManager,
            locationId: houghton?.Id ?? centurion?.Id);

        await UpsertDirectoryUserAsync(
            context,
            email: "staff1@fspace.onmicrosoft.com",
            firstName: "Staff",
            lastName: "One",
            role: UserRole.Staff,
            locationId: houghton?.Id ?? centurion?.Id);

        await UpsertDirectoryUserAsync(
            context,
            email: "staff2@fspace.onmicrosoft.com",
            firstName: "Staff",
            lastName: "Two",
            role: UserRole.Staff,
            locationId: houghton?.Id ?? centurion?.Id);

        // Optional lab accounts — kept so they link correctly if created in Entra later.
        await UpsertDirectoryUserAsync(
            context,
            email: "user1@fspace.onmicrosoft.com",
            firstName: "User",
            lastName: "One",
            role: UserRole.CentreManager,
            locationId: houghton?.Id ?? centurion?.Id);

        await UpsertDirectoryUserAsync(
            context,
            email: "testuser2@fspace.onmicrosoft.com",
            firstName: "Test",
            lastName: "User2",
            role: UserRole.Staff,
            locationId: houghton?.Id ?? centurion?.Id);
    }

    private static async Task UpsertDirectoryUserAsync(
        ApplicationDbContext context,
        string email,
        string firstName,
        string lastName,
        UserRole role,
        int? locationId)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        if (existing is not null)
        {
            // Keep role/location aligned with team directory; never write a password hash.
            existing.Role = role;
            existing.LocationId = locationId;
            existing.IsActive = true;
            existing.PasswordHash = null;
            await context.SaveChangesAsync();
            return;
        }

        context.Users.Add(new User
        {
            // Temporary oid until first Entra login replaces it with the real object id.
            EntraObjectId = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = normalized,
            PhoneNumber = string.Empty,
            PasswordHash = null,
            Role = role,
            LocationId = locationId,
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Removes temporary local rows that were once created with placeholder OIDs
    /// for real tenant accounts (passwords must never live in app code).
    /// </summary>
    private static async Task RemoveMistakenLocalEntraCopiesAsync(ApplicationDbContext context)
    {
        var staleOids = new[]
        {
            Guid.Parse("aaaaaaaa-0001-0001-0001-000000000011"),
            Guid.Parse("aaaaaaaa-0001-0001-0001-000000000012"),
            Guid.Parse("aaaaaaaa-0001-0001-0001-000000000013")
        };

        var stale = await context.Users.Where(u => staleOids.Contains(u.EntraObjectId)).ToListAsync();
        if (stale.Count == 0) return;
        context.Users.RemoveRange(stale);
        await context.SaveChangesAsync();
    }

    /// <summary>Backward-compatible entry point used by Program.cs.</summary>
    public static Task SeedUsersAsync(ApplicationDbContext context) =>
        SeedAsync(context);

    private static async Task SeedLocationsAndRoomsAsync(ApplicationDbContext context)
    {
        if (await context.Locations.AnyAsync())
            return;

        var centurion = new Location
        {
            Name = "Centurion",
            Address = "92 Koranna Ave, Doringkloof, Centurion"
        };
        var houghton = new Location
        {
            Name = "Houghton Estate",
            Address = "29 West St, Houghton Estate, Johannesburg"
        };
        var eagle = new Location
        {
            Name = "Eagle Canyon",
            Address = "Eagle Canyon Office Park, Cnr Christiaan De Wet & Dolfyn Str, Randparkridge"
        };

        context.Locations.AddRange(centurion, houghton, eagle);
        await context.SaveChangesAsync();

        context.Boardrooms.AddRange(
            new Boardroom { Name = "Boardroom A", LocationId = centurion.Id, Capacity = 8 },
            new Boardroom { Name = "Boardroom B", LocationId = centurion.Id, Capacity = 12 },
            new Boardroom { Name = "Training Room", LocationId = centurion.Id, Capacity = 20 },
            new Boardroom { Name = "Boardroom 1", LocationId = houghton.Id, Capacity = 6 },
            new Boardroom { Name = "Executive Boardroom", LocationId = houghton.Id, Capacity = 14 },
            new Boardroom { Name = "Confuzzled", LocationId = eagle.Id, Capacity = 4 },
            new Boardroom { Name = "Thingamajik", LocationId = eagle.Id, Capacity = 6 },
            new Boardroom { Name = "Whachamacallit", LocationId = eagle.Id, Capacity = 8 },
            new Boardroom { Name = "Fiddlestix", LocationId = eagle.Id, Capacity = 10 },
            new Boardroom { Name = "Meeting Room", LocationId = eagle.Id, Capacity = 8 },
            new Boardroom { Name = "Training Room", LocationId = eagle.Id, Capacity = 16 }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedUsersInternalAsync(ApplicationDbContext context)
    {
        if (await context.Users.AnyAsync())
            return;

        var centurion = await context.Locations.FirstAsync(l => l.Name == "Centurion");
        var houghton = await context.Locations.FirstAsync(l => l.Name == "Houghton Estate");
        var eagle = await context.Locations.FirstAsync(l => l.Name == "Eagle Canyon");

        context.Users.AddRange(
            new User
            {
                EntraObjectId = OidCenturionCm,
                FirstName = "Centurion",
                LastName = "Manager",
                Email = "centurion@flexispace.net.za",
                PhoneNumber = "0108225132",
                Role = UserRole.CentreManager,
                LocationId = centurion.Id
            },
            new User
            {
                EntraObjectId = OidRebecca,
                FirstName = "Rebecca",
                LastName = "Centre",
                Email = "rebecca@flexispace.net.za",
                PhoneNumber = "0104438770",
                Role = UserRole.CentreManager,
                LocationId = houghton.Id
            },
            new User
            {
                EntraObjectId = OidAntoinette,
                FirstName = "Antoinette",
                LastName = "Centre",
                Email = "antoinette@flexispace.net.za",
                PhoneNumber = "0104438757",
                Role = UserRole.CentreManager,
                LocationId = eagle.Id
            },
            new User
            {
                EntraObjectId = OidLesego,
                FirstName = "Lesego",
                LastName = "Centre",
                Email = "lesego@flexispace.net.za",
                PhoneNumber = "0104438758",
                Role = UserRole.CentreManager,
                LocationId = eagle.Id
            },
            new User
            {
                EntraObjectId = OidStaff,
                FirstName = "Thabo",
                LastName = "Staff",
                Email = "staff@flexispace.net.za",
                PhoneNumber = "0100000001",
                Role = UserRole.Staff,
                LocationId = houghton.Id
            },
            new User
            {
                EntraObjectId = OidAdmin,
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@flexispace.net.za",
                PhoneNumber = "0100000002",
                Role = UserRole.Administrator,
                LocationId = null
            },
            new User
            {
                EntraObjectId = OidClient,
                FirstName = "Client",
                LastName = "Guest",
                Email = "client@example.com",
                PhoneNumber = "0100000003",
                Role = UserRole.Client,
                LocationId = null
            }
        );

        await context.SaveChangesAsync();
    }
}
