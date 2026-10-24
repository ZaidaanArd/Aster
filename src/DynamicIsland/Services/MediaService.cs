using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland.Services;

public sealed record MediaSnapshot(
    string Title,
    string Artist,
    string Album,
    BitmapImage? Artwork,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    string SourceApp);

public sealed class MediaService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _polling;
    private bool _disposed;
    private string _lastArtKey = string.Empty;
    private BitmapImage? _lastArt;
    private TimeSpan _duration;
    private TimeSpan _timelinePosition;
    private DateTimeOffset _timelineUpdated;
    private bool _isPlaying;

    public MediaSnapshot? Current { get; private set; }
    public event Action<MediaSnapshot?>? SnapshotChanged;

    public MediaService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public TimeSpan EstimatedPosition
    {
        get
        {
            var position = _timelinePosition;
            if (_isPlaying)
            {
                position += DateTimeOffset.Now - _timelineUpdated;
            }

            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }

            if (_duration > TimeSpan.Zero && position > _duration)
            {
                position = _duration;
            }

            return position;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged += (_, _) =>
                {
                    _lastArtKey = string.Empty;
                    _ = PollAsync();
                };
            }

            _timer.Start();
            await PollAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media init failed: {ex}");
        }
    }

    private async Task PollAsync()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1 || _disposed)
        {
            return;
        }

        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = _manager?.GetCurrentSession();
            if (session is null)
            {
                _session = null;
                if (Current is not null)
                {
                    Current = null;
                    _isPlaying = false;
                    _duration = TimeSpan.Zero;
                    _timelinePosition = TimeSpan.Zero;
                    SnapshotChanged?.Invoke(null);
                }

                return;
            }

            if (!ReferenceEquals(session, _session))
            {
                _session = session;
                _lastArtKey = string.Empty;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            _isPlaying = playback.PlaybackStatus ==
                         GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            _timelinePosition = timeline.Position;
            _timelineUpdated = timeline.LastUpdatedTime;
            _duration = timeline.EndTime > TimeSpan.Zero ? timeline.EndTime : TimeSpan.Zero;

            var artKey = $"{properties.Title}|{properties.Artist}|{properties.AlbumTitle}";
            if (!string.Equals(artKey, _lastArtKey, StringComparison.Ordinal))
            {
                _lastArtKey = artKey;
                _lastArt = await LoadArtworkAsync(properties.Thumbnail);
            }

            var snapshot = new MediaSnapshot(
                string.IsNullOrWhiteSpace(properties.Title) ? "Unknown track" : properties.Title.Trim(),
                string.IsNullOrWhiteSpace(properties.Artist) ? "Unknown artist" : properties.Artist.Trim(),
                properties.AlbumTitle?.Trim() ?? string.Empty,
                _lastArt,
                _isPlaying,
                EstimatedPosition,
                _duration,
                FriendlySource(session.SourceAppUserModelId));

            Current = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media poll failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _session ?? _manager?.GetCurrentSession();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.TryTogglePlayPauseAsync();
            await PollAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task NextAsync()
    {
        var session = _session ?? _manager?.GetCurrentSession();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.TrySkipNextAsync();
            await PollAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task PreviousAsync()
    {
        var session = _session ?? _manager?.GetCurrentSession();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.TrySkipPreviousAsync();
            await PollAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        var session = _session ?? _manager?.GetCurrentSession();
        if (session is null)
        {
            return;
        }

        try
        {
            await session.TryChangePlaybackPositionAsync(position.Ticks);
            _timelinePosition = position;
            _timelineUpdated = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private static async Task<BitmapImage?> LoadArtworkAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0)
            {
                return null;
            }

            var size = (uint)Math.Min(stream.Size, 8u * 1024u * 1024u);
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 256;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Artwork failed: {ex.Message}");
            return null;
        }
    }

    private static string FriendlySource(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "Media";
        }

        if (appId.Length == 16 && appId.All(Uri.IsHexDigit))
        {
            return "Browser";
        }

        var id = appId.ToLowerInvariant();
        if (id.Contains("spotify")) return "Spotify";
        if (id.Contains("applemusic") || id.Contains("itunes")) return "Apple Music";
        if (id.Contains("microsoftedge") || id.Contains("msedge")) return "Edge";
        if (id.Contains("chrome")) return "Chrome";
        if (id.Contains("firefox")) return "Firefox";
        if (id.Contains("brave")) return "Brave";
        if (id.Contains("opera")) return "Opera";
        if (id.Contains("vlc")) return "VLC";
        if (id.Contains("wmplayer")) return "Windows Media Player";
        if (id.Contains("zune") || id.Contains("mediaplayer")) return "Media Player";
        if (id.Contains("music")) return "Music";
        return appId;
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _session = null;
        _manager = null;
        _lastArt = null;
    }
}
