using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class Equipment
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        //To describe the specification of the equipment
        public required string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        //Many to many relationship between boardrooms and equipment, so we use a collection to represent this relationship
        //So we created BoardroomEquipment entity to represent the many-to-many relationship between Boardroom and Equipment.
        //This entity contains foreign keys to both Boardroom and Equipment, allowing us to associate multiple pieces of equipment with multiple boardrooms.
        public ICollection<BoardroomEquipment> BoardroomEquipments { get; set; }
            = new List<BoardroomEquipment>();

        // A user may request extra equipment for their booking, so we use a collection to represent this relationship
        public ICollection<BookingEquipment> BookingEquipments { get; set; }
            = new List<BookingEquipment>();
    }
}