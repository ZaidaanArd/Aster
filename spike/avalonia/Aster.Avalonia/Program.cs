using System.Diagnostics;
using Avalonia;

namespace Aster.Avalonia;

internal static class Program
{
    public static readonly Stopwatch StartupClock = new();

    [STAThread]
    public static void Main(string[] args)
    {
        StartupClock.Start();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
