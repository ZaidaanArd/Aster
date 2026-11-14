namespace Aster.Core.Models;

public sealed record BatterySnapshot(int Percent, bool IsCharging, bool HasBattery);
