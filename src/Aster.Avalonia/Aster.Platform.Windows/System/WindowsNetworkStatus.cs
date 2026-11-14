using System.Net.NetworkInformation;
using Aster.Core.Interfaces;

namespace Aster.Platform.Windows.System;

public sealed class WindowsNetworkStatus : INetworkStatus, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private bool _current;

    public WindowsNetworkStatus(IUiDispatcher dispatcher, IAppLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _ = LoopAsync(_cts.Token);
    }

    public bool IsConnected => Volatile.Read(ref _current);
    public event EventHandler<bool>? Changed;

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
        bool connected;
        try
        {
            connected = NetworkInterface.GetIsNetworkAvailable() &&
                NetworkInterface.GetAllNetworkInterfaces().Any(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
        }
        catch (Exception ex)
        {
            _log.Error("Network status query failed.", ex);
            connected = false;
        }

        if (connected == _current)
        {
            return;
        }

        Volatile.Write(ref _current, connected);
        _dispatcher.Post(() => Changed?.Invoke(this, connected));
    }

    public void Dispose() => _cts.Cancel();
}
