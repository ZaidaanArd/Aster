using Aster.Core.Interfaces;
using Aster.Platform.Linux.X11;

namespace Aster.Platform.Linux.System;

public sealed class LinuxFullscreenWatcher : IFullscreenWatcher, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private bool _current;

    public LinuxFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log)
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
            try { await Task.Delay(900, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Refresh()
    {
        bool fullscreen = false;
        try
        {
            var display = X11Native.Display;
            if (display == IntPtr.Zero)
            {
                return;
            }

            var root = X11Native.XDefaultRootWindow(display);
            var active = X11Native.ReadWindow(root, "_NET_ACTIVE_WINDOW");
            if (active != IntPtr.Zero)
            {
                var atoms = X11Native.ReadAtoms(active, "_NET_WM_STATE");
                var full = X11Native.Atom("_NET_WM_STATE_FULLSCREEN");
                fullscreen = atoms.Contains(full);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Fullscreen query failed.", ex);
        }

        if (fullscreen == _current) return;
        Volatile.Write(ref _current, fullscreen);
        _dispatcher.Post(() => Changed?.Invoke(this, fullscreen));
    }

    public void Dispose() => _cts.Cancel();
}
