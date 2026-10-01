using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Seed
{
    // Seeds FlexiSpace's real sites, their boardrooms and the equipment
    // and catering catalogues (the same sites and rooms the prototypes were
    // designed around), so a fresh dev database has something real to book
    // against. Runs in Development only - see Program.cs.
    //
    // Safe to run on every startup: everything is matched by name and only
    // added if missing. Nothing existing is changed or deleted, so rooms or
    // locations the team edits, deactivates or adds by hand are left alone.
    public static class ReferenceDataSeeder
    {
        private sealed record CatalogueSeed(string Name, string Description);

        private sealed record RoomSeed(
            string Name,
            int Capacity,
            string[] Equipment,
            string[]? CombinedFrom = null);

        private sealed record LocationSeed(string Name, string Address, RoomSeed[] Rooms);

        private static readonly CatalogueSeed[] EquipmentCatalogue =
        {
            new("TV", "Wall-mounted display screen"),
            new("Projector", "Ceiling or table projector with screen"),
            new("Whiteboard", "Whiteboard with markers"),
            new("Video conferencing", "Camera, microphone and speaker for video calls"),
            new("Teams Room", "Microsoft Teams Room system"),
            new("Flip chart", "Flip chart stand with paper"),
            new("HDMI cables", "HDMI cables for connecting laptops")
        };

        // What the booking form offers under "Catering". The web app reads
        // this list from GET api/catering, so it must exist in the database.
        private static readonly CatalogueSeed[] CateringCatalogue =
        {
            new("Tea", "Tea station"),
            new("Coffee", "Filter coffee"),
            new("Lunch", "Platter lunch"),
            new("Snacks", "Biscuits and snacks"),
            new("Water", "Still and sparkling water")
        };

        private static readonly LocationSeed[] Locations =
        {
            new("Centurion", "92 Koranna Ave, Doringkloof, Centurion", new RoomSeed[]
            {
                new("Boardroom A", 8, new[] { "TV", "Whiteboard", "HDMI cables" }),
                new("Boardroom B", 12, new[] { "Projector", "Video conferencing", "Whiteboard" }),
                new("Training Room", 20, new[] { "Projector", "Flip chart", "Teams Room" })
            }),
            new("Houghton Estate", "29 West St, Houghton Estate, Johannesburg", new RoomSeed[]
            {
                new("Boardroom 1", 6, new[] { "TV", "Whiteboard" }),
                new("Executive Boardroom", 14, new[] { "TV", "Video conferencing", "Teams Room", "Whiteboard" })
            }),
            new("Eagle Canyon", "Eagle Canyon Office Park, Cnr Christiaan De Wet & Dolfyn Str, Randparkridge", new RoomSeed[]
            {
                new("Confuzzled", 4, new[] { "TV", "Whiteboard" }),
                new("Thingamajik", 6, new[] { "TV", "HDMI cables" }),
                new("Whachamacallit", 8, new[] { "Projector", "Whiteboard" }),
                new("Thingamajik + Whachamacallit", 14,
                    new[] { "TV", "HDMI cables", "Projector", "Whiteboard" },
                    CombinedFrom: new[] { "Thingamajik", "Whachamacallit" }),
                new("Fiddlestix", 10, new[] { "TV", "Video conferencing" }),
                new("Meeting Room", 8, new[] { "TV", "Whiteboard", "HDMI cables" }),
                new("Training Room", 16, new[] { "Projector", "Flip chart", "Teams Room" })
            })
        };

        public static async Task SeedLocationsAndBoardroomsAsync(ApplicationDbContext context)
        {
            // --- Equipment catalogue ---
            var equipment = await context.Equipments.ToListAsync();
            foreach (var seed in EquipmentCatalogue)
            {
                if (equipment.Any(e => SameName(e.Name, seed.Name))) continue;

                var item = new Equipment { Name = seed.Name, Description = seed.Description };
                context.Equipments.Add(item);
                equipment.Add(item);
            }

            // --- Catering catalogue ---
            var catering = await context.Caterings.ToListAsync();
            foreach (var seed in CateringCatalogue)
            {
                if (catering.Any(c => SameName(c.Name, seed.Name))) continue;

                var item = new Catering { Name = seed.Name, Description = seed.Description };
                context.Caterings.Add(item);
                catering.Add(item);
            }

            // --- Locations and their boardrooms ---
            var locations = await context.Locations
                .Include(l => l.Boardrooms)
                .ToListAsync();

            foreach (var locationSeed in Locations)
            {
                var location = locations.FirstOrDefault(l => SameName(l.Name, locationSeed.Name));
                if (location == null)
                {
                    location = new Location { Name = locationSeed.Name, Address = locationSeed.Address };
                    context.Locations.Add(location);
                    locations.Add(location);
                }

                // Ordinary rooms first, so a combined room's component rooms
                // already exist when it's created.
                foreach (var roomSeed in locationSeed.Rooms.OrderBy(r => r.CombinedFrom is null ? 0 : 1))
                {
                    if (location.Boardrooms.Any(b => SameName(b.Name, roomSeed.Name))) continue;

                    var room = new Boardroom
                    {
                        Name = roomSeed.Name,
                        Capacity = roomSeed.Capacity,
                        Location = location,
                        Status = BoardroomStatus.Available,
                        IsActive = true
                    };

                    foreach (var equipmentName in roomSeed.Equipment)
                    {
                        var item = equipment.First(e => SameName(e.Name, equipmentName));
                        room.BoardroomEquipments.Add(new BoardroomEquipment
                        {
                            Boardroom = room,
                            Equipment = item,
                            Quantity = 1
                        });
                    }

                    if (roomSeed.CombinedFrom is not null)
                    {
                        foreach (var componentName in roomSeed.CombinedFrom)
                        {
                            var component = location.Boardrooms.FirstOrDefault(b => SameName(b.Name, componentName));
                            if (component == null) continue;

                            room.Components.Add(new BoardroomComponent
                            {
                                CombinedBoardroom = room,
                                ComponentBoardroom = component
                            });
                        }
                    }

                    location.Boardrooms.Add(room);
                }
            }

            await context.SaveChangesAsync();
        }

        private static bool SameName(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
