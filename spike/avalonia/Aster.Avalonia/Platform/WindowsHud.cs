using Avalonia;
using Avalonia.Controls;

namespace Aster.Avalonia.Platform;

/// <summary>
/// Applies the Win32 window styles and message handling that turn the host window
/// into a click-through, non-activating HUD. No-ops outside Windows.
/// </summary>
internal static class WindowsHud
{
    private const uint WM_NCHITTEST = 0x0084;
    private const uint WM_MOUSEACTIVATE = 0x0021;
    private const int HTTRANSPARENT = -1;
    private const int HTCLIENT = 1;
    private const int MA_NOACTIVATE = 3;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    public static void Attach(Window window, Func<PixelPoint, bool> isInteractive)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Win32Properties.AddWindowStylesCallback(window, (style, exStyle) =>
            (style, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));

        Win32Properties.AddWndProcHookCallback(window, (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            switch (msg)
            {
                case WM_MOUSEACTIVATE:
                    handled = true;
                    return new IntPtr(MA_NOACTIVATE);

                case WM_NCHITTEST:
                    var screenPoint = new PixelPoint(
                        unchecked((short)(lParam.ToInt64() & 0xFFFF)),
                        unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF)));
                    var clientPoint = window.PointToClient(screenPoint);
                    var scaling = window.RenderScaling;
                    var clientPixels = new PixelPoint(
                        (int)Math.Round(clientPoint.X * scaling),
                        (int)Math.Round(clientPoint.Y * scaling));
                    handled = true;
                    return isInteractive(clientPixels)
                        ? new IntPtr(HTCLIENT)
                        : new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        });
    }
}
