namespace Aster.Core;

public static class AppPaths
{
    public static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AsterLab",
        "Avalonia");

    public static string LogPath => Path.Combine(BaseDirectory, "logs", "app.log");
    public static string SettingsPath => Path.Combine(BaseDirectory, "settings.json");
}
