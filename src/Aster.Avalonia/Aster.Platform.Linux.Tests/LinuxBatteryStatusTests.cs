using Aster.Platform.Linux.System;

namespace Aster.Platform.Linux.Tests;

public class LinuxBatteryStatusTests
{
    [Fact]
    public async Task ReadsCapacityAndStatusFromSysfs()
    {
        var root = Path.Combine(Path.GetTempPath(), "aster-battery-" + Guid.NewGuid().ToString("N"));
        var bat = Path.Combine(root, "BAT0");
        Directory.CreateDirectory(bat);
        File.WriteAllText(Path.Combine(bat, "capacity"), "82");
        File.WriteAllText(Path.Combine(bat, "status"), "Charging");
        try
        {
            using var battery = new LinuxBatteryStatus(root, new InlineDispatcher(), new NullLog());
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (!battery.Current.HasBattery && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.Equal(82, battery.Current.Percent);
            Assert.True(battery.Current.IsCharging);
            Assert.True(battery.Current.HasBattery);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class InlineDispatcher : Aster.Core.Interfaces.IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
    }

    private sealed class NullLog : Aster.Core.Interfaces.IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
