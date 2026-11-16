using Avalonia;
using Aster.Core.System;

namespace Aster.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var single = new MutexSingleInstance("AsterLabAvalonia.SingleInstance");
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
