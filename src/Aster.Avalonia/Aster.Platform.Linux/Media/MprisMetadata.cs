namespace Aster.Platform.Linux.Media;

public sealed record MprisMetadata(
    string Title,
    string Artist,
    string Album,
    TimeSpan Duration,
    string? ArtUrl,
    string TrackId);
