using Aster.Core;
using Aster.Core.Interfaces;
using Aster.Core.Logging;
using Aster.Core.Models;
using Aster.Core.Settings;
using Aster.Core.System;
using Aster.Platform.Windows.Media;
using Aster.Platform.Windows.System;
using Aster.Platform.Windows.Window;
using Aster.UI.Abstractions;

namespace Aster.Platform.Windows;

public sealed class WindowsPlatformFactory : IPlatformFactory
{
    public string PlatformName => "Windows";
    public string AutostartLabel => "Start with Windows";
    public CapabilityMatrix Capabilities => new(
        Capability.Media | Capability.Battery | Capability.Network | Capability.Tray |
        Capability.Autostart | Capability.Fullscreen | Capability.NoActivate |
        Capability.DpiChange | Capability.SingleInstance);

    public IAppLog CreateLog() => new FileAppLog(AppPaths.LogPath);

    public ISettingsStore CreateSettingsStore(IAppLog log) =>
        new JsonSettingsStore(AppPaths.SettingsPath, log);

    public IMediaSession CreateMediaSession(IUiDispatcher dispatcher, IAppLog log) =>
        new SmtcMediaSession(dispatcher, log);

    public IBatteryStatus CreateBatteryStatus(IUiDispatcher dispatcher, IAppLog log) =>
        new WindowsBatteryStatus(dispatcher, log);

    public INetworkStatus CreateNetworkStatus(IUiDispatcher dispatcher, IAppLog log) =>
        new NetworkStatus(dispatcher, log);

    public IAutostart CreateAutostart(IAppLog log) => new RegistryAutostart(log);

    public IFullscreenWatcher CreateFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log) =>
        new WindowsFullscreenWatcher(dispatcher, log);

    public IWindowPlacement CreateWindowPlacement(IAppLog log) =>
        new WindowsWindowPlacement(log);

    public IWindowChrome CreateWindowChrome(IAppLog log) => new WindowsWindowChrome();

    public ISingleInstance CreateSingleInstance(string name) =>
        new MutexSingleInstance(name);
}
