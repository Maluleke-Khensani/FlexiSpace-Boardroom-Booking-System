using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Services;

namespace Flexispace.Mobile.ViewModels;

public partial class SettingsViewModel(IAuthService auth) : ObservableObject
{
    private bool _suppressThemeApply;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string rankLine = string.Empty;
    [ObservableProperty] private string homeBase = string.Empty;
    [ObservableProperty] private string appVersion = string.Empty;
    [ObservableProperty] private string buildLabel = "Demo · mock services";
    [ObservableProperty] private string themeCaption = "Light mode";

    [ObservableProperty] private bool bookingConfirmations;
    [ObservableProperty] private bool bookingReminders;
    [ObservableProperty] private bool cancellationAlerts;
    [ObservableProperty] private bool showAssistant;
    [ObservableProperty] private bool questPopups;
    [ObservableProperty] private bool isDarkMode;

    public bool IsLightMode => !IsDarkMode;

    [RelayCommand]
    private void Appearing()
    {
        var user = auth.CurrentUser;
        Name = user?.Name ?? "Guest";
        Email = user?.Email ?? "—";
        Role = user is null ? "—" : RolePermissions.DisplayName(user.Role);
        RankLine = user is null
            ? "Guest"
            : $"Level {RolePermissions.AccessLevel(user.Role)} · {RolePermissions.RankTitle(user.Role)}";
        HomeBase = string.IsNullOrWhiteSpace(user?.LocationId) ? "All locations" : user.LocationId;

        AppVersion = $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        BuildLabel = "Demo · mock services";

        _suppressThemeApply = true;
        BookingConfirmations = AppPreferences.BookingConfirmations;
        BookingReminders = AppPreferences.BookingReminders;
        CancellationAlerts = AppPreferences.CancellationAlerts;
        ShowAssistant = AppPreferences.ShowAssistant;
        QuestPopups = AppPreferences.QuestPopups;
        IsDarkMode = AppPreferences.IsDarkMode;
        _suppressThemeApply = false;

        ThemeCaption = IsDarkMode ? "Dark mode" : "Light mode";
        OnPropertyChanged(nameof(IsLightMode));
    }

    partial void OnBookingConfirmationsChanged(bool value) =>
        AppPreferences.BookingConfirmations = value;

    partial void OnBookingRemindersChanged(bool value) =>
        AppPreferences.BookingReminders = value;

    partial void OnCancellationAlertsChanged(bool value) =>
        AppPreferences.CancellationAlerts = value;

    partial void OnShowAssistantChanged(bool value)
    {
        AppPreferences.ShowAssistant = value;
#if WINDOWS
        Platforms.Windows.ChatbotWindowOverlay.SetVisible(value);
#endif
    }

    partial void OnQuestPopupsChanged(bool value) =>
        AppPreferences.QuestPopups = value;

    partial void OnIsDarkModeChanged(bool value)
    {
        ThemeCaption = value ? "Dark mode" : "Light mode";
        OnPropertyChanged(nameof(IsLightMode));
        if (_suppressThemeApply) return;
        AppThemeService.Apply(value);
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync() =>
        await Shell.Current.GoToAsync("PrivacyPage");

    [RelayCommand]
    private async Task OpenWebsiteAsync() =>
        await CompanyLinks.OpenAsync(CompanyLinks.Website);

    [RelayCommand]
    private async Task ContactSupportAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync(
                $"mailto:{PrivacyPolicyContent.ContactEmail}?subject=Flexispace%20mobile%20support");
        }
        catch
        {
            await ActionFeedback.FailAsync(PrivacyPolicyContent.ContactEmail, "Could not open mail");
        }
    }

    [RelayCommand]
    private async Task ResetPreferencesAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Reset preferences?",
            "Theme, notification, and experience toggles return to their defaults. Your account stays signed in.",
            "Reset",
            "Keep");
        if (!confirm) return;

        AppPreferences.Reset();
        Appearing();
        AppThemeService.ApplyFromPreferences();
#if WINDOWS
        Platforms.Windows.ChatbotWindowOverlay.SetVisible(AppPreferences.ShowAssistant);
#endif
        await ActionFeedback.SuccessAsync("Preferences restored to defaults.", "Settings reset");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Sign out?",
            "You’ll return to the welcome screen. Demo character select stays available after the next sign-in.",
            "Sign out",
            "Stay");
        if (!confirm) return;

        await auth.LogoutAsync();
        await ActionFeedback.InfoAsync("Signed out. See you next round.", "Session ended");
        await Shell.Current.GoToAsync("//WelcomePage");
    }
}
