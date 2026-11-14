using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Aster.Avalonia.Composition;
using Aster.Avalonia.Platform;
using Aster.Avalonia.Services;
using Aster.Avalonia.ViewModels;
using Aster.Core.Logic;
using Aster.Core.Settings;
using Aster.Platform.Windows;
using Aster.Platform.Windows.Window;

namespace Aster.Avalonia.Views;

public partial class MainWindow : Window
{
    public static Stopwatch StartupClock { get; } = Stopwatch.StartNew();

    private readonly FileAppLog _log;
    private readonly JsonSettingsStore _settings;
    private readonly ShellViewModel _state;
    private readonly WindowMotionController _motion;
    private readonly WindowsWindowPlacement _placement;
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();

        var composition = new CompositionRoot();
        _log = composition.Log;
        _settings = composition.Settings;
        _placement = composition.Placement;

        _state = new ShellViewModel();
        DataContext = _state;
        _motion = new WindowMotionController(this, _state, _placement, _settings, _log);

        WindowsHud.Attach(this);
        Opened += async (_, _) =>
        {
            await _settings.LoadAsync();
            PositionAtTopCenter();
            WriteReadyFile();
            _motion.Start();
        };
    }

    public WindowMotionController Motion => _motion;

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
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        if (IsVisible)
        {
            _settingsWindow.Show(this);
        }
        else
        {
            _settingsWindow.Show();
        }
    }
}
