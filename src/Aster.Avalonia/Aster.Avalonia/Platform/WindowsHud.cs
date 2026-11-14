using Avalonia.Controls;

namespace Aster.Avalonia.Platform;

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

        Win32Properties.AddWndProcHookCallback(window, (hWnd, msg, wParam, lParam, ref handled) =>
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
