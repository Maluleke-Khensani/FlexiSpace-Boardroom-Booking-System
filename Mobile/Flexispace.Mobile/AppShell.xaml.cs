using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;
using Flexispace.Mobile.Views;

namespace Flexispace.Mobile;

public partial class AppShell : Shell
{
    public AppShell(IAuthService auth)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(LocationDetailPage), typeof(LocationDetailPage));
        Routing.RegisterRoute(nameof(BookingDetailPage), typeof(BookingDetailPage));
        Routing.RegisterRoute(nameof(BookingConfirmationPage), typeof(BookingConfirmationPage));
        Routing.RegisterRoute(nameof(MyBookingsPage), typeof(MyBookingsPage));
        Routing.RegisterRoute(nameof(LocationsPage), typeof(LocationsPage));

        BuildTabs(auth.CurrentUser?.Role);
    }

    /// <summary>
    /// Builds the tab bar once, at construction, for the role that's signed in right now.
    /// A tab a role can't use is never added — there's nothing to disable or hide later.
    /// Role changes swap in a whole new AppShell (via ShellReloader) rather than mutating
    /// this one, because toggling ShellContent.IsVisible on a live Windows tab bar crashes.
    /// </summary>
    private void BuildTabs(UserRole? role)
    {
        var tabBar = new TabBar { Route = "MainTabs" };

        // Every signed-in role can see Home, book a room, get notified, and manage their profile.
        tabBar.Items.Add(CreateTab("Home", "HomePage", typeof(HomePage)));
        tabBar.Items.Add(CreateTab("Book", "BookingPage", typeof(BookingPage)));

        if (role is null || RolePermissions.CanViewAvailability(role.Value))
            tabBar.Items.Add(CreateTab("Live", "AvailabilityPage", typeof(AvailabilityPage)));

        if (role is null || RolePermissions.CanAccessManageHub(role.Value))
            tabBar.Items.Add(CreateTab("Manage", "ManagePage", typeof(ManagePage)));

        tabBar.Items.Add(CreateTab("Alerts", "NotificationsPage", typeof(NotificationsPage)));
        tabBar.Items.Add(CreateTab("Profile", "ProfilePage", typeof(ProfilePage)));

        Items.Add(tabBar);
    }

    private static ShellContent CreateTab(string title, string route, Type pageType) => new()
    {
        Title = title,
        Route = route,
        Icon = "dotnet_bot.png",
        // Resolve through DI (like the {DataTemplate} XAML markup extension normally does)
        // so pages still get their view models injected.
        ContentTemplate = new DataTemplate(() => IPlatformApplication.Current!.Services.GetRequiredService(pageType))
    };
}
