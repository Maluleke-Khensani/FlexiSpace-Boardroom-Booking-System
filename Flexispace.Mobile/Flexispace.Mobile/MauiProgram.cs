using Flexispace.Mobile.Services;
using Flexispace.Mobile.Services.Mock;
using Flexispace.Mobile.ViewModels;
using Flexispace.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace Flexispace.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if WINDOWS
        Flexispace.Mobile.Platforms.Windows.TabBarHandlerFix.AppendMapping();
#elif ANDROID
        Flexispace.Mobile.Platforms.Android.TabBarHandlerFix.AppendMapping();
#elif IOS
        Flexispace.Mobile.Platforms.iOS.TabBarHandlerFix.AppendMapping();
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Transient: a fresh Shell (with tabs matching the current role) is built on every
        // login/role switch instead of mutating one long-lived instance — see ShellReloader.
        builder.Services.AddTransient<AppShell>();

        builder.Services.AddSingleton<MockDataStore>();
        builder.Services.AddSingleton<IAuthService, MockAuthService>();
        builder.Services.AddSingleton<INotificationService, MockNotificationService>();
        builder.Services.AddSingleton<IRoomService, MockRoomService>();
        builder.Services.AddSingleton<IBookingService, MockBookingService>();
        builder.Services.AddSingleton<IAdminService, MockAdminService>();

        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<AvailabilityViewModel>();
        builder.Services.AddTransient<BookingViewModel>();
        builder.Services.AddTransient<MyBookingsViewModel>();
        builder.Services.AddTransient<BookingDetailViewModel>();
        builder.Services.AddTransient<BookingConfirmationViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<LocationDetailViewModel>();
        builder.Services.AddTransient<LocationsViewModel>();
        builder.Services.AddTransient<ManageViewModel>();
        builder.Services.AddTransient<PrivacyViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();

        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<AvailabilityPage>();
        builder.Services.AddTransient<BookingPage>();
        builder.Services.AddTransient<MyBookingsPage>();
        builder.Services.AddTransient<BookingDetailPage>();
        builder.Services.AddTransient<BookingConfirmationPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<LocationDetailPage>();
        builder.Services.AddTransient<LocationsPage>();
        builder.Services.AddTransient<ManagePage>();
        builder.Services.AddTransient<PrivacyPage>();
        builder.Services.AddTransient<ReportsPage>();

        return builder.Build();
    }
}
