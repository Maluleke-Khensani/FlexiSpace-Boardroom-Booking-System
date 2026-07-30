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
        public required Booking Booking { get; set; } 
        public int CateringId { get; set; }
        public required Catering Catering { get; set; }

        public int Quantity { get; set; } = 1;

    }
}
