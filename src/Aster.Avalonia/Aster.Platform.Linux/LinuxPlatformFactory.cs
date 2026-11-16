using Aster.Core;
using Aster.Core.Interfaces;
using Aster.Core.Logging;
using Aster.Core.Models;
using Aster.Core.Settings;
using Aster.Core.System;
using Aster.Platform.Linux.Media;
using Aster.Platform.Linux.System;
using Aster.Platform.Linux.Window;
using Aster.UI.Abstractions;

namespace Aster.Platform.Linux;

public sealed class LinuxPlatformFactory : IPlatformFactory
{
    private readonly bool _useX11;

    public LinuxPlatformFactory(bool useX11 = true) => _useX11 = useX11;

    public string PlatformName => _useX11 ? "Linux" : "Linux (Wayland)";
    public string AutostartLabel => "Start with system";
    public CapabilityMatrix Capabilities => new(
        Capability.Media | Capability.Battery | Capability.Network | Capability.Tray |
        Capability.Autostart | Capability.DpiChange | Capability.SingleInstance |
        (_useX11 ? Capability.NoActivate | Capability.Fullscreen : Capability.None));

    public IAppLog CreateLog() => new FileAppLog(AppPaths.LogPath);
    public ISettingsStore CreateSettingsStore(IAppLog log) =>
        new JsonSettingsStore(AppPaths.SettingsPath, log);
    public IMediaSession CreateMediaSession(IUiDispatcher dispatcher, IAppLog log) =>
        new MprisMediaSession(new DBusMprisClient(), dispatcher, log);
    public IBatteryStatus CreateBatteryStatus(IUiDispatcher dispatcher, IAppLog log) =>
        new LinuxBatteryStatus(null, dispatcher, log);
    public INetworkStatus CreateNetworkStatus(IUiDispatcher dispatcher, IAppLog log) =>
        new NetworkStatus(dispatcher, log);
    public IAutostart CreateAutostart(IAppLog log) => new LinuxAutostart();
    public IFullscreenWatcher CreateFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log) =>
        new LinuxFullscreenWatcher(dispatcher, log);
    public IWindowPlacement CreateWindowPlacement(IAppLog log) =>
        _useX11 ? new LinuxWindowPlacement(log) : new NoopWindowPlacement();
    public IWindowChrome CreateWindowChrome(IAppLog log) =>
        _useX11 ? new LinuxWindowChrome(log) : new NoopWindowChrome();
    public ISingleInstance CreateSingleInstance(string name) =>
        new MutexSingleInstance(name);
}
