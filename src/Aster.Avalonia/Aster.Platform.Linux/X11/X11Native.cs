using System.Runtime.InteropServices;

namespace Aster.Platform.Linux.X11;

internal static class X11Native
{
    private const string Lib = "libX11";

    static X11Native()
    {
        // Must run before the first Xlib call; the display and the fullscreen
        // watcher are used from different threads.
        XInitThreads();
    }

    [DllImport(Lib)]
    private static extern int XInitThreads();

    private static readonly Lazy<IntPtr> s_display =
        new(() => XOpenDisplay(null), isThreadSafe: true);

    public static IntPtr Display => s_display.Value;

    [DllImport(Lib)]
    public static extern IntPtr XOpenDisplay(string? display);

    [DllImport(Lib)]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport(Lib)]
    public static extern int XMoveResizeWindow(
        IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [DllImport(Lib)]
    public static extern int XFlush(IntPtr display);

    [DllImport(Lib)]
    public static extern int XGetGeometry(
        IntPtr display,
        IntPtr drawable,
        out IntPtr root,
        out int x,
        out int y,
        out uint width,
        out uint height,
        out uint borderWidth,
        out uint depth);

    [DllImport(Lib)]
    public static extern int XQueryPointer(
        IntPtr display,
        IntPtr window,
        out IntPtr root,
        out IntPtr child,
        out int rootX,
        out int rootY,
        out int winX,
        out int winY,
        out uint mask);

    [DllImport(Lib)]
    public static extern IntPtr XGetDefault(
        IntPtr display, string program, string option);

    [DllImport(Lib)]
    public static extern IntPtr XInternAtom(
        IntPtr display, string atomName, bool onlyIfExists);

    [DllImport(Lib)]
    public static extern int XChangeProperty(
        IntPtr display,
        IntPtr window,
        IntPtr property,
        IntPtr type,
        int format,
        int mode,
        IntPtr[] data,
        int count);

    [DllImport(Lib)]
    public static extern int XSetWMHints(
        IntPtr display, IntPtr window, ref XWMHints hints);

    [StructLayout(LayoutKind.Sequential)]
    public struct XWMHints
    {
        public long flags;
        public int input;
        public int initialState;
        public IntPtr iconPixmap;
        public IntPtr iconWindow;
        public int iconX;
        public int iconY;
        public IntPtr iconMask;
        public IntPtr windowGroup;
    }

    public const long InputHint = 1L;
    public const int XaAtom = 4;
    public const int PropModeReplace = 0;

    public static IntPtr Atom(string name) =>
        Display == IntPtr.Zero ? IntPtr.Zero : XInternAtom(Display, name, false);
}
