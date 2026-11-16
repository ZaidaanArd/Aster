using Aster.Platform.Linux;
using Aster.UI;
using Avalonia;

namespace Aster.Desktop.Linux.Wayland;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var factory = new LinuxPlatformFactory(useX11: false);
        using var single = factory.CreateSingleInstance("AsterLabAvalonia.Wayland");
        if (!single.IsFirstInstance)
        {
            return 1;
        }

        App.PlatformFactory = factory;
        App.StartupClock.Start();
        return AppBuilder.Configure<App>()
            .UseWayland()
            .UseSkia()
            .UseHarfBuzz()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
