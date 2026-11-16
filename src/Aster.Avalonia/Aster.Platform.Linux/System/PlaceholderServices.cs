using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Platform.Linux.System;

public sealed class NoBatteryStatus : IBatteryStatus
{
    public BatterySnapshot Current { get; } = new(100, false, false);
    public event EventHandler<BatterySnapshot>? Changed { add { } remove { } }
}

public sealed class NoFullscreenWatcher : IFullscreenWatcher
{
    public bool IsFullscreen => false;
    public event EventHandler<bool>? Changed { add { } remove { } }
}

public sealed class NoAutostart : IAutostart
{
    public bool IsEnabled => false;
    public bool SetEnabled(bool enabled) => false;
}
