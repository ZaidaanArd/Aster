using Aster.Avalonia.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Avalonia.Tests;

public class ShellStatusTests
{
    [Fact]
    public void BatteryAndNetworkUpdate()
    {
        var battery = new FakeBattery();
        var network = new FakeNetwork();
        var vm = new ShellViewModel(battery, network);

        battery.Update(new BatterySnapshot(82, true, true));
        network.Update(true);

        Assert.Equal("82%+", vm.BatteryText);
        Assert.True(vm.IsWifiConnected);
        Assert.Equal("Wi-Fi", vm.WifiText);
        Assert.True(vm.Capabilities.Has(Capability.Media));
    }

    [Fact]
    public void NoBatteryHidesText()
    {
        var battery = new FakeBattery();
        var vm = new ShellViewModel(battery, new FakeNetwork());

        battery.Update(new BatterySnapshot(100, false, false));

        Assert.Equal(string.Empty, vm.BatteryText);
    }

    private sealed class FakeBattery : IBatteryStatus
    {
        public BatterySnapshot Current { get; private set; } = new(100, false, true);
        public event EventHandler<BatterySnapshot>? Changed;

        public void Update(BatterySnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, snapshot);
        }
    }

    private sealed class FakeNetwork : INetworkStatus
    {
        public bool IsConnected { get; private set; }
        public event EventHandler<bool>? Changed;

        public void Update(bool connected)
        {
            IsConnected = connected;
            Changed?.Invoke(this, connected);
        }
    }
}
