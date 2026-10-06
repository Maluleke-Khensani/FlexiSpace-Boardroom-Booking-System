using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Always-on-screen popups for action results so users never have to scroll
/// to find a banner. Titles lean into the app's light quest / rank tone.
/// Quietly no-ops when Settings → Quest popups is off.
/// </summary>
public static class ActionFeedback
{
    public static Task SuccessAsync(string message, string title = "Quest complete") =>
        ShowAsync(title, message);

    public static Task FailAsync(string message, string title = "Not quite") =>
        ShowAsync(title, message);

    public static Task InfoAsync(string message, string title = "Heads up") =>
        ShowAsync(title, message);

    private static Task ShowAsync(string title, string message)
    {
        if (Shell.Current is null || !AppPreferences.QuestPopups)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
            await Shell.Current.DisplayAlertAsync(title, message, "Got it"));
    }
}
