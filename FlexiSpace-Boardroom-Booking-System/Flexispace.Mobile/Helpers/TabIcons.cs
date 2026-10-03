using Microsoft.Maui.Graphics;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Material Icons (PUA) glyphs for the Shell tab bar — filled vs outline pairs from
/// MaterialIcons-Regular.ttf so the selected tab reads heavier than the rest.
/// </summary>
public static class TabIcons
{
    public const string FontFamily = "MaterialIcons";
    public const double Size = 22;

    public static FontImageSource Create(string route, bool selected) => new()
    {
        FontFamily = FontFamily,
        Glyph = Glyph(route, selected),
        Size = Size,
        Color = selected ? Color.FromArgb("#111111") : Color.FromArgb("#5A554D")
    };

    public static void Apply(FontImageSource image, string route, bool selected)
    {
        image.Glyph = Glyph(route, selected);
        image.Color = selected ? Color.FromArgb("#111111") : Color.FromArgb("#5A554D");
    }

    public static string Glyph(string route, bool selected) => route switch
    {
        "HomePage" => selected ? "\ue9b2" : "\ue88a",           // home_filled / home
        "BookingPage" => selected ? "\ue878" : "\ue935",        // event / calendar_today
        "AvailabilityPage" => selected ? "\ueaa2" : "\ue51e",   // monitor_heart / sensors
        "ManagePage" => selected ? "\ue85d" : "\ue14f",         // assignment / content_paste
        "NotificationsPage" => selected ? "\ue7f4" : "\ue7f5",  // notifications / notifications_none
        "ProfilePage" => selected ? "\ue7fd" : "\ue7ff",        // person / person_outline
        _ => "\ue88a"
    };
}
