using Azure.Identity;
using FlexiSpace.Core.Services;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace FlexiSpace.Infrastructure.services
{
    public class MicrosoftGraphEmailService : IEmailService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly string _senderEmail;

        public MicrosoftGraphEmailService(
            string tenantId,
            string clientId,
            string clientSecret,
            string senderEmail)
        {
            _senderEmail = senderEmail;

            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            _graphClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });
        }

        public async Task SendBookingConfirmationAsync(
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
            var message = new Message
            {
                Subject = $"FlexiSpace Booking Confirmation - {boardroomName}",

                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = $"""
                        <h2>Booking Confirmed</h2>

                        <p>Hello {recipientName},</p>

                        <p>
                            Your FlexiSpace boardroom booking has been successfully
                            created.
                        </p>

                        <h3>Booking Details</h3>

                        <p><strong>Boardroom:</strong> {boardroomName}</p>
                        <p><strong>Location:</strong> {locationName}</p>
                        <p><strong>Address:</strong> {locationAddress}</p>
                        <p><strong>Date:</strong> {bookingDate:dddd, dd MMMM yyyy}</p>
                        <p><strong>Time:</strong> {startTime:HH:mm} - {endTime:HH:mm}</p>
                        <p><strong>Attendees:</strong> {numberOfAttendees}</p>

                        {(string.IsNullOrWhiteSpace(company)
                            ? ""
                            : $"<p><strong>Company:</strong> {company}</p>")}

                        {(string.IsNullOrWhiteSpace(notes)
                            ? ""
                            : $"<p><strong>Notes:</strong> {notes}</p>")}

                        <p>
                            Please keep this email for your records.
                        </p>

                        <p>
                            Thank you,<br />
                            FlexiSpace
                        </p>
                        """
                },

                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = recipientEmail
                        }
                    }
                ]
            };

            await _graphClient
                .Users[_senderEmail]
                .SendMail
                .PostAsync(new SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });
        }
    }
}