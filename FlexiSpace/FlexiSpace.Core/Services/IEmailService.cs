namespace FlexiSpace.Core.Services
{
    public interface IEmailService 
    {
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

        Task SendBookingModifiedAsync(
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

        Task SendBookingCancellationAsync(
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