using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class BookingCatering
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; } 
        public int CateringId { get; set; }
        public Catering? Catering { get; set; }

        public int Quantity { get; set; } = 1;

    }
}
