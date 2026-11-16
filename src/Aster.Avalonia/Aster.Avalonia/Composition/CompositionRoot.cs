using Aster.Core;
using Aster.Core.Logging;
using Aster.Core.Settings;
using Aster.Platform.Windows.Window;

namespace Aster.Avalonia.Composition;

public sealed class CompositionRoot
{
    public FileAppLog Log { get; }
    public JsonSettingsStore Settings { get; }
    public WindowsWindowPlacement Placement { get; }

    public CompositionRoot()
    {
        Log = new FileAppLog(AppPaths.LogPath);
        Settings = new JsonSettingsStore(AppPaths.SettingsPath, Log);
        Placement = new WindowsWindowPlacement(Log);
    }
}
