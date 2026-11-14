using System.Runtime.InteropServices;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Platform.Windows.System;

public sealed class WindowsBatteryStatus : IBatteryStatus, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private BatterySnapshot _current = new(100, false, true);

    public WindowsBatteryStatus(IUiDispatcher dispatcher, IAppLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _ = LoopAsync(_cts.Token);
    }

    public BatterySnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<BatterySnapshot>? Changed;

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Refresh();
            try
            {
                await Task.Delay(4000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Refresh()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return;
        }

        var hasBattery = (status.BatteryFlag & 128) == 0;
        var percent = status.BatteryLifePercent == byte.MaxValue
            ? 100
            : Math.Clamp((int)status.BatteryLifePercent, 0, 100);
        var charging = status.ACLineStatus == 1;
        var next = new BatterySnapshot(percent, charging, hasBattery);

        if (next == Current)
        {
            return;
        }

        Volatile.Write(ref _current, next);
        _dispatcher.Post(() => Changed?.Invoke(this, next));
    }

    public void Dispose() => _cts.Cancel();

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
}
