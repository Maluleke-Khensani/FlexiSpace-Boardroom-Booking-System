namespace Flexispace.Mobile.Services;

/// <summary>
/// Local preference keys for the Settings page. Survives app restarts via
/// <see cref="Preferences"/>; reset only clears Flexispace settings keys.
/// </summary>
public static class AppPreferences
{
    private const string Prefix = "flexi.settings.";

    public static bool BookingConfirmations
    {
        get => Preferences.Default.Get(Prefix + "booking_confirmations", true);
        set => Preferences.Default.Set(Prefix + "booking_confirmations", value);
    }

    public static bool BookingReminders
    {
        get => Preferences.Default.Get(Prefix + "booking_reminders", true);
        set => Preferences.Default.Set(Prefix + "booking_reminders", value);
    }

    public static bool CancellationAlerts
    {
        get => Preferences.Default.Get(Prefix + "cancellation_alerts", true);
        set => Preferences.Default.Set(Prefix + "cancellation_alerts", value);
    }

    public static bool ShowAssistant
    {
        get => Preferences.Default.Get(Prefix + "show_assistant", true);
        set => Preferences.Default.Set(Prefix + "show_assistant", value);
    }

    public static bool QuestPopups
    {
        get => Preferences.Default.Get(Prefix + "quest_popups", true);
        set => Preferences.Default.Set(Prefix + "quest_popups", value);
    }

    public static bool IsDarkMode
    {
        get => Preferences.Default.Get(Prefix + "dark_mode", false);
        set => Preferences.Default.Set(Prefix + "dark_mode", value);
    }

    public static void Reset()
    {
        Preferences.Default.Remove(Prefix + "booking_confirmations");
        Preferences.Default.Remove(Prefix + "booking_reminders");
        Preferences.Default.Remove(Prefix + "cancellation_alerts");
        Preferences.Default.Remove(Prefix + "show_assistant");
        Preferences.Default.Remove(Prefix + "quest_popups");
        Preferences.Default.Remove(Prefix + "dark_mode");
    }
}
