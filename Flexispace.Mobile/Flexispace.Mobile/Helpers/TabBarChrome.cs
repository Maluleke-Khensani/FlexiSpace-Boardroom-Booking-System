#if WINDOWS
using Flexispace.Mobile.Platforms.Windows;
#elif ANDROID
using Flexispace.Mobile.Platforms.Android;
#elif IOS
using Flexispace.Mobile.Platforms.iOS;
#endif

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Cross-platform tab chrome: swap filled/outline glyphs, then ask the platform
/// handler to paint the gold icon pill, bounce the active icon, and refresh the badge.
/// </summary>
public static class TabBarChrome
{
    public static void SyncIcons(Shell shell)
    {
        var selectedRoute = CurrentTabRoute(shell);

        foreach (var tabBar in shell.Items.OfType<TabBar>())
        {
            foreach (var content in EnumerateContents(tabBar))
            {
                var selected = string.Equals(content.Route, selectedRoute, StringComparison.Ordinal);
                if (content.Icon is FontImageSource image)
                    TabIcons.Apply(image, content.Route, selected);
                else
                    content.Icon = TabIcons.Create(content.Route, selected);
            }
        }
    }

    public static void ApplyChrome(Shell shell)
    {
#if WINDOWS || ANDROID || IOS
        TabBarHandlerFix.ApplyChrome(shell);
#endif
    }

    public static void BounceSelected(Shell shell)
    {
#if WINDOWS || ANDROID || IOS
        TabBarHandlerFix.BounceSelected(shell);
#endif
    }

    public static void UpdateBadge(Shell shell, int unread)
    {
#if WINDOWS || ANDROID || IOS
        TabBarHandlerFix.UpdateBadge(shell, unread);
#endif
    }

    public static string? CurrentTabRoute(Shell shell) =>
        shell.CurrentItem?.CurrentItem?.CurrentItem?.Route
        ?? shell.CurrentItem?.CurrentItem?.Route
        ?? shell.CurrentItem?.Route;

    private static IEnumerable<ShellContent> EnumerateContents(TabBar tabBar)
    {
        foreach (var section in tabBar.Items)
        {
            foreach (var content in section.Items)
                yield return content;
        }
    }
}
