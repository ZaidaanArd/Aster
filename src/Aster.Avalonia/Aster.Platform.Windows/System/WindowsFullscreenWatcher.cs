using System.Runtime.InteropServices;
using System.Text;
using Aster.Core.Interfaces;
using static Aster.Platform.Windows.Window.NativeMethods;

namespace Aster.Platform.Windows.System;

public sealed class WindowsFullscreenWatcher : IFullscreenWatcher, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private bool _current;

    public WindowsFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _ = LoopAsync(_cts.Token);
    }

    public bool IsFullscreen => Volatile.Read(ref _current);
    public event EventHandler<bool>? Changed;

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Refresh();
            try
            {
                await Task.Delay(900, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Refresh()
    {
        bool fullscreen;
        try
        {
            fullscreen = IsForegroundFullscreen();
            if (!fullscreen && SHQueryUserNotificationState(out var state) == 0)
            {
                fullscreen = state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY
                    or QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                    or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;
            }
        }
        catch (Exception ex)
        {
            _log.Error("Fullscreen query failed.", ex);
            fullscreen = false;
        }

        if (fullscreen == _current)
        {
            return;
        }

        Volatile.Write(ref _current, fullscreen);
        _dispatcher.Post(() => Changed?.Invoke(this, fullscreen));
    }

    private static bool IsForegroundFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        var cls = className.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return false;
        }

        if (IsZoomed(hwnd))
        {
            return false;
        }

        var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        if ((style & WS_CAPTION) == WS_CAPTION)
        {
            return false;
        }

        if (!GetClientRect(hwnd, out var clientRect))
        {
            return false;
        }

        var origin = new POINT();
        if (!ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        var windowRect = new RECT
        {
            Left = origin.X,
            Top = origin.Y,
            Right = origin.X + clientRect.Width,
            Bottom = origin.Y + clientRect.Height
        };

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        const int tolerance = 2;
        var screen = info.rcMonitor;
        return windowRect.Left <= screen.Left + tolerance &&
               windowRect.Top <= screen.Top + tolerance &&
               windowRect.Right >= screen.Right - tolerance &&
               windowRect.Bottom >= screen.Bottom - tolerance;
    }

    public void Dispose() => _cts.Cancel();
}
