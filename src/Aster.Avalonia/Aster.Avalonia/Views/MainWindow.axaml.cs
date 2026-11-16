using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Aster.Avalonia.Composition;
using Aster.Avalonia.Platform;
using Aster.Avalonia.Services;
using Aster.Avalonia.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Aster.Core.Settings;
using Aster.Platform.Windows.Media;
using Aster.Platform.Windows.System;
using Aster.Platform.Windows.Window;

namespace Aster.Avalonia.Views;

public partial class MainWindow : Window
{
    private static readonly IBrush GlassBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#DE14141B"), 0),
            new GradientStop(Color.Parse("#F007070A"), 1)
        }
    };

    private static readonly IBrush SolidBrush = new SolidColorBrush(Color.Parse("#F2070708"));

    public static Stopwatch StartupClock { get; } = Stopwatch.StartNew();

    private readonly IAppLog _log;
    private readonly JsonSettingsStore _settings;
    private readonly ShellViewModel _state;
    private readonly WindowMotionController _motion;
    private readonly WindowsWindowPlacement _placement;
    private readonly RegistryAutostart _autostart;
    private readonly TrayService? _tray;
    private readonly WindowsFullscreenWatcher _fullscreen;
    private readonly WindowsBatteryStatus _battery;
    private readonly Aster.Core.System.NetworkStatus _network;
    private readonly SmtcMediaSession _mediaSession;
    private readonly MediaViewModel _media;
    private readonly DispatcherTimer _clockTimer;
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private bool _userHidden;
    private bool _fullscreenHidden;
    private bool _initialized;
    private bool _openSettingsWhenReady;

    public MainWindow()
    {
        InitializeComponent();

        var composition = new CompositionRoot();
        _log = composition.Log;
        _settings = composition.Settings;
        _placement = composition.Placement;

        var dispatcher = new AvaloniaUiDispatcher();
        _battery = new WindowsBatteryStatus(dispatcher, _log);
        _network = new Aster.Core.System.NetworkStatus(dispatcher, _log);
        _mediaSession = new SmtcMediaSession(dispatcher, _log);
        _media = new MediaViewModel(_mediaSession, dispatcher, _log);
        _state = new ShellViewModel(_battery, _network, _media);
        DataContext = _state;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => _state.UpdateClock(DateTimeOffset.Now);
        _clockTimer.Start();
        _motion = new WindowMotionController(this, _state, _placement, _settings, _log);
        Screens.Changed += (_, _) =>
        {
            PositionAtTopCenter();
            _motion.Reapply();
        };
        ExpandedMedia.SettingsRequested += (_, _) => ShowSettings();
        ExpandedMedia.CloseRequested += (_, _) =>
        {
            _motion.SuppressHover();
            _motion.SetExpanded(false);
        };

        _autostart = new RegistryAutostart(_log);
        var commands = new TrayCommandHandler(
            toggleVisibility: () =>
            {
                if (IsVisible)
                {
                    _userHidden = true;
                    Hide();
                }
                else
                {
                    _userHidden = false;
                    ShowNoActivate();
                }
            },
            isAutostart: () => _autostart.IsEnabled,
            setAutostart: enabled =>
            {
                if (!_autostart.SetEnabled(enabled))
                {
                    return false;
                }

                var updated = _settings.Current with { StartWithWindows = enabled };
                _ = _settings.SaveAsync(updated);
                return true;
            },
            openSettings: ShowSettings,
            quit: () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
        try
        {
            _tray = new TrayService(commands, () => _autostart.IsEnabled);
        }
        catch (Exception ex)
        {
            _log.Error("Tray creation failed.", ex);
            _tray = null;
            _openSettingsWhenReady = true;
        }

        _fullscreen = new WindowsFullscreenWatcher(new AvaloniaUiDispatcher(), _log);
        _fullscreen.Changed += OnFullscreenChanged;

        WindowsHud.Attach(this);
        Opened += async (_, _) =>
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _log.Info("Aster Lab Avalonia started.");
            await _settings.LoadAsync();
            _settingsViewModel = new SettingsViewModel(_settings, _log);
            _settingsViewModel.Saved += (_, settings) => ApplySettings(settings);
            ApplySettings(_settings.Current);
            if (Environment.GetCommandLineArgs().Contains("--settings"))
            {
                ShowSettings();
            }

            if (_openSettingsWhenReady)
            {
                _openSettingsWhenReady = false;
                ShowSettings();
            }

            PositionAtTopCenter();
            WriteReadyFile();
            _motion.Start();
            _mediaSession.Start();
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

    private void OnFullscreenChanged(object? sender, bool fullscreen)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_settings.Current.HideInFullscreen)
            {
                return;
            }

            if (fullscreen && !_userHidden && !_fullscreenHidden)
            {
                _fullscreenHidden = true;
                Hide();
            }
            else if (!fullscreen && _fullscreenHidden)
            {
                _fullscreenHidden = false;
                if (!_userHidden)
                {
                    ShowNoActivate();
                }
            }
        });
    }

    private void ShowNoActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        PositionAtTopCenter();
    }

    private void ApplySettings(AppSettings settings)
    {
        _state.ApplySettings(settings);
        Island.Background = settings.GlassBackground ? GlassBrush : SolidBrush;
        GlassNoise.IsVisible = settings.GlassBackground;
        _motion.ApplySettings();

        if (!_autostart.SetEnabled(settings.StartWithWindows))
        {
            _log.Warn("Autostart update failed while applying settings.");
            if (_settingsViewModel is not null)
            {
                _settingsViewModel.StartWithWindows = _autostart.IsEnabled;
            }
        }

        _tray?.SetAutostartChecked(settings.StartWithWindows);

        if (!settings.HideInFullscreen && _fullscreenHidden)
        {
            _fullscreenHidden = false;
            if (!_userHidden)
            {
                ShowNoActivate();
            }
        }
        else if (settings.HideInFullscreen && _fullscreen.IsFullscreen && !_userHidden && !_fullscreenHidden)
        {
            _fullscreenHidden = true;
            Hide();
        }
    }

    public void ShowSettings()
    {
        if (_settingsViewModel is null)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsViewModel);
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var width = (int)Math.Round(_settingsWindow.Width * screen.Scaling);
            var height = (int)Math.Round(_settingsWindow.Height * screen.Scaling);
            var x = screen.Bounds.X + (screen.Bounds.Width - width) / 2;
            var y = screen.Bounds.Y + (int)Math.Round((WindowGeometry.ExpandedHeight + 8) * screen.Scaling);
            var maxY = screen.WorkingArea.Bottom - height;
            _settingsWindow.Position = new PixelPoint(x, Math.Min(y, maxY));
        }

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
