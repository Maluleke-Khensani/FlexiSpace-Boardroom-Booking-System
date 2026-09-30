namespace FlexiSpace.Core.Entities
{
    // One row per device a user has signed into the mobile app on. A user
    // can have more than one (phone + tablet), which is why this is its
    // own table rather than a single column on User - and why push
    // notifications are mobile-only "for free": a web session never
    // registers a token here, so it never receives one.
    public class DeviceToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        // The FCM registration token for this device/app install.
        public required string Token { get; set; }

        // "android" or "ios" - informational only; FCM's HTTP v1 API
        // accepts the same request shape for both, so nothing branches on
        // this today, but it's useful for support/debugging.
        public required string Platform { get; set; }

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        // Bumped every time the app re-registers the same token (e.g. on
        // each app start), so stale tokens from uninstalled apps can be
        // identified and cleaned up later if needed.
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
