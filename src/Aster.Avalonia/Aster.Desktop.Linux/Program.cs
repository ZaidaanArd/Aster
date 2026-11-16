using Aster.Platform.Linux;
using Aster.UI;
using Avalonia;

namespace Aster.Desktop.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var factory = new LinuxPlatformFactory();
        using var single = factory.CreateSingleInstance("AsterLabAvalonia.SingleInstance");
        if (!single.IsFirstInstance)
        {
            return 1;
        }

        App.PlatformFactory = factory;
        App.StartupClock.Start();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
