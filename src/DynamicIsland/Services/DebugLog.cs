using System.IO;

namespace DynamicIsland.Services;

internal static class DebugLog
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "dynamic-island-debug.log");

    public static void Write(string message)
    {
#if DEBUG
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never break the app.
        }
#endif
    }
}
