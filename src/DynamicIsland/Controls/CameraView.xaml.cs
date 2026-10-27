using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class CameraView : UserControl
{
    private static readonly Brush LiveDotBrush = CreateFrozenBrush(Color.FromRgb(0x32, 0xD7, 0x4B));
    private static readonly Brush WarningDotBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly Brush ErrorDotBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x45, 0x3A));
    private static readonly Brush MutedDotBrush = CreateFrozenBrush(Color.FromRgb(0x5E, 0x5E, 0x66));
    private static readonly Brush LiveTextBrush = CreateFrozenBrush(Color.FromRgb(0x32, 0xD7, 0x4B));
    private static readonly Brush ErrorTextBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x6B, 0x63));

    private readonly CameraService _camera;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _statusTimer;

    private WriteableBitmap? _bitmap;
    private bool _active;
    private bool _loadingMirror;
    private bool _subscribed;
    private int _activationToken;
    private string _liveStatus = "Camera off";

    public CameraView(CameraService camera, SettingsService settings)
    {
        _camera = camera;
        _settings = settings;

        _loadingMirror = true;
        InitializeComponent();

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.4)
        };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            if (_active)
            {
                ShowLiveStatus();
            }
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Subscribe();
    }

    public void SetActive(bool active)
    {
        DebugLog.Write($"camera view set active={active} current={_active}");
        if (_active == active)
        {
            return;
        }

        _active = active;
        _activationToken++;

        if (active)
        {
            var token = _activationToken;
            PrepareStarting();
            _ = StartCameraAsync(token);
        }
        else
        {
            _statusTimer.Stop();
            _ = _camera.StopAsync();
            ClearPreview();
            SetIdleUi();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();
        _loadingMirror = true;
        MirrorToggle.IsChecked = _settings.Current.CameraMirror;
        _loadingMirror = false;
        ApplyMirror();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _active = false;
        _activationToken++;
        _statusTimer.Stop();
        Unsubscribe();
        _ = _camera.StopAsync();
        ClearPreview();
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        _camera.FrameReady += OnFrameReady;
        _camera.StatusChanged += OnCameraStatusChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        _camera.FrameReady -= OnFrameReady;
        _camera.StatusChanged -= OnCameraStatusChanged;
    }

    private async Task StartCameraAsync(int token)
    {
        try
        {
            await _camera.StartAsync();
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera view start failed: {ex}");
        }
        finally
        {
            if (token != _activationToken || !_active)
            {
                await _camera.StopAsync();
            }
        }
    }

    private void OnFrameReady(CameraFrame frame)
    {
        Dispatcher.BeginInvoke(new Action(() => RenderFrame(frame)), DispatcherPriority.Render);
    }

    private void RenderFrame(CameraFrame frame)
    {
        if (!_active)
        {
            _camera.MarkFrameRendered();
            return;
        }

        try
        {
            if (_bitmap is null ||
                _bitmap.PixelWidth != frame.Width ||
                _bitmap.PixelHeight != frame.Height)
            {
                _bitmap = new WriteableBitmap(
                    frame.Width,
                    frame.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null);

                PreviewImage.Source = _bitmap;
            }

            _bitmap.WritePixels(
                new Int32Rect(0, 0, frame.Width, frame.Height),
                frame.Pixels,
                frame.Stride,
                0);

            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Visible;
            LiveChip.Visibility = Visibility.Visible;
            ShutterButton.IsEnabled = true;

            _liveStatus = $"Live \u00B7 {frame.Width} \u00D7 {frame.Height}";
            if (!_statusTimer.IsEnabled)
            {
                StatusText.Text = _liveStatus;
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera render failed: {ex}");
        }
        finally
        {
            _camera.MarkFrameRendered();
        }
    }

    private void OnCameraStatusChanged(CameraStatus status)
    {
        if (!_active)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            ApplyStatus(status);
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(() => ApplyStatus(status)));
        }
    }

    private void ApplyStatus(CameraStatus status)
    {
        DeviceText.Text = string.IsNullOrWhiteSpace(status.DeviceName)
            ? "Camera"
            : status.DeviceName;

        HintText.Visibility = Visibility.Visible;

        switch (status.State)
        {
            case CameraState.Starting:
                _statusTimer.Stop();
                SetHeader("STARTING", WarningDotBrush, WarningDotBrush);
                MirrorToggle.IsEnabled = false;
                ShutterButton.IsEnabled = false;
                SwitchButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Collapsed;
                OpenSettingsButton.Visibility = Visibility.Collapsed;
                StatusText.Text = "Starting camera...";
                ShowPlaceholder();
                break;

            case CameraState.Live:
                SetHeader("LIVE", LiveDotBrush, LiveTextBrush);
                MirrorToggle.IsEnabled = true;
                UpdateSwitchButton();
                RetryButton.Visibility = Visibility.Collapsed;
                OpenSettingsButton.Visibility = Visibility.Collapsed;

                _liveStatus = string.IsNullOrWhiteSpace(status.Message)
                    ? "Live"
                    : $"Live \u00B7 {status.Message}";

                if (!_statusTimer.IsEnabled)
                {
                    StatusText.Text = _liveStatus;
                }

                if (_bitmap is null)
                {
                    ShowPlaceholder();
                }

                break;

            case CameraState.NoCamera:
                _statusTimer.Stop();
                SetHeader("OFFLINE", MutedDotBrush, MutedDotBrush);
                MirrorToggle.IsEnabled = false;
                ShutterButton.IsEnabled = false;
                SwitchButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Visible;
                OpenSettingsButton.Visibility = Visibility.Collapsed;
                StatusText.Text = status.Message ?? "No camera found";
                ShowPlaceholder();
                break;

            case CameraState.AccessDenied:
                _statusTimer.Stop();
                SetHeader("BLOCKED", ErrorDotBrush, ErrorTextBrush);
                MirrorToggle.IsEnabled = false;
                ShutterButton.IsEnabled = false;
                SwitchButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Visible;
                OpenSettingsButton.Visibility = Visibility.Visible;
                StatusText.Text = "Camera access blocked";
                ShowPlaceholder();
                break;

            case CameraState.Busy:
                _statusTimer.Stop();
                SetHeader("BUSY", WarningDotBrush, WarningDotBrush);
                MirrorToggle.IsEnabled = false;
                ShutterButton.IsEnabled = false;
                SwitchButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Visible;
                OpenSettingsButton.Visibility = Visibility.Collapsed;
                StatusText.Text = "Camera is in use by another app";
                ShowPlaceholder();
                break;

            case CameraState.Error:
                _statusTimer.Stop();
                SetHeader("ERROR", ErrorDotBrush, ErrorTextBrush);
                MirrorToggle.IsEnabled = false;
                ShutterButton.IsEnabled = false;
                SwitchButton.Visibility = Visibility.Collapsed;
                RetryButton.Visibility = Visibility.Visible;
                OpenSettingsButton.Visibility = Visibility.Visible;
                StatusText.Text = "Camera unavailable";
                ShowPlaceholder();
                break;

            case CameraState.Idle:
                SetIdleUi();
                break;
        }
    }

    private void UpdateSwitchButton()
    {
        SwitchButton.Visibility = _camera.DeviceCount > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        SwitchButton.IsEnabled = true;
    }

    private void PrepareStarting()
    {
        _statusTimer.Stop();
        SetHeader("STARTING", WarningDotBrush, WarningDotBrush);
        DeviceText.Text = "Camera";
        MirrorToggle.IsEnabled = false;
        ShutterButton.IsEnabled = false;
        SwitchButton.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        OpenSettingsButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Starting camera...";
        HintText.Visibility = Visibility.Visible;
        ShowPlaceholder();
    }

    private void SetIdleUi()
    {
        SetHeader("OFF", MutedDotBrush, MutedDotBrush);
        DeviceText.Text = "Camera";
        MirrorToggle.IsEnabled = false;
        ShutterButton.IsEnabled = false;
        SwitchButton.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        OpenSettingsButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Camera off";
        HintText.Visibility = Visibility.Visible;
        ShowPlaceholder();
    }

    private void SetHeader(string state, Brush dotBrush, Brush textBrush)
    {
        CameraStateText.Text = state;
        CameraStateText.Foreground = textBrush;
        CameraDot.Fill = dotBrush;
    }

    private void ShowPlaceholder()
    {
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        LiveChip.Visibility = Visibility.Collapsed;
    }

    private void ClearPreview()
    {
        _bitmap = null;
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        LiveChip.Visibility = Visibility.Collapsed;
        ShutterButton.IsEnabled = false;
    }

    private void ShowLiveStatus()
    {
        if (_camera.State == CameraState.Live)
        {
            StatusText.Text = _liveStatus;
        }
    }

    private async void OnShutterClick(object sender, RoutedEventArgs e)
    {
        if (!_active || _bitmap is null)
        {
            return;
        }

        ShutterButton.IsEnabled = false;
        _statusTimer.Stop();
        Flash();
        StatusText.Text = "Saving...";

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "DynamicIsland",
                "Camera");
            Directory.CreateDirectory(directory);

            var path = await _camera.CapturePhotoAsync(directory);
            path ??= SavePreview(directory);

            StatusText.Text = path is null
                ? "Could not save photo"
                : $"Saved {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera snapshot failed: {ex}");
            StatusText.Text = "Could not save photo";
        }
        finally
        {
            if (_active)
            {
                ShutterButton.IsEnabled = true;
            }

            _statusTimer.Start();
        }
    }

    private string? SavePreview(string directory)
    {
        if (_bitmap is null)
        {
            return null;
        }

        try
        {
            var path = Path.Combine(
                directory,
                $"camera-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));

            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera preview snapshot failed: {ex}");
            return null;
        }
    }

    private void Flash()
    {
        var animation = new DoubleAnimation(0.72, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        FlashOverlay.BeginAnimation(OpacityProperty, animation);
    }

    private void OnMirrorChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingMirror)
        {
            return;
        }

        _settings.Current.CameraMirror = MirrorToggle.IsChecked == true;
        _settings.Save();
        ApplyMirror();
    }

    private void ApplyMirror()
    {
        MirrorTransform.ScaleX = MirrorToggle.IsChecked == true ? -1 : 1;
    }

    private async void OnSwitchClick(object sender, RoutedEventArgs e)
    {
        if (!_active)
        {
            return;
        }

        SwitchButton.IsEnabled = false;
        await _camera.SwitchCameraAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (!_active)
        {
            return;
        }

        _activationToken++;
        var token = _activationToken;
        PrepareStarting();

        await _camera.StopAsync();
        if (_active && token == _activationToken)
        {
            await _camera.StartAsync();
        }
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:privacy-webcam")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera settings launch failed: {ex}");
        }
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
