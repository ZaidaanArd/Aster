using Aster.Core.Settings;
using Aster.Platform.Windows;
using Aster.Platform.Windows.Window;

namespace Aster.Avalonia.Composition;

public sealed class CompositionRoot
{
    public FileAppLog Log { get; }
    public JsonSettingsStore Settings { get; }
    public WindowsWindowPlacement Placement { get; }

    public CompositionRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Log = new FileAppLog(Path.Combine(appData, "AsterLab", "Avalonia", "logs", "app.log"));
        Settings = new JsonSettingsStore(
            Path.Combine(appData, "AsterLab", "Avalonia", "settings.json"),
            Log);
        Placement = new WindowsWindowPlacement(Log);
    }
}
