using Avalonia;
using Aster.Platform.Windows;

namespace Aster.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var single = new SingleInstance("AsterLabAvalonia.SingleInstance");
        if (!single.IsFirstInstance)
        {
            return 1;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
