using Android.Content.Res;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Google.Android.Material.Badge;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Shape;
using Microsoft.Maui.Controls.Handlers;
using AColor = Android.Graphics.Color;
using AView = Android.Views.View;

namespace Flexispace.Mobile.Platforms.Android;

/// <summary>
/// Native BottomNavigationView chrome: Material 3 active-indicator pill behind the
/// selected icon, rounded/elevated bar, bounce on tab change, Alerts badge.
/// </summary>
public static class TabBarHandlerFix
{
    private static int _lastUnread;

    public static void AppendMapping()
    {
        ShellHandler.Mapper.AppendToMapping("FlexispaceTabBarChrome", (handler, _) =>
        {
            if (handler.PlatformView is not AView native)
                return;

            native.Post(() => ApplyToView(native));
        });
    }

    public static void ApplyChrome(Shell shell)
    {
        if (GetNative(shell) is AView native)
            native.Post(() => ApplyToView(native));
    }

    public static void BounceSelected(Shell shell)
    {
        if (GetNative(shell) is AView native)
            native.Post(() => BounceSelected(FindBottomNav(native)));
    }

    public static void UpdateBadge(Shell shell, int unread)
    {
        _lastUnread = unread;
        if (GetNative(shell) is AView native)
            native.Post(() => UpdateAlertsBadge(FindBottomNav(native), unread));
    }

    private static AView? GetNative(Shell shell) => shell.Handler?.PlatformView as AView;

    private static void ApplyToView(AView native)
    {
        var bottomNav = FindBottomNav(native) ?? FindBottomNav(native.RootView);
        if (bottomNav is null)
            return;

        StyleBar(bottomNav);
        ApplyIconPill(bottomNav);
        UpdateAlertsBadge(bottomNav, _lastUnread);
    }

    private static void StyleBar(BottomNavigationView bottomNav)
    {
        var radius = Dp(bottomNav, 16);
        var shape = new MaterialShapeDrawable();
        shape.FillColor = ColorStateList.ValueOf(AColor.White);
        shape.ShapeAppearanceModel = new ShapeAppearanceModel.Builder()
            .SetTopLeftCorner(CornerFamily.Rounded, radius)
            .SetTopRightCorner(CornerFamily.Rounded, radius)
            .Build();
        bottomNav.Background = shape;
        bottomNav.Elevation = Dp(bottomNav, 12);
        bottomNav.SetPadding(bottomNav.PaddingLeft, (int)Dp(bottomNav, 6), bottomNav.PaddingRight, bottomNav.PaddingBottom);
    }

    private static void ApplyIconPill(BottomNavigationView bottomNav)
    {
        var gold = AColor.Rgb(0xC4, 0xA0, 0x35);
        bottomNav.SetItemActiveIndicatorEnabled(true);
        bottomNav.ItemActiveIndicatorColor = ColorStateList.ValueOf(gold);
        bottomNav.SetItemActiveIndicatorWidth((int)Dp(bottomNav, 48));
        bottomNav.SetItemActiveIndicatorHeight((int)Dp(bottomNav, 32));
        bottomNav.SetItemActiveIndicatorShapeAppearance(
            new ShapeAppearanceModel.Builder()
                .SetAllCornerSizes(Dp(bottomNav, 20))
                .Build());
        bottomNav.ItemBackground = null;
    }

    private static void BounceSelected(BottomNavigationView? bottomNav)
    {
        if (bottomNav is null)
            return;

        var menuView = bottomNav.GetChildAt(0) as ViewGroup;
        if (menuView is null)
            return;

        for (var i = 0; i < menuView.ChildCount; i++)
        {
            var child = menuView.GetChildAt(i);
            if (child is null || !child.Selected)
                continue;

            child.Animate()
                ?.ScaleX(1.15f)
                .ScaleY(1.15f)
                .SetDuration(80)
                .SetInterpolator(new OvershootInterpolator())
                .WithEndAction(new Java.Lang.Runnable(() =>
                {
                    child.Animate()
                        ?.ScaleX(1f)
                        .ScaleY(1f)
                        .SetDuration(70)
                        .Start();
                }))
                .Start();
            break;
        }
    }

    private static void UpdateAlertsBadge(BottomNavigationView? bottomNav, int unread)
    {
        if (bottomNav?.Menu is null)
            return;

        for (var i = 0; i < bottomNav.Menu.Size(); i++)
        {
            var item = bottomNav.Menu.GetItem(i);
            if (item is null)
                continue;
            if (item.TitleFormatted?.ToString()?.Contains("Alert", StringComparison.OrdinalIgnoreCase) != true)
                continue;

            if (unread <= 0)
            {
                bottomNav.RemoveBadge(item.ItemId);
                return;
            }

            var badge = bottomNav.GetOrCreateBadge(item.ItemId);
            badge.BackgroundColor = AColor.Rgb(0x9A, 0x7A, 0x1F);
            badge.BadgeTextColor = AColor.White;
            badge.Number = unread;
            badge.MaxCharacterCount = 2;
            badge.SetVisible(true);
            return;
        }
    }

    private static BottomNavigationView? FindBottomNav(AView? view)
    {
        switch (view)
        {
            case null:
                return null;
            case BottomNavigationView bottom:
                return bottom;
            case ViewGroup group:
                {
                    for (var i = 0; i < group.ChildCount; i++)
                    {
                        var found = FindBottomNav(group.GetChildAt(i));
                        if (found is not null)
                            return found;
                    }

                    break;
                }
        }

        return null;
    }

    private static float Dp(AView view, float dp) =>
        TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, view.Resources?.DisplayMetrics);
}
