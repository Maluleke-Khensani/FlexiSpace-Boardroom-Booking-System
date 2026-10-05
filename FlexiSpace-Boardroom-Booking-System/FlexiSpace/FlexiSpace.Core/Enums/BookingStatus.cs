namespace FlexiSpace.Core.Enums;

public enum BookingStatus
{
    /// <summary>Legacy / unused in the current auto-confirm flow.</summary>
    Pending = 0,
    Cancelled = 1,
    Completed = 2,
    /// <summary>Default status when a booking is created.</summary>
    Confirmed = 3
}
