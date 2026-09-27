using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.services
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
    }
}
