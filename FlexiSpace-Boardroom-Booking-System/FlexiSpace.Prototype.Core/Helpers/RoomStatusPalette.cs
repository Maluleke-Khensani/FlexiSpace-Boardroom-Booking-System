namespace Flexispace.Core.Helpers;

public static class RoomStatusPalette
{
    public static string GetColor(string status) => status switch
    {
        "Available" => "#2E7D4F",
        "Occupied" => "#B33A3A",
        "Reserved" => "#C9A227",
        "Cleaning" => "#4A6FA5",
        "Maintenance" => "#8A6A3A",
        "Blocked" => "#6E6E6E",
        _ => "#6E6E6E"
    };
}

public static class StatusAccent
{
    public static string GetColor(Models.BookingStatus status) => status switch
    {
        Models.BookingStatus.Confirmed => "#1E8E5A",
        Models.BookingStatus.Pending => "#C4A035",
        Models.BookingStatus.Cancelled => "#C1392B",
        _ => "#5A554D"
    };
}
