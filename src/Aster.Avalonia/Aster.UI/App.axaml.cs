using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aster.Core;
using Aster.Core.Logging;
using Aster.UI.Abstractions;
using Aster.UI.Views;

namespace Aster.UI;

public partial class App : Application
{
    public static IPlatformFactory PlatformFactory { get; set; } = null!;
    public static Stopwatch StartupClock { get; } = new();

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

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (PlatformFactory is null)
            {
                throw new InvalidOperationException("App.PlatformFactory was not set by the head.");
            }

            var window = new MainWindow(PlatformFactory);
            desktop.MainWindow = window;
            desktop.ShutdownRequested += OnShutdownRequested;

            if (desktop.Args?.Contains("--settings") == true)
            {
                Dispatcher.UIThread.Post(window.ShowSettings);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private bool _flushStarted;

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_flushStarted)
        {
            return;
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not MainWindow main)
        {
            return;
        }

        _flushStarted = true;
        e.Cancel = true;
        try
        {
            await main.FlushSettingsAsync();
        }
        finally
        {
            desktop.Shutdown();
        }
    }

    private static void LogCrash(string message, Exception? exception)
    {
        try
        {
            new FileAppLog(AppPaths.LogPath).Error(message, exception);
        }
        catch
        {
            // Crash logging must never crash the process.
        }
    }
}
