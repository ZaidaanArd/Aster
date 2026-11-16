using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Abstractions;

public interface IPlatformFactory
{
    string PlatformName { get; }
    string AutostartLabel { get; }
    CapabilityMatrix Capabilities { get; }
    IAppLog CreateLog();
    ISettingsStore CreateSettingsStore(IAppLog log);
    IMediaSession CreateMediaSession(IUiDispatcher dispatcher, IAppLog log);
    IBatteryStatus CreateBatteryStatus(IUiDispatcher dispatcher, IAppLog log);
    INetworkStatus CreateNetworkStatus(IUiDispatcher dispatcher, IAppLog log);
    IAutostart CreateAutostart(IAppLog log);
    IFullscreenWatcher CreateFullscreenWatcher(IUiDispatcher dispatcher, IAppLog log);
    IWindowPlacement CreateWindowPlacement(IAppLog log);
    IWindowChrome CreateWindowChrome(IAppLog log);
    ISingleInstance CreateSingleInstance(string name);
}
