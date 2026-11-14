using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Aster.Avalonia.Platform;
using Aster.Core.Logic;
using Aster.Platform.Windows;
using Aster.Platform.Windows.Window;

namespace Aster.Avalonia.Views;

public partial class MainWindow : Window
{
    public static Stopwatch StartupClock { get; } = Stopwatch.StartNew();

    private readonly WindowsWindowPlacement _placement;
    private SettingsWindow? _settings;

    public MainWindow()
    {
        InitializeComponent();
        var log = new FileAppLog(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AsterLab", "Avalonia", "logs", "app.log"));
        _placement = new WindowsWindowPlacement(log);

        WindowsHud.Attach(this);
        Opened += (_, _) =>
        {
            PositionAtTopCenter();
            WriteReadyFile();
        };
    }

    private void PositionAtTopCenter()
    {
        var handle = TryGetPlatformHandle();
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (handle is null || screen is null)
        {
            return;
        }

        var dpi = _placement.GetDpi(handle.Handle);
        var scaling = dpi / 96.0;
        var width = (int)Math.Round(Width * scaling);
        var height = (int)Math.Round(Height * scaling);
        var x = WindowGeometry.CenteredLeft(screen.Bounds.X, screen.Bounds.Width, Width, scaling);

        _placement.SetBounds(handle.Handle, x, screen.Bounds.Y, width, height);
    }

    private static void WriteReadyFile()
    {
        var path = Environment.GetEnvironmentVariable("ASTER_READY_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, StartupClock.ElapsedMilliseconds.ToString());
        }
    }

    public void ShowSettings()
    {
        if (_settings is { IsVisible: true })
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        if (IsVisible)
        {
            _settings.Show(this);
        }
        else
        {
            _settings.Show();
        }
    }
}
