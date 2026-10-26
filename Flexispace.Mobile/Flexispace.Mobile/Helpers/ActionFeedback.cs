using Flexispace.Mobile.Controls;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Branded cards shown over the current page. Problems always appear.
/// Success and tip cards follow Settings → Quest popups.
/// </summary>
public static class ActionFeedback
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static Task SuccessAsync(string message, string title = "Quest complete") =>
        ShowAsync(new FeedbackPrompt
        {
            Title = title,
            Message = message,
            Tone = FeedbackTone.Success
        }, always: false);

    public static Task FailAsync(string? message, string title = "Please check") =>
        ShowAsync(new FeedbackPrompt
        {
            Title = title,
            Message = message ?? string.Empty,
            Tone = FeedbackTone.Fail
        }, always: true);

    public static Task InfoAsync(string message, string title = "Heads up") =>
        ShowAsync(new FeedbackPrompt
        {
            Title = title,
            Message = message,
            Tone = FeedbackTone.Info
        }, always: false);

    /// <summary>A notice the user asked to read. Not gated by Quest popups.</summary>
    public static Task MessageAsync(string message, string title = "Heads up") =>
        ShowAsync(new FeedbackPrompt
        {
            Title = title,
            Message = message,
            Tone = FeedbackTone.Info
        }, always: true);

    public static Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel,
        bool destructive = false) =>
        ShowAsync(new FeedbackPrompt
        {
            Title = title,
            Message = message,
            Tone = FeedbackTone.Confirm,
            AcceptText = accept,
            CancelText = cancel,
            Destructive = destructive
        }, always: true);

    private static async Task<bool> ShowAsync(FeedbackPrompt prompt, bool always)
    {
        if (string.IsNullOrWhiteSpace(prompt.Message) && string.IsNullOrWhiteSpace(prompt.Title))
            return false;
        if (!always && !AppPreferences.QuestPopups)
            return false;

        await Gate.WaitAsync();
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(() => PresentOnPageAsync(prompt));
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<bool> PresentOnPageAsync(FeedbackPrompt prompt)
    {
        var shell = Shell.Current;
        if (shell is null)
            return false;

        // Shown as its own page. Replacing the current page's Content on Windows
        // destroys the native view and throws COMException ("No installed components").
        var overlay = new FeedbackOverlay();
        var modal = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#CC111111"),
            Content = overlay
        };
        Shell.SetNavBarIsVisible(modal, false);
        Shell.SetTabBarIsVisible(modal, false);

        try
        {
            await shell.Navigation.PushModalAsync(modal, animated: false);
        }
        catch (Exception)
        {
            return await FallbackAlertAsync(shell, prompt);
        }

        try
        {
            return await overlay.PresentAsync(prompt);
        }
        finally
        {
            try
            {
                if (shell.Navigation.ModalStack.LastOrDefault() == modal)
                    await shell.Navigation.PopModalAsync(animated: false);
            }
            catch (Exception)
            {
                // The shell may already have been replaced, for example after sign-in.
            }
        }
    }

    private static async Task<bool> FallbackAlertAsync(Shell shell, FeedbackPrompt prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt.CancelText))
        {
            await shell.DisplayAlertAsync(prompt.Title, prompt.Message, prompt.AcceptText);
            return true;
        }

        return await shell.DisplayAlertAsync(prompt.Title, prompt.Message, prompt.AcceptText, prompt.CancelText);
    }
}
