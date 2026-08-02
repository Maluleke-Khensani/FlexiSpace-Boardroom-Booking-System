using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class LocationCalendarAccount
    {
        public int Id { get; set; }
        public required string Email { get; set; }

        public required string DisplayName { get; set; }


        //Suppose Eagle Canyon has three calendars.

        // Which one should receive new bookings first?
        public bool IsPrimary { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int LocationId { get; set; }

        public required Location Location { get; set; }
    }

}
