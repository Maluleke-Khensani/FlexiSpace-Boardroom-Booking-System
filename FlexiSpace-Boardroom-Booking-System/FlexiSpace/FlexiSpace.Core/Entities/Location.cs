using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    public class Location
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public required string Address { get; set; }

        // Navigation property for the related LocationType entity

        // One location can have many boardrooms, so we use a collection to represent this relationship
        public ICollection<Boardroom> Boardrooms { get; set; } = new List<Boardroom>();

        // One location can have many users, so we use a collection to represent this relationship
        public ICollection<User> Users { get; set; } = new List<User>();
    
        public ICollection<LocationCalendarAccount> LocationCalendarAccounts { get; set; } = new List<LocationCalendarAccount>();
    }
    
}
