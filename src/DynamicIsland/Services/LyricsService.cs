using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DynamicIsland.Services;

public sealed class LyricsLine
{
    public double TimeSeconds { get; set; }
    public string Text { get; set; } = string.Empty;

    [JsonIgnore]
    public TimeSpan Time => TimeSpan.FromSeconds(TimeSeconds);
}

public sealed class LyricsDocument
{
    public bool IsSynced { get; set; }
    public bool IsInstrumental { get; set; }
    public string? PlainLyrics { get; set; }
    public List<LyricsLine> Lines { get; set; } = new();
    public string? TrackTitle { get; set; }
    public string? ArtistName { get; set; }
    public string Source { get; set; } = "LRCLIB";
}

public sealed class LyricsService : IDisposable
{
    private static readonly Regex TimestampRegex = new(
        @"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]",
        RegexOptions.Compiled);

    private static readonly Regex OffsetRegex = new(
        @"\[offset:([+-]?\d+)\]",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly SettingsService _settings;
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;

    private CancellationTokenSource? _cts;
    private int _generation;
    private string _currentKey = string.Empty;
    private MediaSnapshot? _lastSnapshot;
    private bool _lastEnabled;

    public LyricsDocument? Current { get; private set; }
    public bool IsLoading { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool HasError { get; private set; }
    public string CacheDirectory => _cacheDirectory;
    public event Action? Changed;

    public LyricsService(SettingsService settings)
    {
        _settings = settings;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamicIsland",
            "lyrics");
        Directory.CreateDirectory(_cacheDirectory);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland/0.9.4 (Windows; WPF)");

        _lastEnabled = settings.Current.LyricsEnabled;
        _settings.Changed += OnSettingsChanged;
    }

    public async Task LoadAsync(MediaSnapshot? snapshot, bool force = false)
    {
        _lastSnapshot = snapshot;

        if (!_settings.Current.LyricsEnabled)
        {
            CancelPending();
            SetState(null, false, "Lyrics disabled in Settings", false);
            return;
        }

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Title))
        {
            CancelPending();
            SetState(null, false, null, false);
            return;
        }

        var key = BuildKey(snapshot);
        if (!force && key == _currentKey)
        {
            return;
        }

        CancelPending();
        var generation = ++_generation;
        var cts = new CancellationTokenSource();
        _cts = cts;
        _currentKey = key;
        SetState(null, true, null, false);
        DebugLog.Write($"lyrics load start key={key}");

        try
        {
            if (!force)
            {
                DebugLog.Write("lyrics cache read");
                var cached = await ReadCacheAsync(key);
                DebugLog.Write($"lyrics cache result={(cached is not null)}");
                if (generation != _generation || cts.IsCancellationRequested)
                {
                    return;
                }

                if (cached is not null)
                {
                    SetState(cached, false, null, false);
                    return;
                }
            }

            DebugLog.Write("lyrics fetch start");
            var document = await FetchAsync(snapshot, cts.Token);
            DebugLog.Write($"lyrics fetch result={(document is not null)}");
            if (generation != _generation || cts.IsCancellationRequested)
            {
                return;
            }

            if (document is null)
            {
                SetState(null, false, "No lyrics found for this track", false);
                return;
            }

            await WriteCacheAsync(key, document);
            if (generation != _generation || cts.IsCancellationRequested)
            {
                return;
            }

            SetState(document, false, null, false);
        }
        catch (OperationCanceledException)
        {
            // HttpClient timeouts also surface as OperationCanceledException.
            // Only treat it as a real error when our own request token was not cancelled.
            if (generation == _generation && !cts.IsCancellationRequested)
            {
                SetState(null, false, "Couldn't reach LRCLIB", true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lyrics fetch failed: {ex.Message}");
            if (generation == _generation && !cts.IsCancellationRequested)
            {
                SetState(null, false, "Couldn't reach LRCLIB", true);
            }
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
            }

            cts.Dispose();
        }
    }

    public void ClearCache()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDirectory, "*.json"))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Clearing lyrics cache failed: {ex.Message}");
        }
    }

    private void SetState(LyricsDocument? document, bool loading, string? message, bool error)
    {
        Current = document;
        IsLoading = loading;
        StatusMessage = message;
        HasError = error;
        Changed?.Invoke();
    }

    private void CancelPending()
    {
        _generation++;
        var cts = _cts;
        _cts = null;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
            // Ignore.
        }
    }

    private void OnSettingsChanged()
    {
        var enabled = _settings.Current.LyricsEnabled;
        if (enabled == _lastEnabled)
        {
            return;
        }

        _lastEnabled = enabled;
        _currentKey = string.Empty;

        if (!enabled)
        {
            CancelPending();
            SetState(null, false, "Lyrics disabled in Settings", false);
            return;
        }

        if (_lastSnapshot is not null)
        {
            _ = LoadAsync(_lastSnapshot, force: true);
        }
        else
        {
            SetState(null, false, null, false);
        }
    }

    private static string BuildKey(MediaSnapshot snapshot)
    {
        static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
        var duration = (int)Math.Round(snapshot.Duration.TotalSeconds);
        return $"{Normalize(snapshot.Title)}|{Normalize(snapshot.Artist)}|{Normalize(snapshot.Album)}|{duration}";
    }

    private string CachePath(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var name = Convert.ToHexString(hash)[..24].ToLowerInvariant();
        return Path.Combine(_cacheDirectory, name + ".json");
    }

    private async Task<LyricsDocument?> ReadCacheAsync(string key)
    {
        try
        {
            var path = CachePath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);
            if (entry?.Document is null)
            {
                return null;
            }

            if (entry.SavedAt < DateTime.UtcNow.AddDays(-30))
            {
                return null;
            }

            return entry.Document;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Reading lyrics cache failed: {ex.Message}");
            return null;
        }
    }

    private async Task WriteCacheAsync(string key, LyricsDocument document)
    {
        try
        {
            var entry = new CacheEntry
            {
                SavedAt = DateTime.UtcNow,
                Document = document
            };
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(CachePath(key), json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Writing lyrics cache failed: {ex.Message}");
        }
    }

    private async Task<LyricsDocument?> FetchAsync(MediaSnapshot snapshot, CancellationToken token)
    {
        var title = snapshot.Title.Trim();
        var artist = snapshot.Artist.Equals("Unknown artist", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : snapshot.Artist.Trim();
        var album = snapshot.Album.Trim();
        var duration = (int)Math.Round(snapshot.Duration.TotalSeconds);

        var getUrl =
            "https://lrclib.net/api/get" +
            $"?artist_name={Uri.EscapeDataString(artist)}" +
            $"&track_name={Uri.EscapeDataString(title)}" +
            $"&album_name={Uri.EscapeDataString(album)}" +
            $"&duration={duration}";

        DebugLog.Write($"lyrics get {getUrl}");

        using (var response = await _http.GetAsync(getUrl, token))
        {
            DebugLog.Write($"lyrics get status={(int)response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(token);
                var dto = JsonSerializer.Deserialize<LrclibResponse>(json, JsonOptions);
                var document = ToDocument(dto);
                if (document is not null)
                {
                    return document;
                }
            }
        }

        var query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
        var searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";
        DebugLog.Write($"lyrics search {searchUrl}");
        using (var response = await _http.GetAsync(searchUrl, token))
        {
            DebugLog.Write($"lyrics search status={(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            var results = JsonSerializer.Deserialize<List<LrclibResponse>>(json, JsonOptions);
            if (results is null || results.Count == 0)
            {
                return null;
            }

            var best = results
                .Where(result =>
                    !string.IsNullOrWhiteSpace(result.SyncedLyrics) ||
                    !string.IsNullOrWhiteSpace(result.PlainLyrics) ||
                    result.Instrumental)
                .OrderBy(result => Score(result, title, artist, duration))
                .FirstOrDefault();

            return best is null ? null : ToDocument(best);
        }
    }

    private static LyricsDocument? ToDocument(LrclibResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.SyncedLyrics))
        {
            var lines = ParseLrc(response.SyncedLyrics);
            if (lines.Count > 0)
            {
                return new LyricsDocument
                {
                    IsSynced = true,
                    Lines = lines,
                    PlainLyrics = response.PlainLyrics,
                    TrackTitle = response.TrackName,
                    ArtistName = response.ArtistName
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(response.PlainLyrics))
        {
            return new LyricsDocument
            {
                IsSynced = false,
                PlainLyrics = response.PlainLyrics,
                TrackTitle = response.TrackName,
                ArtistName = response.ArtistName
            };
        }

        if (response.Instrumental)
        {
            return new LyricsDocument
            {
                IsInstrumental = true,
                TrackTitle = response.TrackName,
                ArtistName = response.ArtistName
            };
        }

        return null;
    }

    private static List<LyricsLine> ParseLrc(string lrc)
    {
        var lines = new List<LyricsLine>();
        var offset = TimeSpan.Zero;

        var offsetMatch = OffsetRegex.Match(lrc);
        if (offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out var offsetMilliseconds))
        {
            offset = TimeSpan.FromMilliseconds(offsetMilliseconds);
        }

        foreach (var raw in lrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = TimestampRegex.Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex.Replace(raw, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var milliseconds = 0;

                if (match.Groups[3].Success)
                {
                    var fraction = match.Groups[3].Value;
                    milliseconds = fraction.Length switch
                    {
                        1 => int.Parse(fraction, CultureInfo.InvariantCulture) * 100,
                        2 => int.Parse(fraction, CultureInfo.InvariantCulture) * 10,
                        _ => int.Parse(fraction, CultureInfo.InvariantCulture)
                    };
                }

                var time = TimeSpan.FromMinutes(minutes) +
                           TimeSpan.FromSeconds(seconds) +
                           TimeSpan.FromMilliseconds(milliseconds) +
                           offset;

                if (time < TimeSpan.Zero)
                {
                    time = TimeSpan.Zero;
                }

                lines.Add(new LyricsLine
                {
                    TimeSeconds = time.TotalSeconds,
                    Text = text
                });
            }
        }

        return lines.OrderBy(line => line.TimeSeconds).ToList();
    }

    private static int Score(LrclibResponse response, string title, string artist, int duration)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(response.SyncedLyrics))
        {
            score -= 5;
        }

        if (!string.IsNullOrWhiteSpace(title) &&
            response.TrackName?.Contains(title, StringComparison.OrdinalIgnoreCase) == true)
        {
            score -= 20;
        }

        if (!string.IsNullOrWhiteSpace(artist) &&
            response.ArtistName?.Contains(artist, StringComparison.OrdinalIgnoreCase) == true)
        {
            score -= 20;
        }

        var responseDuration = response.Duration > 0 ? response.Duration : duration;
        score += (int)Math.Min(Math.Abs(responseDuration - duration), 60);
        return score;
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        CancelPending();
        _http.Dispose();
    }

    private sealed class CacheEntry
    {
        public DateTime SavedAt { get; set; }
        public LyricsDocument? Document { get; set; }
    }

    private sealed class LrclibResponse
    {
        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("albumName")]
        public string? AlbumName { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("instrumental")]
        public bool Instrumental { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }
    }
}
