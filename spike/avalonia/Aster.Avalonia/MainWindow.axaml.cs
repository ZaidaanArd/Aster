using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Aster.Avalonia.Platform;

namespace Aster.Avalonia;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TrayIcon? _tray;
    private SettingsWindow? _settings;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            PositionAtTopCenter();
            WriteReadyFile();
        };

        if (OperatingSystem.IsWindows())
        {
            WindowsHud.Attach(
                this,
                point => HitTest.IsInteractive(
                    point.X,
                    point.Y,
                    Bounds.Width,
                    Bounds.Height,
                    RenderScaling));
        }

        try
        {
            _tray = SetupTray();
        }
        catch (Exception ex) when (!OperatingSystem.IsWindows())
        {
            Trace.TraceWarning($"Tray icon unavailable on this host: {ex.Message}");
        }

        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm");
        _clock.Start();
    }

    private static void WriteReadyFile()
    {
        var path = Environment.GetEnvironmentVariable("ASTER_READY_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, Program.StartupClock.ElapsedMilliseconds.ToString());
        }
    }

    private void PositionAtTopCenter()
    {
        // Screens.Primary can be null on X11 servers without a primary-output hint
        // (for example Xvfb), so fall back to the first reported screen.
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var left = WindowPlacement.CenteredLeft(
            screen.Bounds.X,
            screen.Bounds.Width,
            Width,
            screen.Scaling);

        Position = new PixelPoint(left, screen.Bounds.Y);
    }

    private TrayIcon? SetupTray()
    {
        var menu = new NativeMenu();

        var toggle = new NativeMenuItem("Show/Hide");
        toggle.Click += (_, _) => IsVisible = !IsVisible;

        var settings = new NativeMenuItem("Settings...");
        settings.Click += (_, _) => ShowSettings();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.Shutdown();

        menu.Items.Add(toggle);
        menu.Items.Add(settings);
        menu.Items.Add(quit);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Aster.Avalonia/Assets/tray.ico"))),
            ToolTipText = "Aster Lab Avalonia",
            Menu = menu,
            IsVisible = true
        };

        if (Application.Current is { } app)
        {
            TrayIcon.SetIcons(app, new TrayIcons { tray });
        }

        return tray;
    }

    private void ShowSettings()
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
            // Showing an owned window requires a visible owner.
            _settings.Show();
        }
    }
}
