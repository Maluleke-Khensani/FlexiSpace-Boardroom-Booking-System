namespace Flexispace.Mobile.Models;

/// <summary>
/// Mobile booking statuses. New bookings are always <see cref="Confirmed"/>.
/// <see cref="Pending"/> is unused in the product flow (kept only so old mock data
/// or API wording does not crash converters).
/// </summary>
public enum BookingStatus
{
    Confirmed,
    Pending,
    Cancelled,
    Completed
}
