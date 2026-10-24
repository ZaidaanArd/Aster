using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Interop;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class IslandView : UserControl
{
    private const double ExpandedWidth = 500;
    private const double ExpandedHeight = 214;

    private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x32, 0xD7, 0x4B));
    private static readonly Brush OrangeBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush WhiteBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));

    private readonly SettingsService _settings;
    private readonly MediaService _media;
    private readonly AudioService _audio;
    private readonly BrightnessService _brightness;
    private readonly BatteryService _battery;
    private readonly NetworkService _network;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Random _random = new();

    private MediaSnapshot? _snapshot;
    private bool _expanded;
    private bool _hovering;
    private bool _eyesShown;
    private bool _dozing;
    private bool _springRunning;
    private bool _wasCharging;
    private bool _initialized;
    private bool _toastVisible;
    private int _toastToken;

    private double _currentWidth = 260;
    private double _currentHeight = 40;
    private double _velocityWidth;
    private double _velocityHeight;
    private double _targetWidth = 260;
    private double _targetHeight = 40;
    private double _collapsedWidth = 260;
    private double _collapsedHeight = 40;
    private long _lastFrameTicks;

    private DateTime _lastActivity = DateTime.Now;

    public IslandView(
        SettingsService settings,
        MediaService media,
        AudioService audio,
        BrightnessService brightness,
        BatteryService battery,
        NetworkService network)
    {
        _settings = settings;
        _media = media;
        _audio = audio;
        _brightness = brightness;
        _battery = battery;
        _network = network;

        InitializeComponent();

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

        _media.SnapshotChanged += OnMediaSnapshot;
        _audio.VolumeChanged += OnVolumeChanged;
        _brightness.BrightnessChanged += OnBrightnessChanged;
        _battery.Changed += OnBatteryChanged;
        _network.Changed += OnNetworkChanged;

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
        CompositionTarget.Rendering -= OnRendering;
        _springRunning = false;

        _media.SnapshotChanged -= OnMediaSnapshot;
        _audio.VolumeChanged -= OnVolumeChanged;
        _brightness.BrightnessChanged -= OnBrightnessChanged;
        _battery.Changed -= OnBatteryChanged;
        _network.Changed -= OnNetworkChanged;
        _initialized = false;
    }

    public void ApplySettings()
    {
        _collapsedWidth = _settings.Current.MinimalNotch ? 160 : 260;
        _collapsedHeight = _settings.Current.MinimalNotch ? 12 : 40;

        var radius = 22;
        IslandBorder.CornerRadius = new CornerRadius(0, 0, radius, radius);
        ShadowBorder.CornerRadius = new CornerRadius(0, 0, radius, radius);
        CollapsedLayer.Visibility = _settings.Current.MinimalNotch ? Visibility.Collapsed : Visibility.Visible;

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
        }

        if (!_settings.Current.ShowMascot)
        {
            ShowClock();
        }

        UpdateIdleVisual();
    }

    private void CheckHover()
    {
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
                return;
            }

            _hovering = inside;
            _lastActivity = DateTime.Now;

            if (inside)
            {
                _collapseTimer.Stop();
                Wake();
                SetExpanded(true);
            }
            else
            {
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

        _expanded = expanded;
        _targetWidth = expanded ? ExpandedWidth : _collapsedWidth;
        _targetHeight = expanded ? ExpandedHeight : _collapsedHeight;
        ExpandedLayer.IsHitTestVisible = expanded;

        if (expanded)
        {
            _lastActivity = DateTime.Now;
            ShowClock();
            UpdateMediaUI(_snapshot);
        }

        StartSpring();
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

        var widthSettled = Step(ref _currentWidth, ref _velocityWidth, _targetWidth, dt);
        var heightSettled = Step(ref _currentHeight, ref _velocityHeight, _targetHeight, dt);

        Width = _currentWidth;
        Height = _currentHeight;

        var progress = Math.Clamp(
            (_currentWidth - _collapsedWidth) / Math.Max(1, ExpandedWidth - _collapsedWidth),
            0,
            1);

        ExpandedLayer.Opacity = Math.Clamp((progress - 0.18) / 0.82, 0, 1);
        CollapsedLayer.Opacity = _settings.Current.MinimalNotch ? 0 : Math.Clamp(1 - progress * 3, 0, 1);

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

    private static bool Step(ref double value, ref double velocity, double target, double dt)
    {
        const double stiffness = 240.0;
        const double damping = 27.0;

        var force = (target - value) * stiffness;
        velocity += force * dt;
        velocity *= Math.Exp(-damping * dt);
        value += velocity * dt;

        if (Math.Abs(target - value) < 0.15 && Math.Abs(velocity) < 0.5)
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
        ClockText.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        var date = now.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
        if (!string.IsNullOrEmpty(date))
        {
            DateText.Text = char.ToUpper(date[0], CultureInfo.CurrentCulture) + date[1..];
        }
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

        if (_toastVisible)
        {
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

        if (_eyesShown && !_dozing && !_expanded)
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
            var trackChanged = !string.Equals(_snapshot?.Title, snapshot?.Title, StringComparison.Ordinal) ||
                               _snapshot?.IsPlaying != snapshot?.IsPlaying;
            _snapshot = snapshot;
            if (trackChanged)
            {
                _lastActivity = DateTime.Now;
            }

            UpdateMediaUI(snapshot);
            UpdateProgress();
            UpdateIdleVisual();
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
            PlayGlyph.Text = "\uE768";
            PrevButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            FooterSource.Text = string.Empty;
            PlayingDot.Opacity = 0;
            MediaDot.Opacity = 0;
            return;
        }

        TitleText.Text = snapshot.Title;
        ArtistText.Text = string.IsNullOrWhiteSpace(snapshot.Artist)
            ? (string.IsNullOrWhiteSpace(snapshot.Album) ? "Unknown artist" : snapshot.Album)
            : snapshot.Artist;

        if (snapshot.Artwork is not null)
        {
            ArtBrush.ImageSource = snapshot.Artwork;
            ArtFallback.Visibility = Visibility.Collapsed;
            ArtGlyph.Visibility = Visibility.Collapsed;
        }
        else
        {
            ArtBrush.ImageSource = null;
            ArtFallback.Visibility = Visibility.Visible;
            ArtGlyph.Visibility = Visibility.Visible;
        }

        PlayGlyph.Text = snapshot.IsPlaying ? "\uE769" : "\uE768";
        PrevButton.IsEnabled = true;
        PlayButton.IsEnabled = true;
        NextButton.IsEnabled = true;
        FooterSource.Text = snapshot.SourceApp;
        PlayingDot.Opacity = snapshot.IsPlaying ? 1 : 0;
        MediaDot.Opacity = snapshot.IsPlaying ? 1 : 0;
    }

    private void UpdateProgress()
    {
        if (_snapshot is null)
        {
            ProgressFill.Width = 0;
            FooterText.Text = string.Empty;
            return;
        }

        var duration = _snapshot.Duration;
        var position = _media.EstimatedPosition;
        var ratio = duration.TotalMilliseconds > 0
            ? Math.Clamp(position.TotalMilliseconds / duration.TotalMilliseconds, 0, 1)
            : 0;

        var width = ProgressHit.ActualWidth;
        if (width > 0)
        {
            ProgressFill.Width = width * ratio;
        }

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

    private void OnProgressSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateProgress();
    }

    private void OnProgressMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_snapshot is null || _snapshot.Duration <= TimeSpan.Zero)
        {
            return;
        }

        var width = ProgressHit.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(e.GetPosition(ProgressHit).X / width, 0, 1);
        _ = _media.SeekAsync(TimeSpan.FromTicks((long)(_snapshot.Duration.Ticks * ratio)));
        e.Handled = true;
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

    private void ShowToast(string glyph, string label, double? ratio)
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

        ToastGlyph.Text = glyph;
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

        ToastBorder.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
        ToastBorder.BeginAnimation(OpacityProperty, fade);

        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
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
