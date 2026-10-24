using System.Diagnostics;
using System.Management;

namespace DynamicIsland.Services;

public sealed class BrightnessService : IDisposable
{
    private System.Threading.Timer? _timer;
    private int _lastValue = -1;
    private int _polling;

    public bool IsSupported { get; private set; }
    public int Brightness { get; private set; }
    public event Action<int>? BrightnessChanged;

    public void Start()
    {
        if (!TryRead(out var value))
        {
            IsSupported = false;
            return;
        }

        IsSupported = true;
        Brightness = value;
        _lastValue = value;
        _timer = new System.Threading.Timer(_ => Poll(), null, 1200, 1200);
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return;
        }

        try
        {
            if (!TryRead(out var value))
            {
                return;
            }

            if (value == _lastValue)
            {
                return;
            }

            _lastValue = value;
            Brightness = value;
            BrightnessChanged?.Invoke(value);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private static bool TryRead(out int value)
    {
        value = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using var managementObject = (ManagementObject)item;
                var raw = managementObject["CurrentBrightness"];
                if (raw is null)
                {
                    continue;
                }

                value = Convert.ToInt32(raw);
                return value is >= 0 and <= 100;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Brightness read failed: {ex.Message}");
        }

        return false;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
