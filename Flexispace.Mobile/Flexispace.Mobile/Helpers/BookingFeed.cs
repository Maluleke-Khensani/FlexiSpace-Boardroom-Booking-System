namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Tells open Mine and Calendar pages that a booking was saved on this device
/// so they reload without restarting the app.
/// </summary>
public static class BookingFeed
{
    public static event Action? Changed;

    public static void Publish()
    {
        var handler = Changed;
        if (handler is null)
            return;

        if (MainThread.IsMainThread)
            handler();
        else
            MainThread.BeginInvokeOnMainThread(() => handler());
    }
}
