namespace FlexiSpace.Core.Services
{
    // Sends outbound email. Kept behind an interface so BookingService (and
    // anything else that needs to notify someone by email) doesn't depend
    // on a specific provider.
    public interface IEmailService
    {
        // Plain-text email: Centre Manager alerts, booking updates and
        // cancellations, and the 1-hour reminder.
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string body);

        // The formatted (HTML) confirmation the booker gets when a booking
        // is created, including the location's name and address.
        Task SendBookingConfirmationAsync(
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
            string? notes);
    }
}
