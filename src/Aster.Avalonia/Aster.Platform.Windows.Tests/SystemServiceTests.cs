using Aster.Platform.Windows.System;

namespace Aster.Platform.Windows.Tests;

public class SystemServiceTests
{
    [Fact]
    public void BatterySnapshotIsInRange()
    {
        using var battery = new WindowsBatteryStatus(new InlineDispatcher(), new NullLog());
        var current = battery.Current;

        Assert.InRange(current.Percent, 0, 100);
    }

    [Fact]
    public void NetworkStatusReturnsBoolean()
    {
        using var network = new WindowsNetworkStatus(new InlineDispatcher(), new NullLog());
        _ = network.IsConnected;
    }

    [Fact]
    public void FullscreenWatcherReturnsBoolean()
    {
        using var watcher = new WindowsFullscreenWatcher(new InlineDispatcher(), new NullLog());
        _ = watcher.IsFullscreen;
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
