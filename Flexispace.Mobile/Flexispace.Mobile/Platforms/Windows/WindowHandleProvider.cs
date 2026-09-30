#if WINDOWS
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Flexispace.Mobile.Services.Real;

// MSAL's interactive flow on Windows works best when it knows which
// native window to center/parent its sign-in UI on
// (WithParentActivityOrWindow) - without it, MSAL still works but the
// sign-in window can appear behind the app or in an unexpected position.
// MAUI's own Window wraps a WinUI3 Microsoft.UI.Xaml.Window under the
// hood on this platform, so this pulls that out and resolves its native
// HWND via WinRT.Interop - the standard MAUI+MSAL Windows integration
// pattern. Only compiled for the Windows target (see the #if WINDOWS
// guard and the .Windows.cs suffix), matching Flexispace.Mobile.csproj's
// current Windows-only TargetFrameworks.
internal static class WindowHandleProvider
{
    public static IntPtr GetActiveWindowHandle()
    {
        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (mauiWindow?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
        {
            return IntPtr.Zero;
        }

        return WindowNative.GetWindowHandle(winUiWindow);
    }
}
#endif
