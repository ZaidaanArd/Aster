using System.Diagnostics;
using Aster.Avalonia.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Platform.Windows.Window;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aster.Avalonia.Services;

public sealed class WindowMotionController : IDisposable
{
    private readonly Window _window;
    private readonly ShellViewModel _state;
    private readonly WindowsWindowPlacement _placement;
    private readonly ISettingsStore _settings;
    private readonly IAppLog _log;
    private IntPtr _hwnd;
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(320) };

    private SpringState _widthSpring = new() { Value = WindowGeometry.CollapsedWidth };
    private SpringState _heightSpring = new() { Value = WindowGeometry.CollapsedHeight };
    private double _targetWidth = WindowGeometry.CollapsedWidth;
    private double _targetHeight = WindowGeometry.CollapsedHeight;
    private bool _expanded;
    private bool _running;
    private bool _disposed;
    private bool _hoverSuppressed;
    private long _lastTicks;
    private int _lastPhysicalWidth = -1;
    private int _lastPhysicalHeight = -1;

    public WindowMotionController(
        Window window,
        ShellViewModel state,
        WindowsWindowPlacement placement,
        ISettingsStore settings,
        IAppLog log)
    {
        _window = window;
        _state = state;
        _placement = placement;
        _settings = settings;
        _log = log;

        _hoverTimer.Tick += (_, _) => OnHoverTick();
        _collapseTimer.Tick += (_, _) => OnCollapseTick();
    }

    public bool IsExpanded => _expanded;

    public void Start()
    {
        _hwnd = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (_hwnd == IntPtr.Zero)
        {
            _log.Warn("Window handle unavailable; motion disabled.");
            return;
        }

        ApplyCollapsedTarget(snap: true);
        _hoverTimer.Start();
    }

    public void SetExpanded(bool expanded)
    {
        if (_disposed || _expanded == expanded)
        {
            return;
        }

        if (expanded && _settings.Current.MinimalNotch)
        {
            return;
        }

        _expanded = expanded;
        _state.IsExpanded = expanded;
        _targetWidth = expanded
            ? WindowGeometry.ExpandedWidth
            : WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        _targetHeight = expanded
            ? WindowGeometry.ExpandedHeight
            : WindowGeometry.CollapsedHeightFor(_settings.Current.MinimalNotch);
        EnsureFrames();
    }

    public void SuppressHover() => _hoverSuppressed = true;

    public void Reapply()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ApplySize(force: true);
        }
    }

    public void ApplySettings()
    {
        if (_settings.Current.MinimalNotch && _expanded)
        {
            _expanded = false;
            _state.IsExpanded = false;
            ApplyCollapsedTarget(snap: true);
            return;
        }

        if (!_expanded)
        {
            ApplyCollapsedTarget(snap: true);
        }
    }

    private void ApplyCollapsedTarget(bool snap)
    {
        _targetWidth = WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        _targetHeight = WindowGeometry.CollapsedHeightFor(_settings.Current.MinimalNotch);

        if (!snap)
        {
            return;
        }

        _widthSpring = new SpringState { Value = _targetWidth };
        _heightSpring = new SpringState { Value = _targetHeight };
        ApplySize(force: true);
        _state.ExpansionProgress = 0;
        _state.IsExpanded = false;
        _expanded = false;
    }

    private void OnHoverTick()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var inside = _placement.IsCursorInside(_hwnd);
        if (inside && !_hoverSuppressed && !_settings.Current.MinimalNotch)
        {
            _collapseTimer.Stop();
            SetExpanded(true);
        }
        else if (!inside)
        {
            _hoverSuppressed = false;
            if (_expanded && !_collapseTimer.IsEnabled)
            {
                _collapseTimer.Start();
            }
        }
    }

    private void OnCollapseTick()
    {
        _collapseTimer.Stop();
        if (_hwnd == IntPtr.Zero || _placement.IsCursorInside(_hwnd))
        {
            return;
        }

        SetExpanded(false);
    }

    private void EnsureFrames()
    {
        if (_running || _disposed)
        {
            return;
        }

        _running = true;
        _lastTicks = 0;
        _window.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        if (_disposed)
        {
            _running = false;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var dt = _lastTicks == 0
            ? 1.0 / 60.0
            : Math.Min((now - _lastTicks) / (double)Stopwatch.Frequency, 0.05);
        _lastTicks = now;

        var bouncy = _settings.Current.BouncySpring;
        var widthSettled = _widthSpring.Step(_targetWidth, dt, bouncy);
        var heightSettled = _heightSpring.Step(_targetHeight, dt, bouncy);

        ApplySize(force: false);
        _state.ExpansionProgress = Progress();

        if (widthSettled && heightSettled)
        {
            ApplySize(force: true);
            _running = false;
            return;
        }

        _window.RequestAnimationFrame(OnFrame);
    }

    private double Progress()
    {
        var collapsed = WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        return Math.Clamp(
            (_widthSpring.Value - collapsed) /
            Math.Max(1, WindowGeometry.ExpandedWidth - collapsed),
            0,
            1);
    }

    private void ApplySize(bool force)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var screen = _window.Screens.Primary ?? _window.Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var physicalWidth = (int)Math.Round(_widthSpring.Value * scaling);
        var physicalHeight = (int)Math.Round(_heightSpring.Value * scaling);

        if (!force &&
            Math.Abs(physicalWidth - _lastPhysicalWidth) < 1 &&
            Math.Abs(physicalHeight - _lastPhysicalHeight) < 1)
        {
            return;
        }

        _lastPhysicalWidth = physicalWidth;
        _lastPhysicalHeight = physicalHeight;
        _window.Width = _widthSpring.Value;
        _window.Height = _heightSpring.Value;

        var x = WindowGeometry.CenteredLeft(
            screen.Bounds.X,
            screen.Bounds.Width,
            _widthSpring.Value,
            scaling);
        _placement.SetBounds(_hwnd, x, screen.Bounds.Y, physicalWidth, physicalHeight);
    }

    public void Dispose()
    {
        _disposed = true;
        _hoverTimer.Stop();
        _collapseTimer.Stop();
    }
}
