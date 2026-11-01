using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DynamicIsland.Interop;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public enum PanelMode
{
    Music,
    Camera,
    Shelf,
    Focus
}

public partial class IslandView : UserControl
{
    private const double PanelWidth = 500;
    private const double PanelHeight = 252;
    private const double MediaClosedWidth = 464;
    private const double MediaOpenWidth = 260;
    private const double LyricsPaneWidth = 204;

    private static readonly PanelMode[] AllModes =
    {
        PanelMode.Music,
        PanelMode.Camera,
        PanelMode.Shelf,
        PanelMode.Focus
    };

    private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x32, 0xD7, 0x4B));
    private static readonly Brush OrangeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush WhiteBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush FocusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x6B));
    private static readonly Brush FocusPausedBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xB8, 0x6B));
    private static readonly Brush LyricsButtonInactiveBrush = new SolidColorBrush(Color.FromRgb(0x94, 0x94, 0x9C));
    private static readonly Brush LyricsButtonActiveBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x7C, 0x9C, 0xFF));
    private static readonly Brush WaterBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0xB8, 0xFF));
    private static readonly Brush WaterTrackBrush = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PlayGlyphBrush = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x0A));

    private readonly SettingsService _settings;
    private readonly MediaService _media;
    private readonly AudioService _audio;
    private readonly BrightnessService _brightness;
    private readonly BatteryService _battery;
    private readonly NetworkService _network;
    private readonly FocusService _focus;
    private readonly FocusView _focusView;
    private readonly LyricsService _lyrics;
    private readonly LyricsView _lyricsView;
    private readonly WaterService _water;
    private readonly CameraService _camera;
    private readonly CameraView _cameraView;
    private readonly ShelfView _shelfView;
    private readonly SolidColorBrush _accentBrush = new(ArtworkAccent.Fallback);
    private readonly Border[] _waterBars;
    private readonly ScaleTransform[] _eqBars;
    private readonly double[] _eqLevels = { 0.52, 0.8, 0.38, 0.68, 0.58 };
    private readonly double[] _eqTargets = { 0.9, 0.4, 0.82, 0.5, 1.0 };

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _equalizerTimer;
    private readonly Random _random = new();

    private MediaSnapshot? _snapshot;
    private bool _expanded;
    private bool _hovering;
    private bool _hoverSuppressed;
    private bool _eyesShown;
    private bool _dozing;
    private bool _springRunning;
    private double _springDamping = 21;
    private bool _wasCharging;
    private bool _initialized;
    private bool _toastVisible;
    private int _toastToken;
    private Action? _toastClick;

    private bool _compactMediaVisible;
    private int _compactAnimationToken;
    private bool _vinylRunning;
    private Color _accentColor = ArtworkAccent.Fallback;
    private bool _accentApplied;

    private double _currentWidth = 300;
    private double _currentHeight = 38;
    private double _velocityWidth;
    private double _velocityHeight;
    private double _targetWidth = 300;
    private double _targetHeight = 38;
    private double _collapsedWidth = 300;
    private double _collapsedHeight = 38;
    private double _expandedWidth = PanelWidth;
    private long _lastFrameTicks;

    private DateTime _lastActivity = DateTime.Now;
    private PanelMode _mode = PanelMode.Music;
    private bool _modeAnimating;
    private DispatcherTimer? _modeAnimationTimer;
    private bool _syncingTabs;
    private bool _lyricsOpen;
    private bool _lyricsAnimating;
    private int _lyricsAnimationToken;
    private bool _shelfDragOutActive;
    private int _lastFocusSecond = -1;
    private bool _wasFocusRunning;

    public event Action? SettingsRequested;

    public double CurrentPanelHeight => PanelHeight;

    public IslandView(
        SettingsService settings,
        MediaService media,
        AudioService audio,
        BrightnessService brightness,
        BatteryService battery,
        NetworkService network,
        FocusService focus,
        LyricsService lyrics,
        WaterService water,
        CameraService camera)
    {
        _settings = settings;
        _media = media;
        _audio = audio;
        _brightness = brightness;
        _battery = battery;
        _network = network;
        _focus = focus;
        _lyrics = lyrics;
        _water = water;
        _camera = camera;

        InitializeComponent();

        _waterBars = new[]
        {
            WaterBar1, WaterBar2, WaterBar3, WaterBar4,
            WaterBar5, WaterBar6, WaterBar7, WaterBar8
        };
        _eqBars = new[] { EqBar1, EqBar2, EqBar3, EqBar4, EqBar5 };

        _focusView = new FocusView(focus);
        FocusHost.Children.Add(_focusView);

        _lyricsView = new LyricsView(media, lyrics, settings)
        {
            Width = LyricsPaneWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        LyricsPane.Children.Add(_lyricsView);

        _cameraView = new CameraView(camera, settings);
        CameraHost.Children.Add(_cameraView);

        _shelfView = new ShelfView();
        ShelfHost.Children.Add(_shelfView);

        _clockTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => TickClock();

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        _hoverTimer.Tick += (_, _) => CheckHover();

        _collapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(320)
        };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            CheckHover();
            if (!_hovering)
            {
                SetExpanded(false);
            }
        };

        _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        _idleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _idleTimer.Tick += (_, _) => UpdateIdleVisual();

        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_random.Next(2600, 5600))
        };
        _blinkTimer.Tick += (_, _) => Blink();

        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1700)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            HideToast();
        };

        _equalizerTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _equalizerTimer.Tick += (_, _) => TickEqualizer();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ApplySettings();
        SetClockText();
        UpdateBattery(_battery.Percent, _battery.IsCharging);
        UpdateNetwork(_network.IsConnected);
        _wasCharging = _battery.IsCharging;
        _snapshot = _media.Current;
        UpdateMediaUI(_snapshot);
        UpdateProgress();
        UpdateWaterWidget();
        UpdateCompactVisual(animate: false);
        ApplyAccentColor(ArtworkAccent.Extract(_snapshot?.Artwork));

        _media.SnapshotChanged += OnMediaSnapshot;
        _audio.VolumeChanged += OnVolumeChanged;
        _brightness.BrightnessChanged += OnBrightnessChanged;
        _battery.Changed += OnBatteryChanged;
        _network.Changed += OnNetworkChanged;
        _focus.Tick += OnFocusTick;
        _focus.StateChanged += OnFocusStateChanged;
        _focus.Completed += OnFocusCompleted;
        _water.ReminderDue += OnWaterReminderDue;
        _water.StateChanged += OnWaterStateChanged;
        _shelfView.ItemsChanged += OnShelfItemsChanged;
        _shelfView.DragOutChanged += OnShelfDragOutChanged;
        _camera.StatusChanged += OnCameraStatusChanged;

        _wasFocusRunning = _focus.IsRunning;
        UpdateTabVisuals();
        UpdateCameraActivation();
        UpdateShelfTabDot();
        IsVisibleChanged += OnIslandVisibilityChanged;

        _clockTimer.Start();
        _hoverTimer.Start();
        _progressTimer.Start();
        _idleTimer.Start();
        _blinkTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Stop();
        _hoverTimer.Stop();
        _collapseTimer.Stop();
        _progressTimer.Stop();
        _idleTimer.Stop();
        _blinkTimer.Stop();
        _toastTimer.Stop();
        _equalizerTimer.Stop();
        _modeAnimationTimer?.Stop();
        CompositionTarget.Rendering -= OnRendering;
        _springRunning = false;

        _media.SnapshotChanged -= OnMediaSnapshot;
        _audio.VolumeChanged -= OnVolumeChanged;
        _brightness.BrightnessChanged -= OnBrightnessChanged;
        _battery.Changed -= OnBatteryChanged;
        _network.Changed -= OnNetworkChanged;
        _focus.Tick -= OnFocusTick;
        _focus.StateChanged -= OnFocusStateChanged;
        _focus.Completed -= OnFocusCompleted;
        _water.ReminderDue -= OnWaterReminderDue;
        _water.StateChanged -= OnWaterStateChanged;
        _shelfView.ItemsChanged -= OnShelfItemsChanged;
        _shelfView.DragOutChanged -= OnShelfDragOutChanged;
        _camera.StatusChanged -= OnCameraStatusChanged;
        IsVisibleChanged -= OnIslandVisibilityChanged;
        _cameraView.SetActive(false);
        _initialized = false;
    }

    public void ApplySettings()
    {
        _collapsedWidth = _settings.Current.MinimalNotch ? 160 : 300;
        _collapsedHeight = _settings.Current.MinimalNotch ? 12 : 38;

        var glass = _settings.Current.GlassBackground;
        IslandBorder.Background = (Brush)FindResource(
            glass ? "IslandGlassBackground" : "IslandSolidBackground");
        ShadowBorder.Background = (Brush)FindResource(
            glass ? "IslandGlassShadow" : "IslandSolidShadow");
        LyricsPane.Background = (Brush)FindResource(
            glass ? "IslandGlassLyrics" : "IslandSolidLyrics");
        ToastBorder.Background = (Brush)FindResource(
            glass ? "IslandGlassToast" : "IslandSolidToast");
        GlassNoise.Visibility = glass ? Visibility.Visible : Visibility.Collapsed;
        GlassTopGlow.Visibility = glass ? Visibility.Visible : Visibility.Collapsed;
        GlassSpecular.Visibility = glass ? Visibility.Visible : Visibility.Collapsed;
        GlassRim.Visibility = glass ? Visibility.Visible : Visibility.Collapsed;
        ArtGlow.Opacity = glass ? 1 : 0.45;

        _springDamping = _settings.Current.BouncySpring ? 21.0 : 27.0;

        var radius = 22;
        IslandBorder.CornerRadius = new CornerRadius(0, 0, radius, radius);
        ShadowBorder.CornerRadius = new CornerRadius(0, 0, radius, radius);
        CollapsedLayer.Visibility = _settings.Current.MinimalNotch ? Visibility.Collapsed : Visibility.Visible;
        CollapsedLayer.IsHitTestVisible = !_expanded;

        if (!_expanded)
        {
            _targetWidth = _collapsedWidth;
            _targetHeight = _collapsedHeight;
            _currentWidth = _collapsedWidth;
            _currentHeight = _collapsedHeight;
            _velocityWidth = 0;
            _velocityHeight = 0;
            Width = _collapsedWidth;
            Height = _collapsedHeight;

            ExpandedLayer.Opacity = 0;
            CollapsedLayer.Opacity = _settings.Current.MinimalNotch ? 0 : 1;
            AmbientGlowHost.Opacity = 0;
            AmbientGlowHost.Visibility = Visibility.Collapsed;
        }

        if (!_settings.Current.ShowMascot)
        {
            ShowClock();
        }

        UpdateIdleVisual();
        UpdateWaterWidget();
        UpdateCompactVisual(animate: false);
    }

    private void CheckHover()
    {
        if (_shelfDragOutActive)
        {
            return;
        }

        try
        {
            if (PresentationSource.FromVisual(this) is null)
            {
                return;
            }

            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var origin = PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(this);
            var rect = new Rect(
                origin.X,
                origin.Y,
                ActualWidth * dpi.DpiScaleX,
                ActualHeight * dpi.DpiScaleY);

            var inside = rect.Contains(cursor.X, cursor.Y);
            if (inside == _hovering)
            {
                if (!inside)
                {
                    _hoverSuppressed = false;
                }

                return;
            }

            _hovering = inside;
            _lastActivity = DateTime.Now;

            if (inside)
            {
                if (_hoverSuppressed)
                {
                    return;
                }

                _collapseTimer.Stop();
                Wake();
                SetExpanded(true);
            }
            else
            {
                _hoverSuppressed = false;
                _collapseTimer.Stop();
                _collapseTimer.Start();
            }
        }
        catch
        {
            // Layout or DPI transitions can briefly invalidate the visual tree.
        }
    }

    private void SetExpanded(bool expanded)
    {
        if (_expanded == expanded)
        {
            return;
        }

        if (!expanded && _shelfDragOutActive)
        {
            return;
        }

        _expanded = expanded;
        CollapsedLayer.IsHitTestVisible = !expanded;

        _expandedWidth = PanelWidth;
        ExpandedLayer.Width = PanelWidth - 36;
        ExpandedLayer.Height = PanelHeight - 36;

        _targetWidth = expanded ? PanelWidth : _collapsedWidth;
        _targetHeight = expanded ? PanelHeight : _collapsedHeight;
        ExpandedLayer.IsHitTestVisible = expanded;

        if (expanded)
        {
            _equalizerTimer.Stop();
            _lastActivity = DateTime.Now;
            ShowClock();
            AmbientGlowHost.Visibility = Visibility.Visible;
            if (_focus.HasSession)
            {
                SetMode(PanelMode.Focus, animate: false);
            }

            UpdateMediaUI(_snapshot);
            UpdateProgress();
            UpdateTabVisuals();
        }
        else
        {
            UpdateCompactVisual(animate: false);
            UpdateEqualizerTimer();
        }

        UpdateCameraActivation();
        StartSpring();
    }

    private void OnIslandVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateCameraActivation();
        UpdateEqualizerTimer();
    }

    private void UpdateCameraActivation()
    {
        var active = _mode == PanelMode.Camera && _expanded && IsVisible;
        DebugLog.Write(
            $"camera activation mode={_mode} expanded={_expanded} visible={IsVisible} active={active}");
        _cameraView.SetActive(active);
        UpdateCameraDot();
    }

    private void OnCameraStatusChanged(CameraStatus status)
    {
        Dispatcher.BeginInvoke(new Action(UpdateCameraDot));
    }

    private void UpdateCameraDot()
    {
        var active = _mode == PanelMode.Camera && _expanded && IsVisible && _camera.IsLive;
        CameraDot.Opacity = active ? 1 : 0.22;
        CameraDot.Fill = _camera.IsLive ? GreenBrush : WaterBrush;
    }

    private void OnShelfItemsChanged()
    {
        UpdateShelfTabDot();
    }

    private void UpdateShelfTabDot()
    {
        ShelfTabDot.Opacity = _shelfView.Count > 0 ? 1 : 0;
    }

    private void OnShelfDragOutChanged(bool active)
    {
        _shelfDragOutActive = active;

        if (active)
        {
            _collapseTimer.Stop();
            return;
        }

        _lastActivity = DateTime.Now;
        CheckHover();
    }

    private void OnShelfDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        SetExpanded(true);
        SetMode(PanelMode.Shelf, animate: false);
        ShowDropOverlay(true);

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnShelfDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!_expanded)
        {
            SetExpanded(true);
            SetMode(PanelMode.Shelf, animate: false);
        }

        ShowDropOverlay(true);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnShelfDragLeave(object sender, DragEventArgs e)
    {
        ShowDropOverlay(false);
        e.Handled = true;
    }

    private void OnShelfDrop(object sender, DragEventArgs e)
    {
        ShowDropOverlay(false);

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        SetExpanded(true);
        SetMode(PanelMode.Shelf, animate: false);

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        var result = paths is null
            ? default
            : _shelfView.AddPaths(paths);

        if (result.Added > 0)
        {
            ShowToast(
                "\uE8B7",
                result.Added == 1
                    ? "File added to shelf"
                    : $"{result.Added} files added to shelf",
                null,
                1800);
        }
        else if (result.Full)
        {
            ShowToast("\uE8B7", "Shelf is full (8)", null, 2200);
        }
        else if (result.Rejected > 0)
        {
            ShowToast("\uE8B7", "Already on shelf", null, 1800);
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void ShowDropOverlay(bool show)
    {
        DropOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartSpring()
    {
        if (_springRunning)
        {
            return;
        }

        _springRunning = true;
        _lastFrameTicks = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastFrameTicks == 0)
        {
            _lastFrameTicks = now;
            return;
        }

        var dt = Math.Min((now - _lastFrameTicks) / (double)Stopwatch.Frequency, 0.05);
        _lastFrameTicks = now;

        var widthSettled = Step(ref _currentWidth, ref _velocityWidth, _targetWidth, dt, _springDamping);
        var heightSettled = Step(ref _currentHeight, ref _velocityHeight, _targetHeight, dt, _springDamping);

        Width = _currentWidth;
        Height = _currentHeight;

        var progress = Math.Clamp(
            (_currentWidth - _collapsedWidth) / Math.Max(1, _expandedWidth - _collapsedWidth),
            0,
            1);

        ExpandedLayer.Opacity = _expanded
            ? 1
            : Math.Clamp((progress - 0.18) / 0.82, 0, 1);
        CollapsedLayer.Opacity = _settings.Current.MinimalNotch ? 0 : Math.Clamp(1 - progress * 3, 0, 1);

        var glowOpacity = ExpandedLayer.Opacity * 0.9;
        if (glowOpacity > 0.01)
        {
            if (AmbientGlowHost.Visibility != Visibility.Visible)
            {
                AmbientGlowHost.Visibility = Visibility.Visible;
            }

            AmbientGlowHost.Opacity = glowOpacity;
        }
        else
        {
            AmbientGlowHost.Opacity = 0;
            AmbientGlowHost.Visibility = Visibility.Collapsed;
        }

        if (!widthSettled || !heightSettled)
        {
            return;
        }

        Width = _targetWidth;
        Height = _targetHeight;
        CompositionTarget.Rendering -= OnRendering;
        _springRunning = false;

        if (!_expanded)
        {
            ExpandedLayer.Opacity = 0;
            CollapsedLayer.Opacity = _settings.Current.MinimalNotch ? 0 : 1;
            UpdateIdleVisual();
        }
    }

    private static bool Step(
        ref double value,
        ref double velocity,
        double target,
        double dt,
        double damping)
    {
        const double stiffness = 240.0;

        var force = (target - value) * stiffness;
        velocity += force * dt;
        velocity *= Math.Exp(-damping * dt);
        value += velocity * dt;

        if (Math.Abs(target - value) < 0.2 && Math.Abs(velocity) < 0.8)
        {
            value = target;
            velocity = 0;
            return true;
        }

        return false;
    }

    private void TickClock()
    {
        SetClockText();
        UpdateIdleVisual();
    }

    private void SetClockText()
    {
        var now = DateTime.Now;
        if (_focus.ShowInPill)
        {
            ClockText.Text = FormatTime(_focus.Remaining);
            ClockText.Foreground = _focus.IsRunning ? FocusBrush : FocusPausedBrush;
        }
        else
        {
            ClockText.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
            ClockText.Foreground = WhiteBrush;
        }

        var date = now.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
        if (!string.IsNullOrEmpty(date))
        {
            DateText.Text = char.ToUpper(date[0], CultureInfo.CurrentCulture) + date[1..];
        }

        HeaderTimeText.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    private void OnFocusTick()
    {
        if (!_focus.ShowInPill)
        {
            return;
        }

        var second = (int)_focus.Remaining.TotalSeconds;
        if (second == _lastFocusSecond)
        {
            return;
        }

        _lastFocusSecond = second;
        SetClockText();
    }

    private void OnFocusStateChanged()
    {
        UpdateTabVisuals();

        if (_focus.IsRunning && !_wasFocusRunning)
        {
            SetMode(PanelMode.Focus);
        }

        _wasFocusRunning = _focus.IsRunning;
        _lastFocusSecond = -1;
        SetClockText();
        UpdateCompactVisual(animate: true);
    }

    private void OnFocusCompleted(FocusPhase phase)
    {
        if (_settings.Current.FocusChime)
        {
            System.Media.SystemSounds.Asterisk.Play();
        }

        _lastActivity = DateTime.Now;
        UpdateTabVisuals();
        SetMode(PanelMode.Focus);
        UpdateCompactVisual(animate: true);

        var label = phase == FocusPhase.Focus
            ? "Focus complete \u00B7 Take a break"
            : "Break over \u00B7 Back to focus";
        ShowToast("\uE916", label, null, 3200);
    }

    private void UpdateTabVisuals()
    {
        if (!_initialized)
        {
            return;
        }

        _syncingTabs = true;
        try
        {
            MusicTab.IsChecked = _mode == PanelMode.Music;
            CameraTab.IsChecked = _mode == PanelMode.Camera;
            ShelfTab.IsChecked = _mode == PanelMode.Shelf;
            FocusTab.IsChecked = _mode == PanelMode.Focus;
        }
        finally
        {
            _syncingTabs = false;
        }

        FocusTabDot.Opacity = _focus.HasSession ? 1 : 0;
    }

    private static int ModeIndex(PanelMode mode)
    {
        return mode switch
        {
            PanelMode.Music => 0,
            PanelMode.Camera => 1,
            PanelMode.Shelf => 2,
            PanelMode.Focus => 3,
            _ => 0
        };
    }

    private FrameworkElement GetView(PanelMode mode)
    {
        return mode switch
        {
            PanelMode.Music => MusicView,
            PanelMode.Camera => CameraHost,
            PanelMode.Shelf => ShelfHost,
            PanelMode.Focus => FocusHost,
            _ => MusicView
        };
    }

    private TranslateTransform GetTranslate(PanelMode mode)
    {
        return mode switch
        {
            PanelMode.Music => MusicTranslate,
            PanelMode.Camera => CameraTranslate,
            PanelMode.Shelf => ShelfTranslate,
            PanelMode.Focus => FocusTranslate,
            _ => MusicTranslate
        };
    }

    private void SetMode(PanelMode mode, bool animate = true)
    {
        if (_modeAnimating)
        {
            return;
        }

        var oldMode = _mode;
        if (oldMode == mode)
        {
            UpdateTabVisuals();
            return;
        }

        _mode = mode;
        UpdateTabVisuals();
        UpdateCameraActivation();

        var canAnimate = animate && _expanded && IsVisible;
        if (!canAnimate)
        {
            SnapMode(mode);
            return;
        }

        AnimateModeSwitch(oldMode, mode);
    }

    private void SnapMode(PanelMode mode)
    {
        foreach (var candidate in AllModes)
        {
            var view = GetView(candidate);
            var translate = GetTranslate(candidate);

            view.BeginAnimation(OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
            view.Opacity = 1;
            view.Visibility = candidate == mode ? Visibility.Visible : Visibility.Collapsed;
        }

        SnapTabHighlight(mode);
    }

    private void AnimateModeSwitch(PanelMode oldMode, PanelMode newMode)
    {
        const double distance = 44;

        _modeAnimating = true;
        MusicTab.IsEnabled = false;
        CameraTab.IsEnabled = false;
        ShelfTab.IsEnabled = false;
        FocusTab.IsEnabled = false;

        var forward = ModeIndex(newMode) > ModeIndex(oldMode);
        var incoming = GetView(newMode);
        var outgoing = GetView(oldMode);
        var incomingTranslate = GetTranslate(newMode);
        var outgoingTranslate = GetTranslate(oldMode);

        foreach (var candidate in AllModes)
        {
            var view = GetView(candidate);
            var translate = GetTranslate(candidate);

            view.BeginAnimation(OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
            view.Opacity = 1;
            view.Visibility = candidate == oldMode || candidate == newMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        outgoing.Opacity = 1;
        outgoingTranslate.X = 0;

        incoming.Opacity = 0;
        incomingTranslate.X = forward ? distance : -distance;

        var outgoingX = new DoubleAnimation(
            0,
            forward ? -distance : distance,
            TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var outgoingOpacity = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(190));
        outgoingTranslate.BeginAnimation(TranslateTransform.XProperty, outgoingX);
        outgoing.BeginAnimation(OpacityProperty, outgoingOpacity);

        var incomingX = new DoubleAnimation(
            incomingTranslate.X,
            0,
            TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var incomingOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(210));
        incomingTranslate.BeginAnimation(TranslateTransform.XProperty, incomingX);
        incoming.BeginAnimation(OpacityProperty, incomingOpacity);

        AnimateTabHighlight(newMode);

        _modeAnimationTimer?.Stop();
        _modeAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _modeAnimationTimer.Tick += (_, _) =>
        {
            _modeAnimationTimer?.Stop();
            FinishModeSwitch(newMode);
        };
        _modeAnimationTimer.Start();
    }

    private void FinishModeSwitch(PanelMode mode)
    {
        _modeAnimating = false;
        MusicTab.IsEnabled = true;
        CameraTab.IsEnabled = true;
        ShelfTab.IsEnabled = true;
        FocusTab.IsEnabled = true;

        foreach (var candidate in AllModes)
        {
            var view = GetView(candidate);
            var translate = GetTranslate(candidate);

            view.BeginAnimation(OpacityProperty, null);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
            view.Opacity = 1;
            view.Visibility = candidate == mode ? Visibility.Visible : Visibility.Collapsed;
        }

        SnapTabHighlight(mode);
        UpdateTabVisuals();
    }

    private void AnimateTabHighlight(PanelMode mode)
    {
        var target = ModeIndex(mode) * 68.0;
        var animation = new DoubleAnimation(
            TabHighlightTransform.X,
            target,
            TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TabHighlightTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void SnapTabHighlight(PanelMode mode)
    {
        TabHighlightTransform.BeginAnimation(TranslateTransform.XProperty, null);
        TabHighlightTransform.X = ModeIndex(mode) * 68;
    }

    private void OnMusicTabChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTabs || !_initialized)
        {
            return;
        }

        SetMode(PanelMode.Music);
    }

    private void OnCameraTabChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTabs || !_initialized)
        {
            return;
        }

        SetMode(PanelMode.Camera);
    }

    private void OnShelfTabChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTabs || !_initialized)
        {
            return;
        }

        SetMode(PanelMode.Shelf);
    }

    private void OnFocusTabChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTabs || !_initialized)
        {
            return;
        }

        SetMode(PanelMode.Focus);
    }

    private void OnLyricsButtonClick(object sender, RoutedEventArgs e)
    {
        if (_lyricsAnimating)
        {
            return;
        }

        _lyricsOpen = !_lyricsOpen;

        if (_lyricsOpen)
        {
            LyricsPane.Visibility = Visibility.Visible;
            _ = _lyrics.LoadAsync(_snapshot);
        }

        SetLyricsButtonState();
        AnimateLyricsPane(_lyricsOpen);
    }

    private void SetLyricsButtonState()
    {
        LyricsButton.Foreground = _lyricsOpen ? Brushes.White : LyricsButtonInactiveBrush;
        LyricsButton.Background = _lyricsOpen ? LyricsButtonActiveBrush : Brushes.Transparent;
        LyricsButton.ToolTip = _lyricsOpen ? "Hide lyrics" : "Show lyrics";
    }

    private void AnimateLyricsPane(bool open)
    {
        _lyricsAnimating = true;
        var token = ++_lyricsAnimationToken;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(300);

        if (open)
        {
            LyricsPane.Visibility = Visibility.Visible;
        }

        MusicMedia.BeginAnimation(WidthProperty, new DoubleAnimation(
            open ? MediaClosedWidth : MediaOpenWidth,
            open ? MediaOpenWidth : MediaClosedWidth,
            duration)
        {
            EasingFunction = ease
        });

        LyricsPane.BeginAnimation(WidthProperty, new DoubleAnimation(
            open ? 0 : LyricsPaneWidth,
            open ? LyricsPaneWidth : 0,
            duration)
        {
            EasingFunction = ease
        });

        MusicInfoTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(
            open ? 0 : -8,
            open ? -8 : 0,
            duration)
        {
            EasingFunction = ease
        });

        LyricsTranslate.X = open ? LyricsPaneWidth : 0;
        LyricsTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(
            open ? LyricsPaneWidth : 0,
            open ? 0 : LyricsPaneWidth,
            duration)
        {
            EasingFunction = ease
        });

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(320)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (token != _lyricsAnimationToken)
            {
                return;
            }

            FinishLyricsAnimation(open);
        };
        timer.Start();
    }

    private void FinishLyricsAnimation(bool open)
    {
        _lyricsAnimating = false;

        MusicMedia.BeginAnimation(WidthProperty, null);
        LyricsPane.BeginAnimation(WidthProperty, null);
        LyricsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        MusicInfoTranslate.BeginAnimation(TranslateTransform.XProperty, null);

        LyricsTranslate.X = 0;
        MusicInfoTranslate.X = open ? -8 : 0;
        _lyricsView.Opacity = 1;

        if (open)
        {
            MusicMedia.Width = MediaOpenWidth;
            LyricsPane.Width = LyricsPaneWidth;
        }
        else
        {
            MusicMedia.Width = MediaClosedWidth;
            LyricsPane.Width = 0;
            LyricsPane.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    private void OnCollapseClick(object sender, RoutedEventArgs e)
    {
        _hoverSuppressed = true;
        _lastActivity = DateTime.Now;
        SetExpanded(false);
    }

    private void OnCompactClick(object sender, MouseButtonEventArgs e)
    {
        if (_expanded || _settings.Current.MinimalNotch)
        {
            return;
        }

        _hoverSuppressed = false;
        _lastActivity = DateTime.Now;
        Wake();

        if (_snapshot is not null && !_focus.HasSession)
        {
            SetMode(PanelMode.Music, animate: false);
        }

        SetExpanded(true);
        e.Handled = true;
    }

    private void OnWaterReminderDue()
    {
        if (Dispatcher.CheckAccess())
        {
            ShowWaterReminder();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(ShowWaterReminder));
        }
    }

    private void ShowWaterReminder()
    {
        var count = _water.GlassesToday;
        var goal = _water.Goal;

        if (_water.GoalReached)
        {
            ShowToast(null, $"Goal reached \u00B7 {goal}/{goal}", 1, 2200, drop: true);
            return;
        }

        ShowToast(
            null,
            $"Time to drink \u00B7 {count}/{goal}",
            count / (double)goal,
            6000,
            OnWaterToastClick,
            drop: true);
    }

    private void OnWaterToastClick()
    {
        ShowWaterConfirmation(_water.LogDrink());
    }

    private void OnWaterClick(object sender, RoutedEventArgs e)
    {
        ShowWaterConfirmation(_water.LogDrink());
    }

    private void ShowWaterConfirmation(bool logged)
    {
        var count = _water.GlassesToday;
        var goal = _water.Goal;

        ShowToast(
            null,
            logged
                ? $"Glass logged \u00B7 {count}/{goal}"
                : $"Goal reached \u00B7 {goal}/{goal}",
            count / (double)goal,
            1800,
            drop: true);
    }

    private void OnWaterStateChanged()
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateWaterWidget();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(UpdateWaterWidget));
        }
    }

    private void UpdateWaterWidget()
    {
        if (!_initialized)
        {
            return;
        }

        var enabled = _settings.Current.WaterReminderEnabled;
        WaterButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (!enabled)
        {
            return;
        }

        var count = _water.GlassesToday;
        var goal = _water.Goal;
        var reached = _water.GoalReached;

        WaterCountText.Text = $"{count}/{goal}";
        WaterButton.Foreground = reached ? GreenBrush : WaterBrush;

        var filled = goal > 0
            ? (int)Math.Round(Math.Clamp(count / (double)goal, 0, 1) * _waterBars.Length)
            : 0;
        var fillBrush = reached ? GreenBrush : WaterBrush;
        for (var i = 0; i < _waterBars.Length; i++)
        {
            _waterBars[i].Background = i < filled ? fillBrush : WaterTrackBrush;
        }

        WaterButton.ToolTip = reached
            ? $"Goal reached \u00B7 {goal} glasses today"
            : $"{count} of {goal} glasses today \u00B7 click to log";
    }

    private void UpdateIdleVisual()
    {
        if (!_initialized)
        {
            return;
        }

        if (_settings.Current.MinimalNotch)
        {
            ClockText.Opacity = 0;
            EyesCanvas.Opacity = 0;
            return;
        }

        if (_compactMediaVisible)
        {
            _eyesShown = false;
            _dozing = false;
            ClockText.Opacity = 0;
            EyesCanvas.Opacity = 0;
            return;
        }

        if (_toastVisible)
        {
            return;
        }

        if (_focus.ShowInPill)
        {
            _eyesShown = false;
            _dozing = false;
            ClockText.BeginAnimation(OpacityProperty, null);
            EyesCanvas.BeginAnimation(OpacityProperty, null);
            ClockText.Opacity = 1;
            EyesCanvas.Opacity = 0;
            return;
        }

        if (_expanded || _hovering)
        {
            ShowClock();
            return;
        }

        var idleFor = DateTime.Now - _lastActivity;
        var mediaActive = _snapshot?.IsPlaying == true;

        if (_settings.Current.ShowMascot && !mediaActive && idleFor.TotalSeconds >= 8)
        {
            ShowEyes();
            if (idleFor.TotalSeconds >= 30)
            {
                Doze();
            }
        }
        else
        {
            ShowClock();
        }
    }

    private void ShowEyes()
    {
        if (_eyesShown || !_settings.Current.ShowMascot)
        {
            return;
        }

        _eyesShown = true;
        Animate(ClockText, OpacityProperty, 0, 240);
        Animate(EyesCanvas, OpacityProperty, _dozing ? 0.55 : 1, 240);
    }

    private void ShowClock()
    {
        if (!_eyesShown && !_dozing)
        {
            ClockText.Opacity = 1;
            EyesCanvas.Opacity = 0;
            return;
        }

        _eyesShown = false;
        _dozing = false;
        EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeLeftScale.ScaleY = 1;
        EyeRightScale.ScaleY = 1;

        Animate(ClockText, OpacityProperty, 1, 220);
        Animate(EyesCanvas, OpacityProperty, 0, 180);
    }

    private void Doze()
    {
        if (_dozing)
        {
            return;
        }

        _dozing = true;
        var doze = new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, doze);
        EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, doze);
        Animate(EyesCanvas, OpacityProperty, 0.55, 400);
    }

    private void Wake()
    {
        if (!_dozing)
        {
            return;
        }

        _dozing = false;
        var wake = new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
        };
        EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, wake);
        EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, wake);
        Animate(EyesCanvas, OpacityProperty, 0, 180);
    }

    private void Blink()
    {
        _blinkTimer.Stop();

        if (_eyesShown && !_dozing && !_expanded && !_compactMediaVisible)
        {
            var blink = new DoubleAnimation(1, 0.08, TimeSpan.FromMilliseconds(65))
            {
                AutoReverse = true
            };
            EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
            EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        }

        _blinkTimer.Interval = TimeSpan.FromMilliseconds(_random.Next(2600, 5600));
        _blinkTimer.Start();
    }

    private void OnMediaSnapshot(MediaSnapshot? snapshot)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var lyricsTrackChanged =
                !string.Equals(_snapshot?.Title, snapshot?.Title, StringComparison.Ordinal) ||
                !string.Equals(_snapshot?.Artist, snapshot?.Artist, StringComparison.Ordinal) ||
                _snapshot?.Duration != snapshot?.Duration;

            var trackChanged = lyricsTrackChanged ||
                               _snapshot?.IsPlaying != snapshot?.IsPlaying;
            _snapshot = snapshot;
            if (trackChanged)
            {
                _lastActivity = DateTime.Now;
            }

            UpdateMediaUI(snapshot);
            UpdateProgress();
            UpdateIdleVisual();

            if (lyricsTrackChanged && _lyricsOpen)
            {
                _ = _lyrics.LoadAsync(snapshot);
            }
        }));
    }

    private void UpdateMediaUI(MediaSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            TitleText.Text = "Nothing playing";
            ArtistText.Text = "Start something on Spotify, YouTube, or Apple Music";
            ArtBrush.ImageSource = null;
            ArtFallback.Visibility = Visibility.Visible;
            ArtGlyph.Visibility = Visibility.Visible;
            CompactArtBrush.ImageSource = null;
            CompactArtFallback.Visibility = Visibility.Visible;
            CompactArtGlyph.Visibility = Visibility.Visible;
            CompactTitleText.Text = string.Empty;
            Vinyl.Opacity = 0;
            SetVinyl(false);
            Waveform.Seed = 0;
            PlayGlyph.Text = "\uE768";
            PrevButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            FooterSource.Text = string.Empty;
            PlayingDot.Opacity = 0;
            ApplyAccentColor(ArtworkAccent.Fallback);
            UpdateCompactVisual(animate: true);
            return;
        }

        TitleText.Text = snapshot.Title;
        ArtistText.Text = string.IsNullOrWhiteSpace(snapshot.Artist)
            ? (string.IsNullOrWhiteSpace(snapshot.Album) ? "Unknown artist" : snapshot.Album)
            : snapshot.Artist;
        CompactTitleText.Text = snapshot.Title;

        if (snapshot.Artwork is not null)
        {
            ArtBrush.ImageSource = snapshot.Artwork;
            ArtFallback.Visibility = Visibility.Collapsed;
            ArtGlyph.Visibility = Visibility.Collapsed;
            CompactArtBrush.ImageSource = snapshot.Artwork;
            CompactArtFallback.Visibility = Visibility.Collapsed;
            CompactArtGlyph.Visibility = Visibility.Collapsed;
        }
        else
        {
            ArtBrush.ImageSource = null;
            ArtFallback.Visibility = Visibility.Visible;
            ArtGlyph.Visibility = Visibility.Visible;
            CompactArtBrush.ImageSource = null;
            CompactArtFallback.Visibility = Visibility.Visible;
            CompactArtGlyph.Visibility = Visibility.Visible;
        }

        Vinyl.Opacity = 1;
        SetVinyl(snapshot.IsPlaying);
        Waveform.Seed = StableHash($"{snapshot.Title}|{snapshot.Artist}|{snapshot.Album}");
        PlayGlyph.Text = snapshot.IsPlaying ? "\uE769" : "\uE768";
        PrevButton.IsEnabled = true;
        PlayButton.IsEnabled = true;
        NextButton.IsEnabled = true;
        FooterSource.Text = snapshot.SourceApp;
        PlayingDot.Opacity = snapshot.IsPlaying ? 1 : 0;

        ApplyAccentColor(ArtworkAccent.Extract(snapshot.Artwork));
        UpdateCompactVisual(animate: true);
    }

    private void UpdateCompactVisual(bool animate)
    {
        if (!_initialized)
        {
            return;
        }

        var showMedia = _snapshot is not null &&
                        !_settings.Current.MinimalNotch &&
                        !_focus.ShowInPill;
        var token = ++_compactAnimationToken;

        CompactMedia.Visibility = Visibility.Visible;
        CompactClock.Visibility = Visibility.Visible;

        if (!animate || showMedia == _compactMediaVisible)
        {
            _compactMediaVisible = showMedia;
            CompactMedia.BeginAnimation(OpacityProperty, null);
            CompactClock.BeginAnimation(OpacityProperty, null);
            CompactMedia.Opacity = showMedia && !_toastVisible ? 1 : 0;
            CompactClock.Opacity = showMedia ? 0 : 1;
            CompactMedia.Visibility = showMedia ? Visibility.Visible : Visibility.Collapsed;
            CompactClock.Visibility = showMedia ? Visibility.Collapsed : Visibility.Visible;
            UpdateEqualizerTimer();
            UpdateIdleVisual();
            return;
        }

        _compactMediaVisible = showMedia;
        var duration = TimeSpan.FromMilliseconds(150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fadeIn = showMedia ? CompactMedia : CompactClock;
        var fadeOut = showMedia ? CompactClock : CompactMedia;

        fadeIn.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(fadeIn.Opacity, 1, duration) { EasingFunction = ease });

        var fadeOutAnimation = new DoubleAnimation(fadeOut.Opacity, 0, duration) { EasingFunction = ease };
        fadeOutAnimation.Completed += (_, _) =>
        {
            if (token != _compactAnimationToken)
            {
                return;
            }

            fadeOut.BeginAnimation(OpacityProperty, null);
            fadeOut.Opacity = 0;
            fadeOut.Visibility = Visibility.Collapsed;
        };
        fadeOut.BeginAnimation(OpacityProperty, fadeOutAnimation);

        UpdateEqualizerTimer();
        UpdateIdleVisual();
    }

    private void UpdateEqualizerTimer()
    {
        if (!_initialized)
        {
            return;
        }

        var shouldRun = _compactMediaVisible &&
                        !_expanded &&
                        IsVisible &&
                        !_settings.Current.MinimalNotch &&
                        _snapshot?.IsPlaying == true;

        if (shouldRun)
        {
            if (!_equalizerTimer.IsEnabled)
            {
                _equalizerTimer.Start();
            }

            return;
        }

        _equalizerTimer.Stop();
        FreezeEqualizer();
    }

    private void FreezeEqualizer()
    {
        for (var i = 0; i < _eqBars.Length; i++)
        {
            _eqBars[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _eqBars[i].ScaleY = 0.3;
            _eqLevels[i] = 0.3;
        }
    }

    private void TickEqualizer()
    {
        if (!_compactMediaVisible || _snapshot?.IsPlaying != true)
        {
            _equalizerTimer.Stop();
            FreezeEqualizer();
            return;
        }

        for (var i = 0; i < _eqBars.Length; i++)
        {
            if (_random.NextDouble() < 0.2)
            {
                _eqTargets[i] = 0.3 + _random.NextDouble() * 0.7;
            }

            _eqLevels[i] += (_eqTargets[i] - _eqLevels[i]) * 0.55;
            _eqBars[i].ScaleY = _eqLevels[i];
        }
    }

    private void SetVinyl(bool playing)
    {
        if (_snapshot is null)
        {
            _vinylRunning = false;
            VinylRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            VinylRotate.Angle = 0;
            Vinyl.Opacity = 0;
            return;
        }

        if (playing && !_vinylRunning)
        {
            _vinylRunning = true;
            var from = VinylRotate.Angle % 360;
            VinylRotate.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(from, from + 360, TimeSpan.FromSeconds(11))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                });
        }
        else if (!playing && _vinylRunning)
        {
            _vinylRunning = false;
            var angle = VinylRotate.Angle % 360;
            VinylRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            VinylRotate.Angle = angle;
        }
    }

    private void ApplyAccentColor(Color color)
    {
        if (_accentApplied && color == _accentColor)
        {
            return;
        }

        _accentApplied = true;
        _accentColor = color;
        _accentBrush.Color = color;

        PlayButton.Background = _accentBrush;
        PlayButton.Foreground = PlayGlyphBrush;
        if (PlayButton.Effect is DropShadowEffect shadow)
        {
            shadow.Color = color;
        }

        Waveform.PlayedBrush = _accentBrush;
        CompactEqBar1.Background = _accentBrush;
        CompactEqBar2.Background = _accentBrush;
        CompactEqBar3.Background = _accentBrush;
        CompactEqBar4.Background = _accentBrush;
        CompactEqBar5.Background = _accentBrush;

        ArtGlow.Fill = CreateGlowBrush(color, 76);
        AmbientGlow.Fill = CreateGlowBrush(color, 92);
    }

    private static RadialGradientBrush CreateGlowBrush(Color color, byte centerAlpha)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(
            Color.FromArgb(centerAlpha, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(centerAlpha * 0.32), color.R, color.G, color.B), 0.62));
        brush.GradientStops.Add(new GradientStop(
            Color.FromArgb(0, color.R, color.G, color.B), 1));
        return brush;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return hash;
        }
    }

    private void UpdateProgress()
    {
        if (!_expanded)
        {
            return;
        }

        if (_snapshot is null)
        {
            Waveform.Progress = 0;
            Waveform.Seed = 0;
            FooterText.Text = string.Empty;
            return;
        }

        var duration = _snapshot.Duration;
        var position = _media.EstimatedPosition;
        var ratio = duration.TotalMilliseconds > 0
            ? Math.Clamp(position.TotalMilliseconds / duration.TotalMilliseconds, 0, 1)
            : 0;

        Waveform.Progress = ratio;
        FooterText.Text = duration > TimeSpan.Zero
            ? $"{FormatTime(position)} / {FormatTime(duration)}"
            : string.Empty;
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private void OnWaveformSeek(object sender, double ratio)
    {
        if (_snapshot is null || _snapshot.Duration <= TimeSpan.Zero)
        {
            return;
        }

        _lastActivity = DateTime.Now;
        _ = _media.SeekAsync(TimeSpan.FromTicks((long)(_snapshot.Duration.Ticks * ratio)));
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        _lastActivity = DateTime.Now;
        _ = _media.TogglePlayPauseAsync();
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        _lastActivity = DateTime.Now;
        _ = _media.PreviousAsync();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        _lastActivity = DateTime.Now;
        _ = _media.NextAsync();
    }

    private void OnVolumeChanged(int volume, bool muted)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_settings.Current.VolumeToast)
            {
                return;
            }

            ShowToast(
                muted ? "\uE74F" : "\uE767",
                muted ? "Muted" : $"Volume {volume}%",
                muted ? 0 : volume / 100.0);
            _lastActivity = DateTime.Now;
        }));
    }

    private void OnBrightnessChanged(int brightness)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_settings.Current.BrightnessToast)
            {
                return;
            }

            ShowToast("\uE706", $"Brightness {brightness}%", brightness / 100.0);
            _lastActivity = DateTime.Now;
        }));
    }

    private void OnBatteryChanged(int percent, bool charging)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateBattery(percent, charging);
            if (charging && !_wasCharging)
            {
                ShowToast("\uE83E", $"Charging \u00B7 {percent}%", null);
            }

            _wasCharging = charging;
        }));
    }

    private void UpdateBattery(int percent, bool charging)
    {
        var hasBattery = _battery.HasBattery;
        BatteryWidget.Visibility = hasBattery ? Visibility.Visible : Visibility.Collapsed;
        BatteryText.Visibility = hasBattery ? Visibility.Visible : Visibility.Collapsed;

        BatteryText.Text = $"{percent}%";
        BatteryFill.Width = 20 * Math.Clamp(percent, 0, 100) / 100.0;
        BatteryFill.Background = charging
            ? GreenBrush
            : percent <= 20
                ? OrangeBrush
                : WhiteBrush;
    }

    private void OnNetworkChanged(bool connected)
    {
        Dispatcher.BeginInvoke(new Action(() => UpdateNetwork(connected)));
    }

    private void UpdateNetwork(bool connected)
    {
        WifiGlyph.Opacity = connected ? 1 : 0.25;
    }

    private void ShowToast(
        string? glyph,
        string label,
        double? ratio,
        int durationMs = 1700,
        Action? onClick = null,
        bool drop = false)
    {
        _toastToken++;
        _toastVisible = true;
        _eyesShown = false;
        _dozing = false;
        ClockText.BeginAnimation(OpacityProperty, null);
        EyesCanvas.BeginAnimation(OpacityProperty, null);
        EyeLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeLeftScale.ScaleY = 1;
        EyeRightScale.ScaleY = 1;
        ClockText.Opacity = 0;
        EyesCanvas.Opacity = 0;

        if (_compactMediaVisible)
        {
            Animate(CompactMedia, OpacityProperty, 0, 120);
        }

        ToastGlyph.Text = glyph ?? string.Empty;
        ToastGlyph.Visibility = drop ? Visibility.Collapsed : Visibility.Visible;
        ToastDrop.Visibility = drop ? Visibility.Visible : Visibility.Collapsed;
        ToastLabel.Text = label;

        if (ratio is double value)
        {
            ToastTrack.Visibility = Visibility.Visible;
            ToastFill.Width = 50 * Math.Clamp(value, 0, 1);
        }
        else
        {
            ToastTrack.Visibility = Visibility.Collapsed;
        }

        _toastClick = onClick;
        ToastBorder.IsHitTestVisible = onClick is not null;
        ToastBorder.Cursor = onClick is not null ? Cursors.Hand : Cursors.Arrow;

        ToastBorder.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
        ToastBorder.BeginAnimation(OpacityProperty, fade);

        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _toastTimer.Start();
    }

    private void OnToastMouseDown(object sender, MouseButtonEventArgs e)
    {
        var action = _toastClick;
        if (action is null)
        {
            return;
        }

        _toastClick = null;
        e.Handled = true;
        action();
    }

    private void HideToast()
    {
        _toastClick = null;
        ToastBorder.IsHitTestVisible = false;
        ToastBorder.Cursor = Cursors.Arrow;

        var token = ++_toastToken;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) =>
        {
            if (token != _toastToken)
            {
                return;
            }

            ToastBorder.Visibility = Visibility.Collapsed;
            ToastBorder.BeginAnimation(OpacityProperty, null);
            ToastBorder.Opacity = 0;
            _toastVisible = false;
            UpdateCompactVisual(animate: false);
            UpdateIdleVisual();
        };
        ToastBorder.BeginAnimation(OpacityProperty, fade);
    }

    private static void Animate(UIElement element, DependencyProperty property, double to, int milliseconds)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(property, animation);
    }
}
