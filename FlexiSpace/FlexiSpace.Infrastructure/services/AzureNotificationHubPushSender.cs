using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiSpace.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    // Sends mobile push notifications through Azure Notification Hubs.
    //
    // Replaces FcmPushNotificationSender, which called Firebase directly -
    // the client's approved architecture is Azure-only. The backend now only
    // talks to Azure; Notification Hubs delivers to Android (via FCM v1,
    // configured on the hub in the Azure portal) and to iOS (via APNs).
    //
    // Uses the Notification Hubs REST API ("direct send" to each stored
    // device token) rather than the Microsoft.Azure.NotificationHubs NuGet
    // package, so it adds no dependency and keeps the existing DeviceTokens
    // table as-is.
    //
    // Does nothing (logs once per send) until both settings are present, so
    // the API runs fine without a hub:
    //   NotificationHubs:ConnectionString  - the hub's DefaultFullSharedAccessSignature
    //                                        connection string (keep it in user secrets /
    //                                        App Service settings, never appsettings.json)
    //   NotificationHubs:HubName           - the hub's name
    public class AzureNotificationHubPushSender : IPushNotificationSender
    {
        // Notification Hubs REST API version that supports the "fcmv1"
        // format. Check against the Azure docs when the hub is first set up.
        private const string ApiVersion = "2023-10-01-preview";

        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AzureNotificationHubPushSender> _logger;

        public AzureNotificationHubPushSender(
            IDeviceTokenService deviceTokenService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<AzureNotificationHubPushSender> logger)
        {
            _deviceTokenService = deviceTokenService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendAsync(int userId, string title, string body)
        {
            var connectionString = _configuration["NotificationHubs:ConnectionString"];
            var hubName = _configuration["NotificationHubs:HubName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(hubName))
            {
                _logger.LogWarning(
                    "Push notification skipped for user {UserId} - Azure Notification Hubs is not configured.",
                    userId);
                return;
            }

            var devices = await _deviceTokenService.GetDevicesForUserAsync(userId);
            if (devices.Count == 0)
            {
                return;
            }

            try
            {
                var hub = HubConnection.Parse(connectionString, hubName);
                var client = _httpClientFactory.CreateClient(nameof(AzureNotificationHubPushSender));

                foreach (var device in devices)
                {
                    var isApple = IsApplePlatform(device.Platform);
                    var payload = isApple
                        ? JsonSerializer.Serialize(new { aps = new { alert = new { title, body } } })
                        : JsonSerializer.Serialize(new { message = new { notification = new { title, body } } });

                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"{hub.BaseUri}/messages/?direct&api-version={ApiVersion}");

                    request.Headers.TryAddWithoutValidation("Authorization", hub.CreateSasToken(TimeSpan.FromMinutes(10)));
                    request.Headers.TryAddWithoutValidation("ServiceBusNotification-Format", isApple ? "apple" : "fcmv1");
                    request.Headers.TryAddWithoutValidation("ServiceBusNotification-DeviceHandle", device.Token);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning(
                            "Notification Hubs push failed for user {UserId} ({StatusCode}): {Error}",
                            userId, response.StatusCode, error);
                    }
                }
            }
            catch (Exception ex)
            {
                // A push failure must never fail the booking/notification
                // that triggered it.
                _logger.LogError(ex, "Push notification failed for user {UserId}.", userId);
            }
        }

        private static bool IsApplePlatform(string? platform) =>
            platform is not null
            && (platform.Equals("ios", StringComparison.OrdinalIgnoreCase)
                || platform.Equals("apple", StringComparison.OrdinalIgnoreCase)
                || platform.Equals("iphone", StringComparison.OrdinalIgnoreCase));

        // Parsed "Endpoint=sb://<namespace>.servicebus.windows.net/;
        // SharedAccessKeyName=...;SharedAccessKey=..." connection string.
        private sealed class HubConnection
        {
            public required string BaseUri { get; init; }
            public required string KeyName { get; init; }
            public required string Key { get; init; }

            public static HubConnection Parse(string connectionString, string hubName)
            {
                var parts = connectionString
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

                if (!parts.TryGetValue("Endpoint", out var endpoint)
                    || !parts.TryGetValue("SharedAccessKeyName", out var keyName)
                    || !parts.TryGetValue("SharedAccessKey", out var key))
                {
                    throw new InvalidOperationException(
                        "NotificationHubs:ConnectionString must contain Endpoint, SharedAccessKeyName and SharedAccessKey.");
                }

                var host = new Uri(endpoint.Replace("sb://", "https://", StringComparison.OrdinalIgnoreCase));
                return new HubConnection
                {
                    BaseUri = $"https://{host.Host}/{hubName}",
                    KeyName = keyName,
                    Key = key
                };
            }

            // Standard Service Bus SAS token, scoped to this hub.
            public string CreateSasToken(TimeSpan lifetime)
            {
                var expiry = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds().ToString();
                var resource = WebUtility.UrlEncode(BaseUri.ToLowerInvariant());
                var stringToSign = $"{resource}\n{expiry}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Key));
                var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

                return $"SharedAccessSignature sr={resource}&sig={WebUtility.UrlEncode(signature)}&se={expiry}&skn={KeyName}";
            }
        }
    }
}
