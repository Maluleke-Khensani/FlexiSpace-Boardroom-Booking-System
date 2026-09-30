namespace FlexiSpace.Core.Services
{
    // Abstraction over "push this to whatever devices this user has
    // registered". Deliberately separate from INotificationService - the
    // in-app notification inbox must keep working the same regardless of
    // whether push is configured; if a user has no registered device (a
    // web-only session, or a mobile user who hasn't opened the app since
    // installing), sending simply does nothing rather than failing.
    public interface IPushNotificationSender
    {
        Task SendAsync(int userId, string title, string body);
    }
}
