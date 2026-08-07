
using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.Entities
{
    public class Boardroom
    {
        public int Id { get; set; }

        public int LocationId { get; set; }


        //relationship: Many boardrooms can belong to one location, so we use a navigation property to represent this relationship
        //Location Location in simple terms means that each boardroom is associated with a specific location, and this property allows us to access the details of that location directly from the boardroom entity.
        public Location? Location { get; set; }


        //One Boardroom can have many bookings, so we use a collection to represent this relationship
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        public required string Name { get; set; }

        public int Capacity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public BoardroomStatus Status { get; set; } = BoardroomStatus.Available;

        public ICollection<BoardroomEquipment> BoardroomEquipments { get; set; } = new List<BoardroomEquipment>();

    }
}
