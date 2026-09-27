using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Enums
{
    // History: this enum used to be Pending, Cancelled, Completed (before
    // that, Pending, Approved, Cancelled, Completed). Pending was renamed
    // to Confirmed when the approval step was removed - bookings are
    // created directly as Confirmed now. Renaming in place (rather than
    // adding a new value) keeps the ordinal positions unchanged, so
    // existing data stored as the old 3-value scheme (Pending=0,
    // Cancelled=1, Completed=2) still reads correctly under the new names.
    // This only holds if no real data was ever stored under the older
    // 4-value scheme that included Approved - worth a quick check against
    // any persisted data before relying on it.
    public enum BookingStatus
    {
        Confirmed,
        Cancelled,
        Completed
    }
}
