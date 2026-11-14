using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aster.Avalonia.Views;
using Aster.Platform.Windows;

namespace Aster.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            LogCrash("Dispatcher exception.", e.Exception);
    }

    private static void LogCrash(string message, Exception? exception)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            new FileAppLog(Path.Combine(appData, "AsterLab", "Avalonia", "logs", "app.log"))
                .Error(message, exception);
        }
        catch
        {
            // Crash logging must never crash the process.
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;

            if (desktop.Args?.Contains("--settings") == true)
            {
                Dispatcher.UIThread.Post(window.ShowSettings);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
