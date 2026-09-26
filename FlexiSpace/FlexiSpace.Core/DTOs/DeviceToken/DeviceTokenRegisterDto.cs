namespace FlexiSpace.Core.DTOs.DeviceToken
{
    // Sent by the mobile app once it has an FCM registration token -
    // typically on app start and again whenever Firebase issues a new
    // token (tokens can rotate, e.g. after a reinstall).
    public class DeviceTokenRegisterDto
    {
        public required string Token { get; set; }

        // "android" or "ios".
        public required string Platform { get; set; }
    }
}
