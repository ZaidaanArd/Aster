namespace Aster.Platform.Linux.Media;

public static class LinuxArtworkReader
{
    private const int MaxBytes = 5 * 1024 * 1024;

    public static async Task<byte[]?> ReadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = url.IndexOf(',');
                return comma < 0 ? null : Convert.FromBase64String(url[(comma + 1)..]);
            }

            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var path = new Uri(url).LocalPath;
                return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var bytes = await http.GetByteArrayAsync(url, ct);
                return bytes.Length <= MaxBytes ? bytes : null;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
