namespace FlexiSpace.Core.Services
{
    public interface IDeviceTokenService
    {
        // Registers a device token for a user, or - if that exact token
        // already exists (same device re-registering, e.g. on app start) -
        // just refreshes LastSeenAt and re-points it at userId in case a
        // different user has since signed into the same device.
        Task RegisterTokenAsync(int userId, string token, string platform);

        // Removes a token - call on sign-out so a shared/handed-down
        // device stops receiving the previous user's pushes.
        Task<bool> RemoveTokenAsync(string token);

        Task<IReadOnlyList<string>> GetTokensForUserAsync(int userId);

        // Token + platform ("android" / "ios"), so the push sender can
        // format each notification for the right service.
        Task<IReadOnlyList<RegisteredDevice>> GetDevicesForUserAsync(int userId);
    }

    public sealed record RegisteredDevice(string Token, string Platform);
}
