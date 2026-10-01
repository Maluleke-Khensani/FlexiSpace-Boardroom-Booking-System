using FlexiSpace.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    // Stands in for IEmailService when MicrosoftGraph:TenantId/ClientId/
    // ClientSecret/SenderEmail aren't all configured. Without this,
    // registering MicrosoftGraphEmailService directly with missing config
    // throws inside the DI factory (ClientSecretCredential rejects null
    // arguments), which means every request that needs IBookingService -
    // i.e. every booking endpoint - would fail before reaching any of our
    // own code. This turns "email isn't configured yet" into "email
    // silently doesn't send, logged once per attempt" instead.
    public class NullEmailService : IEmailService
    {
        private readonly ILogger<NullEmailService> _logger;

        public NullEmailService(ILogger<NullEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            _logger.LogWarning(
                "Email to {ToEmail} ('{Subject}') was not sent - MicrosoftGraph email credentials are not configured.",
                toEmail,
                subject);

            return Task.CompletedTask;
        }

        public Task SendBookingConfirmationAsync(
            string recipientEmail,
            string recipientName,
            string boardroomName,
            string locationName,
            string locationAddress,
            DateOnly bookingDate,
            TimeOnly startTime,
            TimeOnly endTime,
            int numberOfAttendees,
            string? company,
            string? notes)
        {
            _logger.LogWarning(
                "Booking confirmation email to {ToEmail} ({Boardroom}, {Date}) was not sent - MicrosoftGraph email credentials are not configured.",
                recipientEmail,
                boardroomName,
                bookingDate);

            return Task.CompletedTask;
        }
    }
}
