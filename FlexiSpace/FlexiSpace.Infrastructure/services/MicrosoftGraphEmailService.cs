using System.Net;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.Services
{
    // Sends email via Microsoft Graph's sendMail action, using the same
    // app-only (client credentials) auth pattern as MicrosoftGraphCalendarService -
    // no interactive/user login involved, so this works from a server
    // background process the same way calendar sync does.
    //
    // Needs the "Mail.Send" Application permission (with admin consent)
    // granted to the same Entra app registration used for
    // MicrosoftGraph:TenantId/ClientId/ClientSecret. That's a one-time
    // Azure Portal step for whoever owns the app registration - this class
    // can't grant itself that permission.
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
            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

            _graphClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });

            _senderEmail = senderEmail;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = body
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = toEmail }
                    }
                }
            };

            await _graphClient
                .Users[_senderEmail]
                .SendMail
                .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });
        }

        // Khensani's HTML booking confirmation. Values people typed (names,
        // company, notes) are HTML-encoded so they show as text and can't
        // inject markup into the email.
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
            static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

            var message = new Message
            {
                Subject = $"FlexiSpace Booking Confirmation - {boardroomName}",

                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = $"""
                        <h2>Booking Confirmed</h2>

                        <p>Hello {E(recipientName)},</p>

                        <p>
                            Your FlexiSpace boardroom booking has been successfully
                            created.
                        </p>

                        <h3>Booking Details</h3>

                        <p><strong>Boardroom:</strong> {E(boardroomName)}</p>
                        <p><strong>Location:</strong> {E(locationName)}</p>
                        <p><strong>Address:</strong> {E(locationAddress)}</p>
                        <p><strong>Date:</strong> {bookingDate:dddd, dd MMMM yyyy}</p>
                        <p><strong>Time:</strong> {startTime:HH:mm} - {endTime:HH:mm}</p>
                        <p><strong>Attendees:</strong> {numberOfAttendees}</p>

                        {(string.IsNullOrWhiteSpace(company)
                            ? ""
                            : $"<p><strong>Company:</strong> {E(company)}</p>")}

                        {(string.IsNullOrWhiteSpace(notes)
                            ? ""
                            : $"<p><strong>Notes:</strong> {E(notes)}</p>")}

                        <p>
                            Please keep this email for your records.
                        </p>

                        <p>
                            Thank you,<br />
                            FlexiSpace
                        </p>
                        """
                },

                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = recipientEmail }
                    }
                }
            };

            await _graphClient
                .Users[_senderEmail]
                .SendMail
                .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });
        }
    }
}
