namespace Aster.Core.Models;

public enum PlaybackState
{
    None,
    Playing,
    Paused,
    Stopped
}

public sealed record MediaSnapshot(
    string Title,
    string Artist,
    string Album,
    byte[]? ArtworkBytes,
    string ArtworkKey,
    PlaybackState Playback,
    TimeSpan Position,
    TimeSpan Duration,
    string SourceApp,
    DateTimeOffset UpdatedAt)
{
    public static MediaSnapshot Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        string.Empty,
        PlaybackState.None,
        TimeSpan.Zero,
        TimeSpan.Zero,
        string.Empty,
        DateTimeOffset.MinValue);

    public bool HasSession => Playback is PlaybackState.Playing or PlaybackState.Paused;
}
