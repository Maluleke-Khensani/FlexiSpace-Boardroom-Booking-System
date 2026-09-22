using Flexispace.Mobile.Helpers;
using Flexispace.Mobile.Models;
using Flexispace.Mobile.Services;
using Flexispace.Mobile.Views;

namespace Flexispace.Mobile;

public partial class AppShell : Shell
{
    private readonly INotificationService _notifications;
    private string? _lastTabRoute;

    public AppShell(IAuthService auth, INotificationService notifications)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(LocationDetailPage), typeof(LocationDetailPage));
        Routing.RegisterRoute(nameof(BookingDetailPage), typeof(BookingDetailPage));
        Routing.RegisterRoute(nameof(BookingConfirmationPage), typeof(BookingConfirmationPage));
        Routing.RegisterRoute(nameof(MyBookingsPage), typeof(MyBookingsPage));
        Routing.RegisterRoute(nameof(LocationsPage), typeof(LocationsPage));
        Routing.RegisterRoute(nameof(PrivacyPage), typeof(PrivacyPage));
        Routing.RegisterRoute(nameof(ReportsPage), typeof(ReportsPage));

        _notifications = notifications;
        BuildTabs(auth.CurrentUser?.Role);

        Navigated += OnNavigated;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _notifications.UnreadCountChanged += OnUnreadCountChanged;
    }

    /// <summary>
    /// Unread alerts shown as a badge on the Alerts tab. Bound from the notification
    /// service rather than a page ViewModel so the count stays correct on every tab.
    /// </summary>
    public int UnreadCount { get; private set; }

    public bool HasUnread => UnreadCount > 0;

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

        if (role is null || RolePermissions.CanViewReports(role.Value))
            tabBar.Items.Add(CreateTab("Reports", "ReportsPage", typeof(ReportsPage)));

        tabBar.Items.Add(CreateTab("Alerts", "NotificationsPage", typeof(NotificationsPage)));
        tabBar.Items.Add(CreateTab("Profile", "ProfilePage", typeof(ProfilePage)));

        Items.Add(tabBar);
    }

    private static ShellContent CreateTab(string title, string route, Type pageType) => new()
    {
        Title = title,
        Route = route,
        AutomationId = $"tab-{route}",
        Icon = TabIcons.Create(route, selected: false),
        // Resolve through DI (like the {DataTemplate} XAML markup extension normally does)
        // so pages still get their view models injected.
        ContentTemplate = new DataTemplate(() => IPlatformApplication.Current!.Services.GetRequiredService(pageType))
    };

    private async void OnLoaded(object? sender, EventArgs e)
    {
        TabBarChrome.SyncIcons(this);
        TabBarChrome.ApplyChrome(this);
        await RefreshUnreadAsync();
        // Native tab items are created a tick after Shell.Loaded — re-apply chrome once they exist.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () =>
        {
            TabBarChrome.SyncIcons(this);
            TabBarChrome.ApplyChrome(this);
            TabBarChrome.UpdateBadge(this, UnreadCount);
        });
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        Navigated -= OnNavigated;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _notifications.UnreadCountChanged -= OnUnreadCountChanged;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        TabBarChrome.SyncIcons(this);
        TabBarChrome.ApplyChrome(this);

        var route = TabBarChrome.CurrentTabRoute(this);
        if (!string.IsNullOrEmpty(route) && route != _lastTabRoute)
        {
            var isFirstPaint = _lastTabRoute is null;
            _lastTabRoute = route;
            if (!isFirstPaint)
                TabBarChrome.BounceSelected(this);
        }
    }

    private async void OnUnreadCountChanged(int unread)
    {
        UnreadCount = unread;
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(HasUnread));
        await MainThread.InvokeOnMainThreadAsync(() => TabBarChrome.UpdateBadge(this, unread));
    }

    private async Task RefreshUnreadAsync()
    {
        UnreadCount = await _notifications.GetUnreadCountAsync();
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(HasUnread));
        TabBarChrome.UpdateBadge(this, UnreadCount);
    }
}
