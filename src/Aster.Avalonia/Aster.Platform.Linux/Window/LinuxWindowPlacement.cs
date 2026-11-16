using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Platform.Linux.X11;
using Aster.UI.Abstractions;

namespace Aster.Platform.Linux.Window;

public sealed class LinuxWindowPlacement : IWindowPlacement
{
    private readonly IAppLog _log;

    public LinuxWindowPlacement(IAppLog log) => _log = log;

    public bool IsCursorInside(IntPtr handle)
    {
        var display = X11Native.Display;
        if (display == IntPtr.Zero || handle == IntPtr.Zero)
        {
            return false;
        }

        if (X11Native.XGetGeometry(
                display, handle, out _, out _, out _,
                out var width, out var height, out _, out _) == 0)
        {
            return false;
        }

        if (X11Native.XQueryPointer(
                display, handle, out _, out _,
                out _, out _, out var winX, out var winY, out _) == 0)
        {
            return false;
        }

        return winX >= 0 && winY >= 0 && winX < width && winY < height;
    }

    public uint GetDpi(IntPtr handle)
    {
        var display = X11Native.Display;
        if (display == IntPtr.Zero)
        {
            return 96;
        }

        var value = X11Native.XGetDefault(display, "Xft", "dpi");
        if (value == IntPtr.Zero)
        {
            return 96;
        }

        var text = global::System.Runtime.InteropServices.Marshal.PtrToStringAnsi(value);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dpi)
            ? (uint)Math.Round(dpi)
            : 96;
    }

    public void SetBounds(IntPtr handle, int x, int y, int width, int height)
    {
        var display = X11Native.Display;
        if (display == IntPtr.Zero || handle == IntPtr.Zero)
        {
            return;
        }

        if (X11Native.XMoveResizeWindow(
                display, handle, x, y, (uint)Math.Max(1, width), (uint)Math.Max(1, height)) == 0)
        {
            _log.Warn("XMoveResizeWindow failed.");
        }

        X11Native.XFlush(display);
    }
}
