using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class Catering
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public  string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<BookingCatering> BookingCaterings { get; set; }
    = new List<BookingCatering>();
    }
}
