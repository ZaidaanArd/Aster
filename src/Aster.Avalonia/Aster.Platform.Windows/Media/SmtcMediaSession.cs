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
    private MediaSnapshot _current = MediaSnapshot.Empty;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string _artTrackKey = string.Empty;
    private string _artMissKey = string.Empty;
    private DateTimeOffset _artRetryAt = DateTimeOffset.MinValue;
    private byte[]? _artBytes;
    private bool _started;
    private bool _sessionSubscribed;

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
        _ = PollLoopAsync(_cts.Token);
    }

    public TimeSpan EstimatedPosition(DateTimeOffset now) => MediaProgress.Estimate(Current, now);

    public async Task PlayPauseAsync(CancellationToken ct = default)
    {
        var session = _manager?.GetCurrentSession();
        if (session is null) return;

        try
        {
            await session.TryTogglePlayPauseAsync();
            await PollAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC play/pause failed.", ex);
        }
    }

    public async Task NextAsync(CancellationToken ct = default)
    {
        var session = _manager?.GetCurrentSession();
        if (session is null) return;

        try
        {
            await session.TrySkipNextAsync();
            await PollAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC next failed.", ex);
        }
    }

    public async Task PreviousAsync(CancellationToken ct = default)
    {
        var session = _manager?.GetCurrentSession();
        if (session is null) return;

        try
        {
            await session.TrySkipPreviousAsync();
            await PollAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC previous failed.", ex);
        }
    }

    public async Task SeekAsync(TimeSpan position, CancellationToken ct = default)
    {
        var session = _manager?.GetCurrentSession();
        if (session is null) return;

        try
        {
            var duration = Current.Duration;
            var target = MediaProgress.ClampSeek(position, duration);
            await session.TryChangePlaybackPositionAsync(target.Ticks);
            await PollAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error("SMTC seek failed.", ex);
        }
    }

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
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        if (!_sessionSubscribed)
        {
            _manager.CurrentSessionChanged += (_, _) => _ = PollAsync(_cts.Token);
            _sessionSubscribed = true;
        }

        var session = _manager.GetCurrentSession();

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

        var now = DateTimeOffset.UtcNow;
        if (_artBytes is null &&
            (!string.Equals(trackKey, _artMissKey, StringComparison.Ordinal) || now >= _artRetryAt))
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
                _artRetryAt = now.AddSeconds(2);
            }
        }

        var artKey = _artBytes is null ? trackKey + "|none" : trackKey;
        var duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : TimeSpan.Zero;
        var snapshot = new MediaSnapshot(
            string.IsNullOrWhiteSpace(properties.Title) ? "Unknown track" : properties.Title.Trim(),
            properties.Artist?.Trim() ?? string.Empty,
            properties.AlbumTitle?.Trim() ?? string.Empty,
            _artBytes,
            artKey,
            state,
            MediaProgress.ClampSeek(timeline.Position, duration),
            duration,
            source,
            DateTimeOffset.Now);

        Publish(snapshot);
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
