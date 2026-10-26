using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Holds the bookings that were just saved so confirmation can show the same
/// room, time, and status Mine and Calendar load from the API.
/// </summary>
public static class BookingHandoff
{
    private static List<Booking> _created = [];

    public static void Remember(IReadOnlyList<Booking> created) =>
        _created = created.Where(b => b.Id != Guid.Empty).ToList();

    public static void Remember(Booking booking)
    {
        if (_created.Any(b => b.Id == booking.Id))
            return;

        _created = [booking];
    }

    public static Booking? Peek(Guid id) =>
        _created.FirstOrDefault(b => b.Id == id);

    public static IReadOnlyList<Booking> CreatedWith(Guid id) =>
        _created.Any(b => b.Id == id) ? _created : [];
}
