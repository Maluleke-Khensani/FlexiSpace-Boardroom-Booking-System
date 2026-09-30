using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Enums
{
    public enum NotificationType
    {
        BookingCreated,
        BookingCancelled,
        BookingReminder,
        BookingModified,

        // A boardroom the user already has a confirmed booking on was
        // marked Maintenance/Unavailable (see
        // BoardroomService.NotifyAffectedBookersAsync) - distinct from
        // BookingCancelled because the booking itself hasn't actually
        // been cancelled, just potentially affected.
        BookingBlocked
    }
}
