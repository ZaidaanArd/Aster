using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.Avalonia.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IBatteryStatus? _battery;
    private readonly INetworkStatus? _network;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private double _expansionProgress;
    [ObservableProperty] private string _clockText = "--:--";
    [ObservableProperty] private bool _isMinimal;
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private bool _isWifiConnected;

    public ShellViewModel(IBatteryStatus? battery = null, INetworkStatus? network = null)
    {
        _battery = battery;
        _network = network;

        if (_battery is not null)
        {
            _battery.Changed += OnBatteryChanged;
            ApplyBattery(_battery.Current);
        }

        if (_network is not null)
        {
            _network.Changed += OnNetworkChanged;
            IsWifiConnected = _network.IsConnected;
        }
    }

    public double ExpandedOpacity => Math.Clamp((ExpansionProgress - 0.18) / 0.82, 0, 1);
    public double CollapsedOpacity => Math.Clamp(1 - ExpansionProgress * 3, 0, 1);
    public string WifiText => IsWifiConnected ? "Wi-Fi" : "Off";
    public double WifiOpacity => IsWifiConnected ? 1 : 0.35;

    public CapabilityMatrix Capabilities { get; } = new(
        Capability.Media | Capability.Battery | Capability.Network | Capability.Tray |
        Capability.Autostart | Capability.Fullscreen | Capability.NoActivate |
        Capability.ClickThrough | Capability.DpiChange | Capability.SingleInstance);

    partial void OnExpansionProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ExpandedOpacity));
        OnPropertyChanged(nameof(CollapsedOpacity));
    }

    partial void OnIsWifiConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(WifiText));
        OnPropertyChanged(nameof(WifiOpacity));
    }

    private void OnBatteryChanged(object? sender, BatterySnapshot snapshot) => ApplyBattery(snapshot);
    private void OnNetworkChanged(object? sender, bool connected) => IsWifiConnected = connected;

    private void ApplyBattery(BatterySnapshot snapshot)
    {
        if (!snapshot.HasBattery)
        {
            BatteryText = string.Empty;
            return;
        }

        BatteryText = snapshot.IsCharging ? $"{snapshot.Percent}%+" : $"{snapshot.Percent}%";
    }

    public void UpdateClock(DateTimeOffset now) =>
        ClockText = now.ToString("HH:mm", CultureInfo.CurrentCulture);
}
