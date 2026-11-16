using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Windows.Media.Control;

namespace Aster.Platform.Windows.Media;

public sealed class SmtcMediaSession : IMediaSession, IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _deviceGate = new(1, 1);
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private MediaSnapshot _current = MediaSnapshot.Empty;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSessionManager? _subscribedManager;
    private string _artTrackKey = string.Empty;
    private string _artMissKey = string.Empty;
    private DateTimeOffset _artRetryAt = DateTimeOffset.MinValue;
    private byte[]? _artBytes;
    private TimeSpan _currentStartTime;
    private DateTimeOffset _lastManagerRecovery = DateTimeOffset.MinValue;
    private bool _started;

    public SmtcMediaSession(IUiDispatcher dispatcher, IAppLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    public MediaSnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<MediaSnapshot>? Changed;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        if (Environment.GetEnvironmentVariable("ASTER_DISABLE_BACKGROUND_POLLING") == "1")
        {
            return;
        }

        _ = PollLoopAsync(_cts.Token);
    }

    public TimeSpan EstimatedPosition(DateTimeOffset now) => MediaProgress.Estimate(Current, now);

    public Task PlayPauseAsync(CancellationToken ct = default) =>
        RunControlAsync(
            async session => await session.TryTogglePlayPauseAsync(),
            "play/pause",
            ct);

    public Task NextAsync(CancellationToken ct = default) =>
        RunControlAsync(
            async session => await session.TrySkipNextAsync(),
            "next",
            ct);

    public Task PreviousAsync(CancellationToken ct = default) =>
        RunControlAsync(
            async session => await session.TrySkipPreviousAsync(),
            "previous",
            ct);

    public Task SeekAsync(TimeSpan position, CancellationToken ct = default) =>
        RunControlAsync(
            async session =>
            {
                var target = MediaProgress.ClampSeek(position, Current.Duration);
                var absolute = SmtcTimeline.ToSeekPosition(_currentStartTime, target);
                await session.TryChangePlaybackPositionAsync(absolute.Ticks);
            },
            "seek",
            ct);

    public static string FriendlySource(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "Unknown";
        }

        var id = appId.ToLowerInvariant();
        if (id.Contains("spotify")) return "Spotify";
        if (id.Contains("msedge")) return "Edge";
        if (id.Contains("chrome")) return "Chrome";
        if (id.Contains("firefox")) return "Firefox";
        if (appId.Length == 16 && appId.All(Uri.IsHexDigit)) return "Browser";
        return appId;
    }

    private async Task RunControlAsync(
        Func<GlobalSystemMediaTransportControlsSession, Task> action,
        string operation,
        CancellationToken ct)
    {
        var acquired = false;
        try
        {
            await _deviceGate.WaitAsync(ct);
            acquired = true;

            var session = _manager?.GetCurrentSession();
            if (session is null)
            {
                return;
            }

            await action(session);
        }
        catch (Exception ex)
        {
            _log.Error($"SMTC {operation} failed.", ex);
            return;
        }
        finally
        {
            if (acquired)
            {
                _deviceGate.Release();
            }
        }

        try
        {
            await PollAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC poll failed.", ex);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollAsync(ct);
            }
            catch (Exception ex)
            {
                _log.Error("SMTC poll failed.", ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(650), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        await _pollGate.WaitAsync(ct);
        try
        {
            await PollCoreAsync(ct);
        }
        finally
        {
            _pollGate.Release();
        }
    }

    private async Task PollCoreAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_manager is null || (now - _lastManagerRecovery) > TimeSpan.FromSeconds(10))
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _lastManagerRecovery = now;
        }

        var manager = _manager;
        if (manager is null)
        {
            Publish(MediaSnapshot.Empty);
            return;
        }

        if (!ReferenceEquals(_subscribedManager, manager))
        {
            manager.CurrentSessionChanged += (_, _) =>
            {
                _manager = null;
                _ = PollSafeAsync();
            };
            _subscribedManager = manager;
        }

        var session = manager.GetCurrentSession();
        if (session is null)
        {
            session = manager.GetSessions().FirstOrDefault();
        }

        if (session is null)
        {
            Publish(MediaSnapshot.Empty);
            return;
        }

        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        var state = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackState.Stopped,
            _ => PlaybackState.None
        };

        var source = FriendlySource(session.SourceAppUserModelId);
        var trackKey = $"{properties.Title}|{properties.Artist}|{properties.AlbumTitle}|{source}";
        if (!string.Equals(trackKey, _artTrackKey, StringComparison.Ordinal))
        {
            _artTrackKey = trackKey;
            _artBytes = null;
            _artMissKey = string.Empty;
            _artRetryAt = DateTimeOffset.MinValue;
        }

        var retryNow = DateTimeOffset.UtcNow;
        if (_artBytes is null &&
            (!string.Equals(trackKey, _artMissKey, StringComparison.Ordinal) || retryNow >= _artRetryAt))
        {
            var bytes = await ArtworkReader.ReadAsync(properties.Thumbnail);
            if (bytes is not null)
            {
                _artBytes = bytes;
                _artMissKey = string.Empty;
            }
            else
            {
                _artMissKey = trackKey;
                _artRetryAt = retryNow.AddSeconds(2);
            }
        }

        var normalized = SmtcTimeline.Normalize(
            timeline.StartTime,
            timeline.Position,
            timeline.EndTime);
        _currentStartTime = timeline.StartTime;
        var duration = normalized.Duration;
        var position = normalized.Position;

        var artKey = _artBytes is null ? trackKey + "|none" : trackKey;
        var snapshot = new MediaSnapshot(
            string.IsNullOrWhiteSpace(properties.Title) ? "Unknown track" : properties.Title.Trim(),
            properties.Artist?.Trim() ?? string.Empty,
            properties.AlbumTitle?.Trim() ?? string.Empty,
            _artBytes,
            artKey,
            state,
            MediaProgress.ClampSeek(position, duration),
            duration,
            source,
            DateTimeOffset.Now);

        Publish(snapshot);
    }

    private async Task PollSafeAsync()
    {
        try
        {
            await PollAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC poll failed.", ex);
        }
    }

    private void Publish(MediaSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        _dispatcher.Post(() => Changed?.Invoke(this, snapshot));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
