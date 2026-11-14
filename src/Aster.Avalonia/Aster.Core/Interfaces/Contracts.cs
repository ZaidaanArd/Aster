using Aster.Core.Models;

namespace Aster.Core.Interfaces;

public interface IMediaSession
{
    MediaSnapshot Current { get; }
    event EventHandler<MediaSnapshot>? Changed;
    Task PlayPauseAsync(CancellationToken ct = default);
    Task NextAsync(CancellationToken ct = default);
    Task PreviousAsync(CancellationToken ct = default);
    Task SeekAsync(TimeSpan position, CancellationToken ct = default);
    TimeSpan EstimatedPosition(DateTimeOffset now);
}

public interface ISettingsStore
{
    AppSettings Current { get; }
    bool CanSave { get; }
    string? LoadWarning { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
    event EventHandler<AppSettings>? Changed;
}

public interface IBatteryStatus
{
    BatterySnapshot Current { get; }
    event EventHandler<BatterySnapshot>? Changed;
}

public interface INetworkStatus
{
    bool IsConnected { get; }
    event EventHandler<bool>? Changed;
}

public interface IAutostart
{
    bool IsEnabled { get; }
    bool SetEnabled(bool enabled);
}

public interface IFullscreenWatcher
{
    bool IsFullscreen { get; }
    event EventHandler<bool>? Changed;
}

public interface IClock
{
    DateTimeOffset Now { get; }
}

public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}

public interface IAppLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}
