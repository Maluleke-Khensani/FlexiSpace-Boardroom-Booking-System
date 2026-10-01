using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public static class SeedData
{
    public static readonly List<OfficeLocation> Locations =
    [
        new()
        {
            Id = "centurion",
            Name = "Centurion",
            Address = "92 Koranna Ave, Doringkloof, Centurion",
            Phone = "010 822 5132",
            CentreManager = "Centre Manager",
            ManagerEmail = "centurion@flexispace.net.za",
            ImageKey = "loc_centurion.jpg",
            Tagline = "Doringkloof business hub"
        },
        new()
        {
            Id = "houghton",
            Name = "Houghton Estate",
            Address = "29 West St, Houghton Estate, Johannesburg",
            Phone = "010 443 8770",
            CentreManager = "Rebecca",
            ManagerEmail = "rebecca@flexispace.net.za",
            ImageKey = "loc_houghton.jpg",
            Tagline = "Private offices in the heart of Houghton"
        },
        new()
        {
            Id = "eagle",
            Name = "Eagle Canyon",
            Address = "Eagle Canyon Office Park, Cnr Christiaan De Wet & Dolfyn Str, Randparkridge",
            Phone = "010 443 8757",
            CentreManager = "Antoinette & Lesego",
            ManagerEmail = "antoinette@flexispace.net.za",
            ImageKey = "loc_eagle.jpg",
            Tagline = "Flexible workspace in Eagle Canyon"
        }
    ];

    public static readonly List<Boardroom> Rooms =
    [
        new() { Id = "cen-a", Name = "Boardroom A", LocationId = "centurion", Capacity = 8, Equipment = ["TV", "Whiteboard", "HDMI cables"], ImageKey = "room_cen_a.jpg" },
        new() { Id = "cen-b", Name = "Boardroom B", LocationId = "centurion", Capacity = 12, Equipment = ["Projector", "Video conferencing", "Whiteboard"], ImageKey = "room_cen_b.jpg" },
        new() { Id = "cen-t", Name = "Training Room", LocationId = "centurion", Capacity = 20, Equipment = ["Projector", "Flip chart", "Teams Room"], ImageKey = "room_cen_t.jpg" },

        new() { Id = "hou-1", Name = "Boardroom 1", LocationId = "houghton", Capacity = 6, Equipment = ["TV", "Whiteboard"], ImageKey = "room_hou_1.png" },
        new() { Id = "hou-exec", Name = "Executive Boardroom", LocationId = "houghton", Capacity = 14, Equipment = ["TV", "Video conferencing", "Teams Room", "Whiteboard"], ImageKey = "room_hou_exec.png" },

        new() { Id = "eag-con", Name = "Confuzzled", LocationId = "eagle", Capacity = 4, Equipment = ["TV", "Whiteboard"], ImageKey = "room_eag_con.jpg" },
        new() { Id = "eag-thi", Name = "Thingamajik", LocationId = "eagle", Capacity = 6, Equipment = ["TV", "HDMI cables"], ImageKey = "room_eag_thi.jpg" },
        new() { Id = "eag-wha", Name = "Whachamacallit", LocationId = "eagle", Capacity = 8, Equipment = ["Projector", "Whiteboard"], ImageKey = "room_eag_wha.jpg" },
        new() { Id = "eag-fid", Name = "Fiddlestix", LocationId = "eagle", Capacity = 10, Equipment = ["TV", "Video conferencing"], ImageKey = "room_eag_fid.jpg" },
        new() { Id = "eag-meet", Name = "Meeting Room", LocationId = "eagle", Capacity = 8, Equipment = ["TV", "Whiteboard", "HDMI cables"], ImageKey = "room_eag_meet.jpg" },
        new() { Id = "eag-train", Name = "Training Room", LocationId = "eagle", Capacity = 16, Equipment = ["Projector", "Flip chart", "Teams Room"], ImageKey = "room_eag_train.jpg" }
    ];

    public static readonly string[] EquipmentOptions =
        ["TV", "Projector", "Whiteboard", "Video conferencing", "Teams Room", "Flip chart", "HDMI cables"];

    public static readonly string[] CateringOptions =
        ["Tea", "Coffee", "Lunch", "Snacks", "Water"];
}
