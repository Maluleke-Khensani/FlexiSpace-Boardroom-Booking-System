using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.Services
{
    public class MicrosoftGraphCalendarService : ICalendarService
    {
        private readonly GraphServiceClient _graphClient;

        public MicrosoftGraphCalendarService(
            string tenantId,
            string clientId,
            string clientSecret)
        {
            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            _graphClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });
        }

        public async Task<string> CreateCalendarEventAsync(
            string calendarEmail,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null)
        {
            var calendarEvent = new Event
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = description ?? string.Empty
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "South Africa Standard Time"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "South Africa Standard Time"
                }
            };

            var createdEvent = await _graphClient
                .Users[calendarEmail]
                .Calendar
                .Events
                .PostAsync(calendarEvent);

            return createdEvent?.Id
                ?? throw new InvalidOperationException(
                    "Microsoft Graph did not return an event ID.");
        }

        public async Task UpdateCalendarEventAsync(
            string calendarEmail,
            string eventId,
            string subject,
            DateTime start,
            DateTime end,
            string? description = null)
        {
            var calendarEvent = new Event
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = description ?? string.Empty
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "South Africa Standard Time"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "South Africa Standard Time"
                }
            };

            await _graphClient
                .Users[calendarEmail]
                .Calendar
                .Events[eventId]
                .PatchAsync(calendarEvent);
        }

        public async Task DeleteCalendarEventAsync(
            string calendarEmail,
            string eventId)
        {
            await _graphClient
                .Users[calendarEmail]
                .Calendar
                .Events[eventId]
                .DeleteAsync();
        }
    }
}