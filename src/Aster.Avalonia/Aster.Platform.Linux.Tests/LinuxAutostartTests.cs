using Aster.Platform.Linux.System;

namespace Aster.Platform.Linux.Tests;

public class LinuxAutostartTests
{
    [Fact]
    public void WritesAndRemovesDesktopEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aster-autostart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var autostart = new LinuxAutostart(dir);
            Assert.False(autostart.IsEnabled);

            Assert.True(autostart.SetEnabled(true));
            Assert.True(autostart.IsEnabled);
            var path = Path.Combine(dir, "aster-lab-avalonia.desktop");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("Type=Application", content);
            Assert.Contains("Exec=", content);
            Assert.DoesNotContain("Exec=\"dotnet\"", content);

            Assert.True(autostart.SetEnabled(false));
            Assert.False(autostart.IsEnabled);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
