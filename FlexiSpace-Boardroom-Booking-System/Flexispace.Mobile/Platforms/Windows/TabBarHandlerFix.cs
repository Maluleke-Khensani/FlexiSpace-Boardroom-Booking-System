using Microsoft.Maui.Controls.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Numerics;
using WinColor = Windows.UI.Color;
using WinBorder = Microsoft.UI.Xaml.Controls.Border;
using WinGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinCorner = Microsoft.UI.Xaml.CornerRadius;
using WinAlignH = Microsoft.UI.Xaml.HorizontalAlignment;
using WinAlignV = Microsoft.UI.Xaml.VerticalAlignment;
using WinThickness = Microsoft.UI.Xaml.Thickness;
using WinVisibility = Microsoft.UI.Xaml.Visibility;
using WinPoint = Windows.Foundation.Point;

namespace Flexispace.Mobile.Platforms.Windows;

/// <summary>
/// WinUI Shell tab chrome: hide the default underline, paint a gold pill behind the
/// selected icon, lift the bar with a shadow, bounce the active icon, and pin an
/// unread badge on Alerts.
/// </summary>
public static class TabBarHandlerFix
{
    private const string PillName = "FlexispaceIconPill";
    private const string BadgeName = "FlexispaceAlertsBadge";
    private static int _lastUnread;

    public static void AppendMapping()
    {
        EnsureTabBarResources();

        ShellHandler.Mapper.AppendToMapping("FlexispaceTabBarChrome", (handler, _) =>
        {
            if (handler.PlatformView is not FrameworkElement root)
                return;

            void OnLoaded(object sender, RoutedEventArgs e)
            {
                root.Loaded -= OnLoaded;
                ApplyToRoot(root);
            }

            if (root.IsLoaded)
                ApplyToRoot(root);
            else
                root.Loaded += OnLoaded;
        });
    }

    public static void ApplyChrome(Shell shell)
    {
        if (GetRoot(shell) is FrameworkElement root)
            ApplyToRoot(root);
    }

    public static void BounceSelected(Shell shell)
    {
        if (GetRoot(shell) is FrameworkElement root)
            BounceSelected(root);
    }

    public static void UpdateBadge(Shell shell, int unread)
    {
        _lastUnread = unread;
        if (GetRoot(shell) is FrameworkElement root)
            UpdateAlertsBadge(root, unread);
    }

    private static FrameworkElement? GetRoot(Shell shell) =>
        shell.Handler?.PlatformView as FrameworkElement;

    private static void ApplyToRoot(FrameworkElement root)
    {
        WireNavigationView(root);

        foreach (var item in FindDescendants<NavigationViewItem>(root))
            ApplyIconPill(item);

        if (FindByName(root, "TopNavArea") is FrameworkElement topNav)
            StyleTabStrip(topNav);
        else if (FindByName(root, "TopNavGrid") is FrameworkElement topGrid)
            StyleTabStrip(topGrid);

        UpdateAlertsBadge(root, _lastUnread);
    }

    private static void WireNavigationView(FrameworkElement root)
    {
        foreach (var nav in FindDescendants<NavigationView>(root))
        {
            nav.SelectionChanged -= OnNavigationSelectionChanged;
            nav.SelectionChanged += OnNavigationSelectionChanged;
        }
    }

    private static void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        foreach (var item in FindDescendants<NavigationViewItem>(sender))
            ApplyIconPill(item);
    }

    private static void StyleTabStrip(FrameworkElement strip)
    {
        // Windows Shell tabs sit at the top — round the edge that meets the page
        // and lift it so it reads as a bar, not a flush strip.
        switch (strip)
        {
            case Control control:
                control.CornerRadius = new WinCorner(0, 0, 16, 16);
                break;
            case WinGrid grid:
                grid.CornerRadius = new WinCorner(0, 0, 16, 16);
                break;
            case WinBorder border:
                border.CornerRadius = new WinCorner(0, 0, 16, 16);
                break;
        }

        try
        {
            strip.Shadow = new ThemeShadow();
            strip.Translation = new Vector3(0, 0, 16);
        }
        catch
        {
            // ThemeShadow is not available in every WinUI host.
        }
    }

    private static void ApplyIconPill(NavigationViewItem item)
    {
        var icon = FindDescendants<FontIcon>(item).FirstOrDefault() as FrameworkElement
                   ?? FindDescendants<AnimatedIcon>(item).FirstOrDefault() as FrameworkElement
                   ?? FindDescendants<Microsoft.UI.Xaml.Controls.Image>(item).FirstOrDefault();

        if (icon is null)
            return;

        var target = icon.Parent is Viewbox viewbox ? viewbox : icon;
        if (target.Parent is not Panel host)
            return;

        var pill = host.Children.OfType<WinBorder>().FirstOrDefault(b => b.Name == PillName);
        if (pill is null)
        {
            pill = new WinBorder
            {
                Name = PillName,
                Width = 44,
                Height = 32,
                CornerRadius = new WinCorner(20),
                HorizontalAlignment = WinAlignH.Center,
                VerticalAlignment = WinAlignV.Center,
                IsHitTestVisible = false
            };
            host.Children.Insert(0, pill);
            WinGrid.SetColumn(pill, WinGrid.GetColumn(target));
            WinGrid.SetRow(pill, WinGrid.GetRow(target));
        }

        pill.Background = item.IsSelected
            ? new WinBrush(WinColor.FromArgb(255, 0xC4, 0xA0, 0x35))
            : new WinBrush(Microsoft.UI.Colors.Transparent);
    }

    private static void BounceSelected(FrameworkElement root)
    {
        var selected = FindDescendants<NavigationViewItem>(root).FirstOrDefault(i => i.IsSelected);
        if (selected is null)
            return;

        var icon = FindDescendants<FontIcon>(selected).FirstOrDefault() as FrameworkElement
                   ?? FindDescendants<AnimatedIcon>(selected).FirstOrDefault() as FrameworkElement
                   ?? FindDescendants<Microsoft.UI.Xaml.Controls.Image>(selected).FirstOrDefault()
                   ?? (FrameworkElement)selected;

        var target = icon.Parent is Viewbox viewbox ? viewbox : icon;
        target.RenderTransformOrigin = new WinPoint(0.5, 0.5);
        target.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        var transform = (ScaleTransform)target.RenderTransform;

        var story = new Storyboard();
        var ax = CreateBounceAnimation();
        var ay = CreateBounceAnimation();
        Storyboard.SetTarget(ax, transform);
        Storyboard.SetTargetProperty(ax, "ScaleX");
        Storyboard.SetTarget(ay, transform);
        Storyboard.SetTargetProperty(ay, "ScaleY");
        story.Children.Add(ax);
        story.Children.Add(ay);
        story.Begin();
    }

    private static DoubleAnimation CreateBounceAnimation() => new()
    {
        From = 1.0,
        To = 1.15,
        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
        AutoReverse = true,
        EasingFunction = new ElasticEase
        {
            Oscillations = 1,
            Springiness = 6,
            EasingMode = EasingMode.EaseOut
        }
    };

    private static void UpdateAlertsBadge(FrameworkElement root, int unread)
    {
        foreach (var item in FindDescendants<NavigationViewItem>(root))
        {
            var title = item.Content?.ToString() ?? string.Empty;
            if (!title.Contains("Alert", StringComparison.OrdinalIgnoreCase))
                continue;

            var icon = FindDescendants<FontIcon>(item).FirstOrDefault() as FrameworkElement
                       ?? FindDescendants<AnimatedIcon>(item).FirstOrDefault() as FrameworkElement
                       ?? FindDescendants<Microsoft.UI.Xaml.Controls.Image>(item).FirstOrDefault();
            if (icon?.Parent is not Panel host)
                continue;

            var badge = host.Children.OfType<WinBorder>().FirstOrDefault(b => b.Name == BadgeName);
            if (unread <= 0)
            {
                if (badge is not null)
                    badge.Visibility = WinVisibility.Collapsed;
                continue;
            }

            if (badge is null)
            {
                var iconTarget = icon.Parent is Viewbox vb ? vb : icon;
                badge = new WinBorder
                {
                    Name = BadgeName,
                    Width = 18,
                    Height = 18,
                    CornerRadius = new WinCorner(9),
                    Background = new WinBrush(WinColor.FromArgb(255, 0x9A, 0x7A, 0x1F)),
                    HorizontalAlignment = WinAlignH.Right,
                    VerticalAlignment = WinAlignV.Top,
                    Margin = new WinThickness(0, -4, -6, 0),
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Foreground = new WinBrush(Microsoft.UI.Colors.White),
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        HorizontalAlignment = WinAlignH.Center,
                        VerticalAlignment = WinAlignV.Center
                    }
                };
                host.Children.Add(badge);
                WinGrid.SetColumn(badge, WinGrid.GetColumn(iconTarget));
                WinGrid.SetRow(badge, WinGrid.GetRow(iconTarget));
            }

            badge.Visibility = WinVisibility.Visible;
            if (badge.Child is TextBlock label)
                label.Text = unread > 9 ? "9+" : unread.ToString();
        }
    }

    private static void EnsureTabBarResources()
    {
        var resources = Microsoft.UI.Xaml.Application.Current?.Resources;
        if (resources is null)
            return;

        var transparent = new WinBrush(Microsoft.UI.Colors.Transparent);
        resources["NavigationViewSelectionIndicatorForeground"] = transparent;
        resources["TopNavigationViewItemBackgroundSelected"] = transparent;
        resources["TopNavigationViewItemBackgroundSelectedPointerOver"] = new WinBrush(WinColor.FromArgb(0x33, 0xC4, 0xA0, 0x35));
        resources["TopNavigationViewItemBackgroundSelectedPressed"] = new WinBrush(WinColor.FromArgb(0x55, 0xC4, 0xA0, 0x35));
        resources["NavigationViewItemBackgroundSelected"] = transparent;
        resources["NavigationViewItemBackgroundSelectedPointerOver"] = new WinBrush(WinColor.FromArgb(0x33, 0xC4, 0xA0, 0x35));
    }

    private static FrameworkElement? FindByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && fe.Name == name)
            return fe;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var match = FindByName(VisualTreeHelper.GetChild(root, i), name);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var nested in FindDescendants<T>(child))
                yield return nested;
        }
    }
}
