using Aster.Core.Interfaces;

namespace Aster.Platform.Linux.System;

public sealed class LinuxAutostart : IAutostart
{
    private readonly string _directory;

    public LinuxAutostart(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart");
    }

    private string FilePath => Path.Combine(_directory, "aster-lab-avalonia.desktop");

    public bool IsEnabled => File.Exists(FilePath);

    public bool SetEnabled(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            if (!enabled)
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
                return true;
            }

            var exe = ResolveExecutable();
            var escaped = exe.Contains('"') ? exe.Replace("\"", "\\\"") : exe;
            var content =
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Aster Lab Avalonia\n" +
                $"Exec=\"{escaped}\"\n" +
                "Terminal=false\n" +
                "X-GNOME-Autostart-enabled=true\n";
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, content);
            File.Move(temp, FilePath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveExecutable()
    {
        var exe = Environment.ProcessPath;
        if (exe is null)
        {
            return "Aster.Avalonia";
        }

        if (string.Equals(
                Path.GetFileNameWithoutExtension(exe),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var entry = global::System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrWhiteSpace(entry))
            {
                return entry;
            }
        }

        return exe;
    }
}
