namespace Aster.Core.Models;

public sealed record AppSettings(
    int SchemaVersion,
    bool StartWithWindows,
    bool MinimalNotch,
    bool HideInFullscreen,
    bool GlassBackground,
    bool BouncySpring);

public static class SettingsSchema
{
    public const int CurrentVersion = 1;

    public static AppSettings Default { get; } = new(
        CurrentVersion,
        StartWithWindows: false,
        MinimalNotch: false,
        HideInFullscreen: true,
        GlassBackground: true,
        BouncySpring: true);
}
