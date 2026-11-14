using Avalonia;
using Avalonia.Controls;

namespace Aster.Avalonia.Platform;

/// <summary>
/// Applies the Win32 window styles that keep the island window out of the
/// taskbar and prevent it from taking activation when clicked.
///
/// The island is its own top-level window, sized to the visible pill. There is
/// no full-size transparent host window, so clicks outside the pill land on the
/// application behind it without any hit-test forwarding. Expansion (the
/// future panel) resizes this window; a separate click-through effect layer can
/// be added later for glow/shadow.
/// </summary>
internal static class WindowsHud
{
    private const uint WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    public static void Attach(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Win32Properties.AddWindowStylesCallback(window, (style, exStyle) =>
            (style, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));

        Win32Properties.AddWndProcHookCallback(window, (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }

            return IntPtr.Zero;
        });
    }
}
