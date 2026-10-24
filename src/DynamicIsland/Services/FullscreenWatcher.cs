using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DynamicIsland.Interop;

namespace DynamicIsland.Services;

public sealed class FullscreenWatcher : IDisposable
{
    private readonly DispatcherTimer _timer;

    public bool IsFullscreen { get; private set; }
    public event Action<bool>? Changed;

    public FullscreenWatcher()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _timer.Tick += (_, _) => Refresh();
        Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        var fullscreen = IsForegroundFullscreen();

        if (!fullscreen)
        {
            try
            {
                if (NativeMethods.SHQueryUserNotificationState(out var state) == 0)
                {
                    fullscreen = state is NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
                        or NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                        or NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
                }
            }
            catch
            {
                // Ignore shell failures.
            }
        }

#if DEBUG
        var foreground = NativeMethods.GetForegroundWindow();
        var className = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(foreground, className, className.Capacity);
        var zoomed = NativeMethods.IsZoomed(foreground);
        var style = NativeMethods.GetWindowLongPtr(foreground, NativeMethods.GWL_STYLE).ToInt64();
        DebugLog.Write($"fullscreen-poll result={fullscreen} fg={foreground} class={className} zoomed={zoomed} style=0x{style:X}");
#endif

        if (fullscreen == IsFullscreen)
        {
            return;
        }

        IsFullscreen = fullscreen;
        Changed?.Invoke(fullscreen);
    }

    private static bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        var cls = className.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return false;
        }

        // A maximized window is not fullscreen, even though its non-client
        // borders can extend past the monitor edges on Windows 11.
        if (NativeMethods.IsZoomed(hwnd))
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        if ((style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION)
        {
            return false;
        }

        if (!NativeMethods.GetClientRect(hwnd, out var clientRect))
        {
            return false;
        }

        var origin = new NativeMethods.POINT();
        if (!NativeMethods.ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        var windowRect = new NativeMethods.RECT
        {
            Left = origin.X,
            Top = origin.Y,
            Right = origin.X + clientRect.Width,
            Bottom = origin.Y + clientRect.Height
        };

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var screen = info.rcMonitor;
        const int tolerance = 2;
        return windowRect.Left <= screen.Left + tolerance &&
               windowRect.Top <= screen.Top + tolerance &&
               windowRect.Right >= screen.Right - tolerance &&
               windowRect.Bottom >= screen.Bottom - tolerance;
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
