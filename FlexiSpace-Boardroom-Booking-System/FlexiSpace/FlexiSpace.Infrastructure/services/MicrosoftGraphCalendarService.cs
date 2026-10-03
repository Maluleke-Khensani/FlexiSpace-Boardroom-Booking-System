using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.services
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




        // Checks if the specified calendar is available between the given start and end times.
        public async Task<bool> IsCalendarAvailableAsync(
    string calendarEmail,
    DateTime start,
    DateTime end)
        {
            var scheduleInformation = await _graphClient
                .Users[calendarEmail]
                .Calendar
                .GetSchedule
                .PostAsGetSchedulePostResponseAsync(
                    new Microsoft.Graph.Users.Item.Calendar.GetSchedule.GetSchedulePostRequestBody
                    {
                        Schedules = new List<string>
                        {
                    calendarEmail
                        },

                        StartTime = new DateTimeTimeZone
                        {
                            DateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"),
                            TimeZone = "South Africa Standard Time"
                        },

                        EndTime = new DateTimeTimeZone
                        {
                            DateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"),
                            TimeZone = "South Africa Standard Time"
                        },

                        AvailabilityViewInterval = 30
                    });

            var schedule = scheduleInformation?.Value?.FirstOrDefault();

            if (schedule == null)
            {
                return true;
            }

            if (schedule.ScheduleItems == null ||
                schedule.ScheduleItems.Count == 0)
            {
                return true;
            }

            return !schedule.ScheduleItems.Any(item =>
                item.Status?.ToString()?.Equals(
                    "busy",
                    StringComparison.OrdinalIgnoreCase) == true
                ||
                item.Status?.ToString()?.Equals(
                    "tentative",
                    StringComparison.OrdinalIgnoreCase) == true
                ||
                item.Status?.ToString()?.Equals(
                    "oof",
                    StringComparison.OrdinalIgnoreCase) == true);
        }
    }


}