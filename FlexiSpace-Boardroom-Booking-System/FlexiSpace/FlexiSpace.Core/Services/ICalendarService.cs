namespace FlexiSpace.Core.Services
{
    public interface ICalendarService
    {
        Task<string> CreateCalendarEventAsync(
            string calendarEmail,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null);

        Task UpdateCalendarEventAsync(
            string calendarEmail,
            string eventId,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null);

        Task DeleteCalendarEventAsync(
            string calendarEmail,
            string eventId);

        Task<bool> IsCalendarAvailableAsync(
            string calendarEmail,
            DateTime start,
            DateTime end);
    }
}