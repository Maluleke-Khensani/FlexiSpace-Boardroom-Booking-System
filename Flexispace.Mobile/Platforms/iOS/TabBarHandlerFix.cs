using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Controls.Handlers;
using UIKit;

namespace Flexispace.Mobile.Platforms.iOS;

/// <summary>
/// Native UITabBar chrome: gold pill behind the selected icon, rounded/elevated bar,
/// spring bounce on tab change, Alerts badge.
/// </summary>
public static class TabBarHandlerFix
{
    private const nint PillTag = 9001;
    private static int _lastUnread;
    private static readonly UIColor BrandGold = UIColor.FromRGB(0xC4, 0xA0, 0x35);
    private static readonly UIColor BrandGoldDeep = UIColor.FromRGB(0x9A, 0x7A, 0x1F);

    public static void AppendMapping()
    {
        ShellHandler.Mapper.AppendToMapping("FlexispaceTabBarChrome", (handler, _) =>
        {
            if (handler.PlatformView is not UIView native)
                return;

            native.Superview?.SetNeedsLayout();
            ApplyToView(native);
        });
    }

    public static void ApplyChrome(Shell shell)
    {
        if (GetNative(shell) is UIView native)
            ApplyToView(native);
    }

    public static void BounceSelected(Shell shell)
    {
        if (GetNative(shell) is UIView native)
            BounceSelected(FindTabBar(native));
    }

    public static void UpdateBadge(Shell shell, int unread)
    {
        _lastUnread = unread;
        if (GetNative(shell) is UIView native)
            UpdateAlertsBadge(FindTabBar(native), unread);
    }

    private static UIView? GetNative(Shell shell) => shell.Handler?.PlatformView as UIView;

    private static void ApplyToView(UIView native)
    {
        var tabBar = FindTabBar(native);
        if (tabBar is null)
            return;

        StyleBar(tabBar);
        UpdatePill(tabBar);
        UpdateAlertsBadge(tabBar, _lastUnread);
    }

    private static void StyleBar(UITabBar tabBar)
    {
        tabBar.BackgroundColor = UIColor.White;
        tabBar.Layer.CornerRadius = 16;
        tabBar.Layer.MaskedCorners = CACornerMask.MinXMinYCorner | CACornerMask.MaxXMinYCorner;
        tabBar.Layer.MasksToBounds = false;
        tabBar.ClipsToBounds = false;
        tabBar.Layer.ShadowColor = UIColor.Black.CGColor;
        tabBar.Layer.ShadowOpacity = 0.16f;
        tabBar.Layer.ShadowRadius = 8;
        tabBar.Layer.ShadowOffset = new CGSize(0, -3);

        var appearance = new UITabBarAppearance();
        appearance.ConfigureWithOpaqueBackground();
        appearance.BackgroundColor = UIColor.White;
        appearance.ShadowColor = UIColor.Clear;
        tabBar.StandardAppearance = appearance;
        tabBar.ScrollEdgeAppearance = appearance;
    }

    private static void UpdatePill(UITabBar tabBar)
    {
        var pill = tabBar.ViewWithTag(PillTag);
        if (pill is null)
        {
            pill = new UIView
            {
                Tag = PillTag,
                BackgroundColor = BrandGold,
                UserInteractionEnabled = false
            };
            pill.Layer.CornerRadius = 16;
            tabBar.InsertSubview(pill, 0);
        }

        var selected = FindSelectedButton(tabBar);
        var image = FindIconView(selected);
        if (selected is null || image is null)
        {
            pill.Hidden = true;
            return;
        }

        pill.Hidden = false;
        var frame = tabBar.ConvertRectFromView(image.Frame, image.Superview);
        pill.Frame = new CGRect(frame.X - 10, frame.Y - 6, frame.Width + 20, frame.Height + 12);
    }

    private static void BounceSelected(UITabBar? tabBar)
    {
        var button = FindSelectedButton(tabBar);
        var icon = FindIconView(button) ?? button;
        if (icon is null)
            return;

        icon.Transform = CGAffineTransform.MakeIdentity();
        UIView.AnimateNotify(
            0.15,
            0,
            0.45f,
            0.8f,
            UIViewAnimationOptions.CurveEaseOut,
            () => icon.Transform = CGAffineTransform.MakeScale(1.15f, 1.15f),
            _ => UIView.Animate(0.12, () => icon.Transform = CGAffineTransform.MakeIdentity()));
    }

    private static void UpdateAlertsBadge(UITabBar? tabBar, int unread)
    {
        if (tabBar?.Items is null)
            return;

        foreach (var item in tabBar.Items)
        {
            if (item.Title?.Contains("Alert", StringComparison.OrdinalIgnoreCase) != true)
                continue;

            item.BadgeColor = BrandGoldDeep;
            item.BadgeValue = unread > 0 ? (unread > 9 ? "9+" : unread.ToString()) : null;
            return;
        }
    }

    private static UITabBar? FindTabBar(UIView? view)
    {
        switch (view)
        {
            case null:
                return null;
            case UITabBar tabBar:
                return tabBar;
        }

        foreach (var child in view.Subviews)
        {
            var found = FindTabBar(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static UIView? FindSelectedButton(UITabBar? tabBar)
    {
        if (tabBar is null)
            return null;

        foreach (var child in tabBar.Subviews)
        {
            if (child.Tag == PillTag)
                continue;
            if (child is UIControl { Selected: true } control)
                return control;
            if (child.Class.Name.Contains("TabBarButton", StringComparison.Ordinal) && child.UserInteractionEnabled)
            {
                // Fall back: the selected item sits under the tab bar's selectedItem.
            }
        }

        var items = tabBar.Items;
        if (items is null || tabBar.SelectedItem is null)
            return null;

        var index = Array.IndexOf(items, tabBar.SelectedItem);
        var buttons = tabBar.Subviews
            .Where(v => v.Tag != PillTag && v.Class.Name.Contains("Button", StringComparison.Ordinal))
            .OrderBy(v => v.Frame.X)
            .ToArray();
        if (index >= 0 && index < buttons.Length)
            return buttons[index];

        return null;
    }

    private static UIView? FindIconView(UIView? button)
    {
        if (button is null)
            return null;
        return button.Subviews.OfType<UIImageView>().FirstOrDefault()
               ?? button.Subviews.FirstOrDefault();
    }
}
