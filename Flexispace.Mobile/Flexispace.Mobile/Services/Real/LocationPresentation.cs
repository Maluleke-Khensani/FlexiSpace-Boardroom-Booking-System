using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Display-only details the real API doesn't store (photos, taglines,
// contact details), keyed by location and room name, for FlexiSpace's
// real sites. Same values and lookup as Flexispace.Web's copy of this
// file - keep the two in step. Any location or room not listed here (e.g.
// one an admin adds through the API) gets a generic fallback photo.
//
// If the API ever gets image/contact fields of its own, read those first
// and keep this only as the fallback.
public static class LocationPresentation
{
    public const string FallbackLocationImage = "room_meeting.png";
    public const string FallbackRoomImage = "room_placeholder.jpg";

    private sealed record SiteInfo(
        string ImageKey,
        string Tagline,
        string Phone,
        string CentreManager,
        string ManagerEmail,
        IReadOnlyDictionary<string, string> RoomImages);

    private static readonly Dictionary<string, SiteInfo> Sites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Centurion"] = new(
            "loc_centurion.jpg",
            "Doringkloof business hub",
            "010 822 5132",
            "Centre Manager",
            "centurion@flexispace.net.za",
            Rooms(
                ("Boardroom A", "room_cen_a.jpg"),
                ("Boardroom B", "room_cen_b.jpg"),
                ("Training Room", "room_cen_t.jpg"))),

        ["Houghton Estate"] = new(
            "loc_houghton.jpg",
            "Private offices in the heart of Houghton",
            "010 443 8770",
            "Rebecca",
            "rebecca@flexispace.net.za",
            Rooms(
                ("Boardroom 1", "room_hou_1.png"),
                ("Executive Boardroom", "room_hou_exec.png"))),

        ["Eagle Canyon"] = new(
            "loc_eagle.jpg",
            "Flexible workspace in Eagle Canyon",
            "010 443 8757",
            "Antoinette & Lesego",
            "antoinette@flexispace.net.za",
            Rooms(
                ("Confuzzled", "room_eag_con.jpg"),
                ("Thingamajik", "room_eag_thi.jpg"),
                ("Whachamacallit", "room_eag_wha.jpg"),
                ("Thingamajik + Whachamacallit", "room_eag_thi.jpg"),
                ("Fiddlestix", "room_eag_fid.jpg"),
                ("Meeting Room", "room_eag_meet.jpg"),
                ("Training Room", "room_eag_train.jpg")))
    };

    // Fills in the display-only fields on a location mapped from the API.
    public static void Apply(OfficeLocation location)
    {
        if (Sites.TryGetValue(location.Name.Trim(), out var site))
        {
            location.ImageKey = site.ImageKey;
            location.Tagline = site.Tagline;
            location.Phone = site.Phone;
            location.CentreManager = site.CentreManager;
            location.ManagerEmail = site.ManagerEmail;
        }
        else
        {
            location.ImageKey = FallbackLocationImage;
            location.Tagline = location.Address;
        }
    }

    // Room names repeat across sites ("Training Room"), so the location is
    // part of the lookup.
    public static string RoomImage(string? locationName, string roomName)
    {
        if (locationName is not null
            && Sites.TryGetValue(locationName.Trim(), out var site)
            && site.RoomImages.TryGetValue(roomName.Trim(), out var image))
        {
            return image;
        }

        return FallbackRoomImage;
    }

    private static IReadOnlyDictionary<string, string> Rooms(params (string Name, string Image)[] rooms) =>
        rooms.ToDictionary(r => r.Name, r => r.Image, StringComparer.OrdinalIgnoreCase);
}
