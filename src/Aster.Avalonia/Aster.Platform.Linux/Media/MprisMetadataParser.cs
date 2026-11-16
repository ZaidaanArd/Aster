namespace Aster.Platform.Linux.Media;

public static class MprisMetadataParser
{
    public static MprisMetadata Parse(IReadOnlyDictionary<string, object?> metadata)
    {
        var rawTitle = GetString(metadata, "xesam:title");
        var title = string.IsNullOrWhiteSpace(rawTitle) ? "Unknown track" : rawTitle;
        var artist = string.Join(", ", GetStrings(metadata, "xesam:artist"));
        var album = GetString(metadata, "xesam:album") ?? string.Empty;
        var duration = GetLong(metadata, "mpris:length") is { } microseconds && microseconds > 0
            ? TimeSpan.FromMicroseconds(microseconds)
            : TimeSpan.Zero;
        var artUrl = GetString(metadata, "mpris:artUrl");
        var trackId = GetString(metadata, "mpris:trackid") ?? string.Empty;

        return new MprisMetadata(title, artist, album, duration, artUrl, trackId);
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value as string : null;

    private static string[] GetStrings(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value)
            ? value switch
            {
                string[] strings => strings,
                string single => new[] { single },
                _ => Array.Empty<string>()
            }
            : Array.Empty<string>();

    private static long? GetLong(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value)
            ? value switch
            {
                long number => number,
                int number => number,
                uint number => number,
                _ => null
            }
            : null;
}
