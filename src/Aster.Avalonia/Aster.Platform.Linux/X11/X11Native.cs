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

    [DllImport(Lib)]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(Lib)]
    public static extern int XGetWindowProperty(
        IntPtr display,
        IntPtr window,
        IntPtr property,
        long longOffset,
        long longLength,
        bool delete,
        IntPtr requiredType,
        out IntPtr actualType,
        out int actualFormat,
        out IntPtr nitems,
        out IntPtr bytesAfter,
        out IntPtr prop);

    [DllImport(Lib)]
    public static extern int XFree(IntPtr data);

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

    public static IntPtr[] ReadAtoms(IntPtr window, string property)
    {
        var display = Display;
        if (display == IntPtr.Zero || window == IntPtr.Zero) return Array.Empty<IntPtr>();

        var result = XGetWindowProperty(
            display, window, Atom(property), 0, 1024, false,
            new IntPtr(XaAtom), out _, out _, out var count, out _, out var data);
        if (result != 0 || data == IntPtr.Zero) return Array.Empty<IntPtr>();

        try
        {
            var length = count.ToInt64();
            var atoms = new IntPtr[length];
            Marshal.Copy(data, atoms, 0, (int)length);
            return atoms;
        }
        finally
        {
            XFree(data);
        }
    }

    public static IntPtr ReadWindow(IntPtr window, string property)
    {
        var display = Display;
        if (display == IntPtr.Zero) return IntPtr.Zero;
        var result = XGetWindowProperty(
            display, window, Atom(property), 0, 1, false,
            new IntPtr(33), out _, out _, out var count, out _, out var data);
        if (result != 0 || data == IntPtr.Zero || count == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            return Marshal.ReadIntPtr(data);
        }
        finally
        {
            XFree(data);
        }
    }
}
