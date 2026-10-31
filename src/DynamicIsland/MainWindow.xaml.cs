using System.Windows;
using System.Windows.Interop;
using DynamicIsland.Controls;
using DynamicIsland.Interop;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly FullscreenWatcher _fullscreen;
    private readonly FocusService _focus;
    private readonly LyricsService _lyrics;
    private readonly WaterService _water;
    private readonly IslandView _island;

    private HwndSource? _source;
    private SettingsWindow? _settingsWindow;
    private bool _userHidden;
    private bool _fullscreenHidden;

    public MainWindow(
        SettingsService settings,
        MediaService media,
        AudioService audio,
        BrightnessService brightness,
        BatteryService battery,
        NetworkService network,
        FullscreenWatcher fullscreen,
        FocusService focus,
        LyricsService lyrics,
        WaterService water,
        CameraService camera)
    {
        _settings = settings;
        _fullscreen = fullscreen;
        _focus = focus;
        _lyrics = lyrics;
        _water = water;

        InitializeComponent();

        _water.IsSuppressed = true;
        IsVisibleChanged += OnIslandVisibilityChanged;

        _island = new IslandView(settings, media, audio, brightness, battery, network, focus, lyrics, water, camera);
        _island.SettingsRequested += ShowSettings;
        IslandHost.Children.Add(_island);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _fullscreen.Changed += OnFullscreenChanged;
    }

    public bool IslandVisible => IsVisible;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        DebugLog.Write("source initialized");

        NativeMethods.AddExtendedStyle(
            hwnd,
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        var cornerPreference = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref cornerPreference,
            sizeof(int));

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        PositionWindow();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();

        if (_fullscreen.IsFullscreen && _settings.Current.HideInFullscreen)
        {
            _fullscreenHidden = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _island.SettingsRequested -= ShowSettings;
        _fullscreen.Changed -= OnFullscreenChanged;
        IsVisibleChanged -= OnIslandVisibilityChanged;
        _source?.RemoveHook(WndProc);
    }

    private void OnIslandVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _water.IsSuppressed = !IsVisible;
    }

    private void PositionWindow()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var monitor = NativeMethods.GetPrimaryMonitorRect();
        var x = monitor.Left + (monitor.Width - rect.Width) / 2;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            monitor.Top,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(NativeMethods.MA_NOACTIVATE);
        }

        if (msg != NativeMethods.WM_NCHITTEST)
        {
            return IntPtr.Zero;
        }

        var hit = lParam.ToInt64();
        var screenX = (short)(hit & 0xFFFF);
        var screenY = (short)((hit >> 16) & 0xFFFF);

        try
        {
            var point = PointFromScreen(new Point(screenX, screenY));
            var origin = _island.TranslatePoint(new Point(0, 0), this);
            var islandRect = new Rect(origin, new Size(_island.ActualWidth, _island.ActualHeight));

            handled = true;
            return islandRect.Contains(point)
                ? new IntPtr(NativeMethods.HTCLIENT)
                : new IntPtr(NativeMethods.HTTRANSPARENT);
        }
        catch
        {
            handled = true;
            return new IntPtr(NativeMethods.HTTRANSPARENT);
        }
    }

    public bool ToggleVisibility()
    {
        if (IsVisible)
        {
            DebugLog.Write("toggle: hiding by user");
            _userHidden = true;
            Hide();
        }
        else
        {
            DebugLog.Write("toggle: showing by user");
            _userHidden = false;
            ShowIsland();
        }

        return IsVisible;
    }

    private void ShowIsland()
    {
        DebugLog.Write("show island");

        if (!IsVisible)
        {
            Show();
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        DebugLog.Write($"fullscreen changed: {fullscreen} hide={_settings.Current.HideInFullscreen} visible={IsVisible} fullscreenHidden={_fullscreenHidden} userHidden={_userHidden}");

        if (!_settings.Current.HideInFullscreen)
        {
            return;
        }

        if (fullscreen)
        {
            if (!IsVisible)
            {
                return;
            }

            _fullscreenHidden = true;
            Hide();
        }
        else if (_fullscreenHidden)
        {
            _fullscreenHidden = false;
            if (!_userHidden)
            {
                ShowIsland();
            }
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        _settings.Current.StartWithWindows = enabled;
        StartupService.SetEnabled(enabled);
        _settings.Save();
    }

    public void SetMinimalNotch(bool enabled)
    {
        _settings.Current.MinimalNotch = enabled;
        _settings.Save();
        _island.ApplySettings();
    }

    public void SetShowMascot(bool enabled)
    {
        _settings.Current.ShowMascot = enabled;
        _settings.Save();
        _island.ApplySettings();
    }

    public void SetHideInFullscreen(bool enabled)
    {
        _settings.Current.HideInFullscreen = enabled;
        _settings.Save();

        if (!enabled && _fullscreenHidden)
        {
            _fullscreenHidden = false;
            if (!_userHidden)
            {
                ShowIsland();
            }
        }
    }

    public void SetGlassBackground(bool enabled)
    {
        _settings.Current.GlassBackground = enabled;
        _settings.Save();
        _island.ApplySettings();
    }

    public void SetBouncySpring(bool enabled)
    {
        _settings.Current.BouncySpring = enabled;
        _settings.Save();
        _island.ApplySettings();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _lyrics, _water, this, _island.CurrentPanelHeight);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowAbout()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        var versionText = version is null ? "0.1.0" : version.ToString(3);

        MessageBox.Show(
            $"Dynamic Island for Windows\nVersion {versionText}\n\n" +
            "A small notch for the top of your screen.\n" +
            "Media controls, clock, battery, Wi-Fi, volume and brightness.",
            "About Dynamic Island",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
