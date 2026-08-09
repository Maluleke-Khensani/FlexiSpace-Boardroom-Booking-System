using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Entities
{
    //This class represents the many-to-many relationship between Boardrooms and Equipment, allowing for the specification of the quantity of each piece of equipment available in a boardroom.
    //No Id property is needed because the combination of BoardroomId and EquipmentId serves as a composite key, uniquely identifying each record in the relationship.
    //Has a Composite Key: The combination of BoardroomId and EquipmentId serves as a composite key, uniquely identifying each record in the relationship.

    public class BoardroomEquipment
    {
        public int BoardroomId { get; set; }

        public Boardroom? Boardroom { get; set; }

        public int EquipmentId { get; set; }

        public Equipment? Equipment { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
