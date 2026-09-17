using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

/// <summary>
/// In-memory booking store shared by mock room + booking services.
/// Replace with HttpClient implementations when the .NET API is ready.
/// </summary>
public class MockDataStore
{
    public List<Booking> Bookings { get; } = [];
    public List<BlockedPeriod> BlockedPeriods { get; } = [];

    public MockDataStore()
    {
        var today = DateTime.Today;
        var staffId = SeedData.Users[0].Id;
        var clientId = SeedData.Users.First(u => u.Role == UserRole.Client).Id;

        Bookings.AddRange(
        [
            new Booking
            {
                RoomId = "hou-exec",
                LocationId = "houghton",
                RoomName = "Executive Boardroom",
                LocationName = "Houghton Estate",
                Start = today.AddHours(10),
                End = today.AddHours(11),
                BookerId = staffId,
                BookerName = "Thabo Staff",
                Company = "Wire Giraffe",
                Attendees = 6,
                Equipment = ["TV", "Video conferencing"],
                Status = BookingStatus.Confirmed,
                OutlookEventId = "mock-outlook-001"
            },
            new Booking
            {
                RoomId = "cen-a",
                LocationId = "centurion",
                RoomName = "Boardroom A",
                LocationName = "Centurion",
                Start = today.AddHours(14),
                End = today.AddHours(15),
                BookerId = staffId,
                BookerName = "Thabo Staff",
                Company = "ACIS",
                Attendees = 4,
                Catering = ["Coffee", "Water"],
                Status = BookingStatus.Confirmed,
                OutlookEventId = "mock-outlook-002"
            },
            new Booking
            {
                RoomId = "eag-meet",
                LocationId = "eagle",
                RoomName = "Meeting Room",
                LocationName = "Eagle Canyon",
                Start = today.AddDays(1).AddHours(14),
                End = today.AddDays(1).AddHours(16),
                BookerId = staffId,
                BookerName = "Thabo Staff",
                Company = "360 Vision Events",
                Attendees = 8,
                Status = BookingStatus.Confirmed
            },
            new Booking
            {
                RoomId = "eag-fid",
                LocationId = "eagle",
                RoomName = "Fiddlestix",
                LocationName = "Eagle Canyon",
                Start = today.AddHours(9),
                End = today.AddHours(12),
                BookerId = clientId,
                BookerName = "Client Guest",
                Company = "Pro Tem",
                Attendees = 10,
                Status = BookingStatus.Pending
            }
        ]);
    }
}

