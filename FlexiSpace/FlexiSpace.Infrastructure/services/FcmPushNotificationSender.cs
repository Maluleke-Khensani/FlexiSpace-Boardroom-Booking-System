using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiSpace.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    // Sends push notifications via Firebase Cloud Messaging's HTTP v1 API,
    // which covers both Android and iOS from a single call - no separate
    // APNs integration needed.
    //
    // Requires a Firebase project with Cloud Messaging enabled and a
    // service account key (Firebase console -> Project settings -> Service
    // accounts -> Generate new private key). Configure:
    //   Firebase:ProjectId          - the Firebase project id
    //   Firebase:ServiceAccountJson - the full contents of that key file
    // via user secrets (or an env var / Key Vault in production) - never
    // in appsettings.json. Until both are set, this logs a warning and
    // does nothing rather than throwing, so booking flows never fail just
    // because push isn't configured yet.
    public class FcmPushNotificationSender : IPushNotificationSender
    {
        private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FcmPushNotificationSender> _logger;

        // A fresh OAuth2 access token is requested from Google for every
        // new instance of this class (it's registered per-request/scoped
        // alongside everything else) - fine for this project's traffic
        // level; if push volume grows, this cache should move to a
        // singleton-scoped token cache instead of re-authenticating on
        // every notification.
        private string? _cachedAccessToken;
        private DateTime _cachedAccessTokenExpiresAt = DateTime.MinValue;

        public FcmPushNotificationSender(
            IDeviceTokenService deviceTokenService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<FcmPushNotificationSender> logger)
        {
            _deviceTokenService = deviceTokenService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendAsync(int userId, string title, string body)
        {
            var projectId = _configuration["Firebase:ProjectId"];
            var serviceAccountJson = _configuration["Firebase:ServiceAccountJson"];

            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                _logger.LogWarning(
                    "Push notification skipped for user {UserId} - Firebase is not configured yet.",
                    userId);
                return;
            }

            var tokens = await _deviceTokenService.GetTokensForUserAsync(userId);

            if (tokens.Count == 0)
            {
                // Normal for a web-only session, or a mobile user who
                // hasn't opened the app since installing - not an error.
                return;
            }

            try
            {
                var accessToken = await GetAccessTokenAsync(serviceAccountJson);

                var client = _httpClientFactory.CreateClient(nameof(FcmPushNotificationSender));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                foreach (var token in tokens)
                {
                    var payload = new
                    {
                        message = new
                        {
                            token,
                            notification = new { title, body }
                        }
                    };

                    var response = await client.PostAsync(
                        $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send",
                        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();

                        // A single stale/uninstalled-app token failing
                        // shouldn't stop the rest of this user's devices
                        // from getting the push - log and continue.
                        _logger.LogWarning(
                            "FCM push failed for user {UserId} ({StatusCode}): {Error}",
                            userId, response.StatusCode, error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Push failing must never take down the booking flow that
                // triggered it.
                _logger.LogError(ex, "Push notification failed for user {UserId}.", userId);
            }
        }

        // Exchanges the service account key for a short-lived OAuth2
        // access token via Google's standard server-to-server JWT flow,
        // cached until shortly before it expires.
        private async Task<string> GetAccessTokenAsync(string serviceAccountJson)
        {
            if (_cachedAccessToken != null && DateTime.UtcNow < _cachedAccessTokenExpiresAt)
            {
                return _cachedAccessToken;
            }

            using var doc = JsonDocument.Parse(serviceAccountJson);
            var root = doc.RootElement;
            var clientEmail = root.GetProperty("client_email").GetString()!;
            var privateKeyPem = root.GetProperty("private_key").GetString()!;

            var now = DateTimeOffset.UtcNow;

            var header = new { alg = "RS256", typ = "JWT" };
            var claims = new
            {
                iss = clientEmail,
                scope = FcmScope,
                aud = TokenEndpoint,
                iat = now.ToUnixTimeSeconds(),
                exp = now.AddMinutes(55).ToUnixTimeSeconds()
            };

            var headerSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
            var claimsSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
            var unsigned = $"{headerSegment}.{claimsSegment}";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var signature = rsa.SignData(
                Encoding.UTF8.GetBytes(unsigned),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var jwt = $"{unsigned}.{Base64UrlEncode(signature)}";

            var client = _httpClientFactory.CreateClient(nameof(FcmPushNotificationSender) + ".Token");

            var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", jwt)
            }));

            response.EnsureSuccessStatusCode();

            using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var accessToken = responseDoc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = responseDoc.RootElement.GetProperty("expires_in").GetInt32();

            _cachedAccessToken = accessToken;
            _cachedAccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            return accessToken;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
