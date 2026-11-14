namespace Aster.Core.Models;

[Flags]
public enum Capability
{
    None = 0,
    Media = 1,
    Battery = 2,
    Network = 4,
    Tray = 8,
    Autostart = 16,
    Fullscreen = 32,
    NoActivate = 64,
    ClickThrough = 128,
    DpiChange = 256,
    SingleInstance = 512
}

public sealed record CapabilityMatrix(Capability Flags)
{
    public bool Has(Capability capability) => (Flags & capability) == capability;
}
