namespace Flexispace.Mobile.Services;

/// <summary>
/// Applies light/dark surface colours to themeable DynamicResource keys and
/// sets <see cref="Application.UserAppTheme"/>. Brand gold / charcoal / ivory
/// hero tokens stay fixed so dark marketing screens keep readable accents.
/// </summary>
public static class AppThemeService
{
    private static readonly Dictionary<string, Color> Light =
        new(StringComparer.Ordinal)
        {
            ["BrandPage"] = Color.FromArgb("#FAF8F4"),
            ["BrandSurface"] = Color.FromArgb("#FFFFFF"),
            ["BrandSurfaceAlt"] = Color.FromArgb("#F3F1EC"),
            ["BrandCard"] = Color.FromArgb("#FFFFFF"),
            ["BrandCardAlt"] = Color.FromArgb("#F7F4EE"),
            ["BrandInk"] = Color.FromArgb("#1A1A1A"),
            ["BrandMuted"] = Color.FromArgb("#5A554D"),
            ["BrandLine"] = Color.FromArgb("#E4DFD6"),
            ["BrandGoldDeep"] = Color.FromArgb("#9A7A1F"),
            ["BrandTealSoft"] = Color.FromArgb("#E4F0F1"),
            ["BrandSuccessSoft"] = Color.FromArgb("#E3F3EA"),
            ["BrandDangerSoft"] = Color.FromArgb("#FBEAE7"),
            ["BrandSteelSoft"] = Color.FromArgb("#E7ECF1"),
            ["BrandClaySoft"] = Color.FromArgb("#F7E9DE"),
            ["BrandGoldWash"] = Color.FromArgb("#FFF4D6"),
            ["BrandPanel"] = Color.FromArgb("#FFFFFF"),
            ["BrandTeal"] = Color.FromArgb("#1F6F78"),
            ["BrandSuccess"] = Color.FromArgb("#1E8E5A"),
            ["BrandSteel"] = Color.FromArgb("#3D5A73"),
            ["BrandClay"] = Color.FromArgb("#B5622E"),
            ["BrandDanger"] = Color.FromArgb("#C1392B"),
        };

    private static readonly Dictionary<string, Color> Dark =
        new(StringComparer.Ordinal)
        {
            ["BrandPage"] = Color.FromArgb("#121212"),
            ["BrandSurface"] = Color.FromArgb("#1C1C1C"),
            ["BrandSurfaceAlt"] = Color.FromArgb("#242424"),
            ["BrandCard"] = Color.FromArgb("#1C1C1C"),
            ["BrandCardAlt"] = Color.FromArgb("#242424"),
            ["BrandInk"] = Color.FromArgb("#F3F0E8"),
            ["BrandMuted"] = Color.FromArgb("#B0AAA0"),
            ["BrandLine"] = Color.FromArgb("#33302B"),
            ["BrandGoldDeep"] = Color.FromArgb("#D4B45A"),
            ["BrandTealSoft"] = Color.FromArgb("#163035"),
            ["BrandSuccessSoft"] = Color.FromArgb("#152820"),
            ["BrandDangerSoft"] = Color.FromArgb("#2A1816"),
            ["BrandSteelSoft"] = Color.FromArgb("#1A222A"),
            ["BrandClaySoft"] = Color.FromArgb("#2A1E16"),
            ["BrandGoldWash"] = Color.FromArgb("#2A2414"),
            ["BrandPanel"] = Color.FromArgb("#1C1C1C"),
            // Brighter accents so icons/labels stay clear on dark washes
            ["BrandTeal"] = Color.FromArgb("#4CB8C4"),
            ["BrandSuccess"] = Color.FromArgb("#3CB87A"),
            ["BrandSteel"] = Color.FromArgb("#8AA6C1"),
            ["BrandClay"] = Color.FromArgb("#D4844A"),
            ["BrandDanger"] = Color.FromArgb("#E05A4C"),
        };

    public static bool IsDark => AppPreferences.IsDarkMode;

    public static void ApplyFromPreferences() =>
        Apply(AppPreferences.IsDarkMode);

    public static void Apply(bool dark)
    {
        AppPreferences.IsDarkMode = dark;

        var app = Application.Current;
        if (app is null) return;

        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;

        var palette = dark ? Dark : Light;
        foreach (var (key, color) in palette)
            app.Resources[key] = color;

        // Keep shell / page chrome in sync when theme flips mid-session.
        if (app.Windows.Count > 0 && app.Windows[0].Page is Shell shell)
        {
            shell.BackgroundColor = palette["BrandPage"];
            shell.FlyoutBackgroundColor = palette["BrandPage"];
            Shell.SetBackgroundColor(shell, palette["BrandPage"]);
            Shell.SetForegroundColor(shell, palette["BrandInk"]);
            Shell.SetTitleColor(shell, palette["BrandInk"]);
            Shell.SetTabBarBackgroundColor(shell, palette["BrandSurface"]);
            Shell.SetTabBarForegroundColor(shell, palette["BrandInk"]);
            Shell.SetTabBarUnselectedColor(shell, palette["BrandMuted"]);
            Shell.SetTabBarTitleColor(shell, palette["BrandGoldDeep"]);

            Helpers.TabBarChrome.SyncIcons(shell);
            Helpers.TabBarChrome.ApplyChrome(shell);
        }
    }
}
