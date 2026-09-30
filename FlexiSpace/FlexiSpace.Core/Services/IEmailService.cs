namespace FlexiSpace.Core.Services
{
    // Sends outbound email. Kept behind an interface so BookingService (and
    // anything else that needs to notify someone by email) doesn't depend
    // on a specific provider.
    public interface IEmailService
    {
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string body);
    }
}
