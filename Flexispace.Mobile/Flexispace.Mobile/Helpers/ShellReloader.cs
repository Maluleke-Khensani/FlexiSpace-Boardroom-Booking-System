using Flexispace.Mobile;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.Helpers;

/// <summary>
/// Swaps in a brand-new <see cref="AppShell"/> whenever the signed-in role changes (login,
/// switch demo user), so the tab bar is built fresh with only the tabs that role can use.
/// We deliberately never mutate tabs on a live Shell — toggling ShellContent.IsVisible
/// after the tab bar has rendered crashes on Windows/WinUI — so a role change always gets
/// an entirely new Shell instance instead.
/// </summary>
public static class ShellReloader
{
    public static async Task ReloadAsync(string route = "//HomePage")
    {
        var services = IPlatformApplication.Current!.Services;
        var shell = services.GetRequiredService<AppShell>();

        Application.Current!.Windows[0].Page = shell;
        AppThemeService.ApplyFromPreferences();

        // Navigate on the new Shell instance directly rather than via the Shell.Current
        // static (which may not repoint to the new instance until the next UI tick).
        await shell.GoToAsync(route);
    }
}
