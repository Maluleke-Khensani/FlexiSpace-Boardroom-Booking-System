using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{

    // This class represents the many-to-many relationship between Booking and Equipment entities.
    public class BookingEquipment
    {
        public int BookingId { get; set; }

        public Booking? Booking { get; set; } 

        public int EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
