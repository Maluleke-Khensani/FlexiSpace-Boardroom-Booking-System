using Microsoft.Maui.Graphics;

namespace Flexispace.Mobile.Helpers;

public static class RoomStatusPalette
{
    public static Color GetColor(string status) => status switch
    {
        "Available" => Color.FromArgb("#2E7D4F"),
        "Occupied" => Color.FromArgb("#B33A3A"),
        "Reserved" => Color.FromArgb("#C9A227"),
        "Cleaning" => Color.FromArgb("#4A6FA5"),
        "Maintenance" => Color.FromArgb("#8A6A3A"),
        "Blocked" => Color.FromArgb("#6E6E6E"),
        _ => Color.FromArgb("#6E6E6E")
    };
}
