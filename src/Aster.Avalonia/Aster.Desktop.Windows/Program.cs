using Aster.Platform.Windows;
using Aster.UI;
using Avalonia;

namespace Aster.Desktop.Windows;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var factory = new WindowsPlatformFactory();
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
