using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aster.Avalonia.Views;

namespace Aster.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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
