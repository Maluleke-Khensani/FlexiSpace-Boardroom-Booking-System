using Flexispace.Mobile.Services;

namespace Flexispace.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        AppThemeService.ApplyFromPreferences();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        AppThemeService.ApplyFromPreferences();

#if WINDOWS
        // Phone-sized desktop preview only. Android/iOS ignore Width/Height and
        // fill the device screen; this #if keeps the pin off those builds entirely.
        window.Width = 400;
        window.Height = 860;
        window.MinimumWidth = 360;
        window.MinimumHeight = 640;

        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                Platforms.Windows.ChatbotWindowOverlay.TryAttach(native, window);
        };
#endif

        return window;
    }
}
