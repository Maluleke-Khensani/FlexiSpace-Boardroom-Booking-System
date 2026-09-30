using FlexiSpace.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    // Stands in for ICalendarService when MicrosoftGraph credentials aren't
    // configured - same reasoning as NullEmailService. This one matters
    // even more once BookingService depends on ICalendarService (for
    // Outlook sync): without a safe fallback, every booking endpoint would
    // fail the moment it touches calendar sync, not just the one
    // GraphTestController action that currently uses it.
    public class NullCalendarService : ICalendarService
    {
        private readonly ILogger<NullCalendarService> _logger;

        public NullCalendarService(ILogger<NullCalendarService> logger)
        {
            _logger = logger;
        }

        public Task<string> CreateCalendarEventAsync(
            string calendarEmail,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null)
        {
            _logger.LogWarning(
                "Calendar event '{Subject}' for {CalendarEmail} was not created - MicrosoftGraph credentials are not configured.",
                subject,
                calendarEmail);

            return Task.FromResult(string.Empty);
        }

        public Task UpdateCalendarEventAsync(
            string calendarEmail,
            string eventId,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null)
        {
            _logger.LogWarning(
                "Calendar event {EventId} for {CalendarEmail} was not updated - MicrosoftGraph credentials are not configured.",
                eventId,
                calendarEmail);

            return Task.CompletedTask;
        }

        public Task DeleteCalendarEventAsync(
            string calendarEmail,
            string eventId)
        {
            _logger.LogWarning(
                "Calendar event {EventId} for {CalendarEmail} was not deleted - MicrosoftGraph credentials are not configured.",
                eventId,
                calendarEmail);

            return Task.CompletedTask;
        }
    }
}
