using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;

namespace Aster.Platform.Linux.Media;

public sealed class MprisMediaSession : IMediaSession, IDisposable
{
    private readonly IMprisClient _client;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, DateTimeOffset> _lastActive = new(StringComparer.Ordinal);
    private MediaSnapshot _current = MediaSnapshot.Empty;
    private MprisPlayerState? _selected;
    private string _artKey = string.Empty;
    private byte[]? _artBytes;
    private bool _started;

    public MprisMediaSession(IMprisClient client, IUiDispatcher dispatcher, IAppLog log)
    {
        _client = client;
        _dispatcher = dispatcher;
        _log = log;
    }

    public MediaSnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<MediaSnapshot>? Changed;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public TimeSpan EstimatedPosition(DateTimeOffset now) =>
        MediaProgress.Estimate(Current, now);

    public async Task PlayPauseAsync(CancellationToken ct = default)
    {
        if (_selected is null) return;
        await _client.PlayPauseAsync(_selected.BusName, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
    }

    public async Task NextAsync(CancellationToken ct = default)
    {
        if (_selected is null) return;
        await _client.NextAsync(_selected.BusName, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
    }

    public async Task PreviousAsync(CancellationToken ct = default)
    {
        if (_selected is null) return;
        await _client.PreviousAsync(_selected.BusName, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
    }

    public async Task SeekAsync(TimeSpan position, CancellationToken ct = default)
    {
        if (_selected is null) return;
        var target = MediaProgress.ClampSeek(position, _selected.Metadata.Duration);
        var selected = _selected;
        try
        {
            await _client.SetPositionAsync(
                selected.BusName, selected.Metadata.TrackId, target, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"SetPosition failed; falling back to Seek: {ex.Message}");
            var offset = target - selected.Position;
            await _client.SeekAsync(selected.BusName, offset, ct).ConfigureAwait(false);
        }

        await RefreshAsync(ct).ConfigureAwait(false);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error("MPRIS poll failed.", ex);
            }

            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var names = await _client.ListPlayersAsync(ct).ConfigureAwait(false);
        var states = new List<MprisPlayerState>();
        foreach (var name in names)
        {
            var state = await _client.GetStateAsync(name, ct).ConfigureAwait(false);
            if (state is null) continue;
            states.Add(state);

            if (state.Playback == PlaybackState.Playing &&
                (!_lastActive.TryGetValue(name, out var previous) ||
                 previous < state.UpdatedAt))
            {
                _lastActive[name] = state.UpdatedAt;
            }
        }

        var playing = states
            .Where(state => state.Playback == PlaybackState.Playing)
            .OrderByDescending(state => _lastActive.GetValueOrDefault(state.BusName))
            .ThenBy(state => state.BusName, StringComparer.Ordinal)
            .FirstOrDefault();

        var selected = playing ?? states
            .OrderByDescending(state => _lastActive.GetValueOrDefault(state.BusName))
            .ThenBy(state => state.BusName, StringComparer.Ordinal)
            .FirstOrDefault();

        _selected = selected;
        if (selected is null)
        {
            Publish(MediaSnapshot.Empty);
            return;
        }

        var trackKey = $"{selected.Metadata.Title}|{selected.Metadata.Artist}|{selected.Metadata.TrackId}";
        if (!string.Equals(trackKey, _artKey, StringComparison.Ordinal))
        {
            _artKey = trackKey;
            _artBytes = null;
            var bytes = await LinuxArtworkReader.ReadAsync(selected.Metadata.ArtUrl, ct).ConfigureAwait(false);
            _artBytes = bytes;
        }

        Publish(new MediaSnapshot(
            selected.Metadata.Title,
            selected.Metadata.Artist,
            selected.Metadata.Album,
            _artBytes,
            _artBytes is null ? trackKey + "|none" : trackKey,
            selected.Playback,
            MediaProgress.ClampSeek(selected.Position, selected.Metadata.Duration),
            selected.Metadata.Duration,
            selected.SourceApp,
            DateTimeOffset.Now));
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
