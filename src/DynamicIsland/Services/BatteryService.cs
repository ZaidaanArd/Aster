using System.Windows.Threading;

namespace DynamicIsland.Services;

public sealed class BatteryService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _initialized;

    public int Percent { get; private set; } = 100;
    public bool IsCharging { get; private set; }
    public bool HasBattery { get; private set; }
    public event Action<int, bool>? Changed;

    public BatteryService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Refresh();
        Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        try
        {
            var status = System.Windows.Forms.SystemInformation.PowerStatus;
            HasBattery = (status.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) == 0;

            var percent = (int)Math.Round(status.BatteryLifePercent * 100);
            if (percent is < 0 or > 100)
            {
                percent = 100;
            }

            var charging = status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            var changed = !_initialized || percent != Percent || charging != IsCharging;
            Percent = percent;
            IsCharging = charging;
            _initialized = true;

            if (changed)
            {
                Changed?.Invoke(Percent, IsCharging);
            }
        }
        catch
        {
            HasBattery = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
