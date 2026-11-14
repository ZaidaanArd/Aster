using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aster.Avalonia.ViewModels;

public partial class MediaViewModel : ObservableObject
{
    private readonly IMediaSession _media;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _progressTimer;
    private string _artKey = string.Empty;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _sourceApp = string.Empty;
    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private Bitmap? _artworkImage;

    public MediaViewModel(IMediaSession media, IUiDispatcher dispatcher, IAppLog log)
    {
        _media = media;
        _dispatcher = dispatcher;
        _log = log;

        _media.Changed += OnMediaChanged;
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += (_, _) => UpdateProgress(_media.Current);
        Apply(_media.Current);
    }

    [RelayCommand]
    private Task PlayPauseAsync() => _media.PlayPauseAsync();

    [RelayCommand]
    private Task NextAsync() => _media.NextAsync();

    [RelayCommand]
    private Task PreviousAsync() => _media.PreviousAsync();

    [RelayCommand]
    private Task SeekAsync(double seconds) => _media.SeekAsync(TimeSpan.FromSeconds(seconds));

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot) =>
        _dispatcher.Post(() => Apply(snapshot));

    private void Apply(MediaSnapshot snapshot)
    {
        HasSession = snapshot.HasSession;
        IsPlaying = snapshot.Playback == PlaybackState.Playing;
        Title = string.IsNullOrWhiteSpace(snapshot.Title) ? "Nothing playing" : snapshot.Title;
        Artist = !string.IsNullOrWhiteSpace(snapshot.Artist)
            ? snapshot.Artist
            : !string.IsNullOrWhiteSpace(snapshot.Album)
                ? snapshot.Album
                : "Unknown artist";
        SourceApp = snapshot.SourceApp;
        DurationSeconds = Math.Max(0, snapshot.Duration.TotalSeconds);
        DurationText = Format(snapshot.Duration);
        UpdateProgress(snapshot);

        if (!string.Equals(snapshot.ArtworkKey, _artKey, StringComparison.Ordinal))
        {
            _artKey = snapshot.ArtworkKey;
            _ = DecodeAndApplyAsync(snapshot.ArtworkBytes, snapshot.ArtworkKey);
        }

        if (IsPlaying)
        {
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private void UpdateProgress(MediaSnapshot snapshot)
    {
        var position = _media.EstimatedPosition(DateTimeOffset.Now);
        PositionSeconds = Math.Max(0, position.TotalSeconds);
        PositionText = Format(position);
        Progress = snapshot.Duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(position.TotalSeconds / snapshot.Duration.TotalSeconds, 0, 1);
    }

    private async Task DecodeAndApplyAsync(byte[]? bytes, string key)
    {
        var bitmap = await Task.Run(() => Decode(bytes));
        _dispatcher.Post(() =>
        {
            if (!string.Equals(_artKey, key, StringComparison.Ordinal))
            {
                bitmap?.Dispose();
                return;
            }

            ArtworkImage?.Dispose();
            ArtworkImage = bitmap;
        });
    }

    private static string Format(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static Bitmap? Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, 256);
        }
        catch
        {
            return null;
        }
    }
}
