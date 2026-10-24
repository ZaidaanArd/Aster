using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace DynamicIsland.Services;

public sealed class NetworkService : IDisposable
{
    private readonly DispatcherTimer _timer;

    public bool IsConnected { get; private set; }
    public event Action<bool>? Changed;

    public NetworkService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Refresh();
        Refresh();
        _timer.Start();
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
        catch
        {
            connected = false;
        }

        if (connected == IsConnected)
        {
            return;
        }

        IsConnected = connected;
        Changed?.Invoke(connected);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
