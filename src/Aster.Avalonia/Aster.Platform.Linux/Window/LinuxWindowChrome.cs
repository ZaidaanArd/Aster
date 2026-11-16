using Aster.Core.Interfaces;
using Aster.Platform.Linux.X11;
using Aster.UI.Abstractions;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aster.Platform.Linux.Window;

public sealed class LinuxWindowChrome : IWindowChrome
{
    private readonly IAppLog _log;

    public LinuxWindowChrome(IAppLog log) => _log = log;

    public void Attach(Avalonia.Controls.Window window)
    {
        window.Opened += (_, _) =>
        {
            window.Screens.Changed += (_, _) => Apply(window);
            Apply(window);
        };
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Visual.IsVisibleProperty)
            {
                Apply(window);
            }
        };
    }

    private void Apply(Avalonia.Controls.Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            Dispatcher.UIThread.Post(() => Apply(window), DispatcherPriority.Background);
            return;
        }

        var display = X11Native.Display;
        if (display == IntPtr.Zero)
        {
            _log.Warn("X11 display unavailable; window chrome not applied.");
            return;
        }

        var hints = new X11Native.XWMHints
        {
            flags = X11Native.InputHint,
            input = 0
        };
        X11Native.XSetWMHints(display, handle, ref hints);

        var stateAtom = X11Native.Atom("_NET_WM_STATE");
        var atoms = new[]
        {
            X11Native.Atom("_NET_WM_STATE_ABOVE"),
            X11Native.Atom("_NET_WM_STATE_SKIP_TASKBAR"),
            X11Native.Atom("_NET_WM_STATE_SKIP_PAGER")
        };
        X11Native.XChangeProperty(
            display, handle, stateAtom,
            new IntPtr(X11Native.XaAtom), 32, X11Native.PropModeReplace,
            atoms, atoms.Length);

        var typeAtom = X11Native.Atom("_NET_WM_WINDOW_TYPE");
        // DOCK is the Phase 2 fallback: KWin focused the UTILITY window on
        // synthetic pill clicks during the 200% window probe.
        var typeValue = new[] { X11Native.Atom("_NET_WM_WINDOW_TYPE_DOCK") };
        X11Native.XChangeProperty(
            display, handle, typeAtom,
            new IntPtr(X11Native.XaAtom), 32, X11Native.PropModeReplace,
            typeValue, typeValue.Length);

        X11Native.XFlush(display);
        _log.Info($"Linux window chrome applied to 0x{handle.ToInt64():X}.");
    }
}
