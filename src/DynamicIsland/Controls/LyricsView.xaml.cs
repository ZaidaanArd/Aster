using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class LyricsView : UserControl
{
    private const int ScrollDurationMs = 220;

    private static readonly Brush InactiveLineBrush = CreateFrozenBrush(Color.FromRgb(0x8C, 0x8C, 0x96));
    private static readonly Brush ActiveLineBrush = CreateFrozenBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush PlainLineBrush = CreateFrozenBrush(Color.FromRgb(0xA8, 0xA8, 0xB0));
    private static readonly Brush LyricsAccentBrush = CreateFrozenBrush(Color.FromRgb(0x8F, 0xA8, 0xFF));
    private static readonly Brush MutedDotBrush = CreateFrozenBrush(Color.FromRgb(0x5E, 0x5E, 0x66));
    private static readonly FontFamily UiFont = new("Segoe UI Variable Display, Segoe UI");

    private readonly MediaService _media;
    private readonly LyricsService _lyrics;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _scrollTimer;
    private readonly List<LyricsLineVisual> _lineVisuals = new();

    private Brush _highlightBrush;
    private string _highlightKey;
    private int _currentLine = -1;
    private bool _initialized;
    private bool _rendering;
    private bool _loadingVisuals;
    private double _scrollFrom;
    private double _scrollTo;
    private long _scrollStartTicks;

    public LyricsView(MediaService media, LyricsService lyrics, SettingsService settings)
    {
        _media = media;
        _lyrics = lyrics;
        _settings = settings;

        _highlightKey = settings.Current.LyricsHighlightColor;
        _highlightBrush = CreateHighlightBrush(ResolveHighlightColor(_highlightKey));

        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => UpdatePlayback();

        _scrollTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _scrollTimer.Tick += OnScrollTick;

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
        _lyrics.Changed += OnLyricsChanged;
        _settings.Changed += OnSettingsChanged;
        RenderLyrics();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _scrollTimer.Stop();
        StopLoadingVisuals();
        _lyrics.Changed -= OnLyricsChanged;
        _settings.Changed -= OnSettingsChanged;
        _initialized = false;
    }

    private void OnLyricsChanged()
    {
        if (Dispatcher.CheckAccess())
        {
            RenderLyrics();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(RenderLyrics));
        }
    }

    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(OnSettingsChanged));
            return;
        }

        if (!_initialized)
        {
            return;
        }

        var key = _settings.Current.LyricsHighlightColor;
        if (string.Equals(key, _highlightKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _highlightKey = key;
        _highlightBrush = CreateHighlightBrush(ResolveHighlightColor(key));

        foreach (var visual in _lineVisuals)
        {
            visual.Highlight.Background = _highlightBrush;
        }
    }

    private static Color ResolveHighlightColor(string? text) =>
        ColorHex.TryParse(text, out var color)
            ? color
            : Color.FromRgb(0x7C, 0x9C, 0xFF);

    private static Brush CreateHighlightBrush(Color color)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0x34, color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0x18, color.R, color.G, color.B), 1)
            }
        };

        brush.Freeze();
        return brush;
    }

    private void RenderLyrics()
    {
        if (!_initialized)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(RenderLyrics));
            return;
        }

        if (_rendering)
        {
            return;
        }

        _rendering = true;
        try
        {
            _scrollTimer.Stop();
            LyricsStack.Children.Clear();
            _lineVisuals.Clear();
            _currentLine = -1;
            LyricsScroll.ScrollToTop();

            RefreshButton.IsEnabled = _media.Current is not null && !_lyrics.IsLoading;

            if (_lyrics.IsLoading)
            {
                StartLoadingVisuals();
                SetStatus("SEARCHING", LyricsAccentBrush, "Fetching lyrics from LRCLIB");
                ShowMessage("Searching lyrics...");
                return;
            }

            StopLoadingVisuals();

            var document = _lyrics.Current;
            if (document is null)
            {
                SetStatus("LRCLIB", MutedDotBrush, "Lyrics from LRCLIB");
                ShowMessage(
                    _media.Current is null
                        ? "Play something to see lyrics"
                        : _lyrics.StatusMessage ?? "Lyrics unavailable",
                    "\uE8D6");
                return;
            }

            if (document.IsInstrumental)
            {
                SetStatus("INSTRUMENTAL", MutedDotBrush, "Instrumental track");
                ShowMessage("Instrumental track", "\uE8D6");
                return;
            }

            if (document.IsSynced && document.Lines.Count > 0)
            {
                SetStatus("SYNCED", LyricsAccentBrush, "Synced lyrics \u00B7 LRCLIB");
                HideMessage();

                for (var index = 0; index < document.Lines.Count; index++)
                {
                    var visual = CreateLineVisual(document.Lines[index]);
                    _lineVisuals.Add(visual);
                    LyricsStack.Children.Add(visual.Border);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdatePlayback();
                    ScrollToCurrentLine();
                }), DispatcherPriority.Background);
                return;
            }

            if (!string.IsNullOrWhiteSpace(document.PlainLyrics))
            {
                SetStatus("PLAIN", MutedDotBrush, "Plain lyrics \u00B7 LRCLIB");
                HideMessage();

                var text = new TextBlock
                {
                    Text = document.PlainLyrics.Trim(),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = UiFont,
                    FontSize = 11.5,
                    Foreground = PlainLineBrush,
                    LineHeight = 18,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    Margin = new Thickness(7, 2, 5, 2)
                };
                LyricsStack.Children.Add(text);
                return;
            }

            SetStatus("LRCLIB", MutedDotBrush, "Lyrics from LRCLIB");
            ShowMessage("No lyrics found for this track", "\uE8D6");
        }
        finally
        {
            _rendering = false;
        }
    }

    private LyricsLineVisual CreateLineVisual(LyricsLine line)
    {
        var text = new TextBlock
        {
            Text = line.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = UiFont,
            FontSize = 11.5,
            Foreground = InactiveLineBrush,
            LineHeight = 18,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(8, 2, 8, 2),
            Opacity = 0.7
        };

        var highlight = new Border
        {
            Background = _highlightBrush,
            CornerRadius = new CornerRadius(7),
            Opacity = 0,
            IsHitTestVisible = false
        };

        var content = new Grid();
        content.Children.Add(highlight);
        content.Children.Add(text);

        var border = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Hand,
            Tag = line.Time,
            Child = content
        };
        border.MouseLeftButtonDown += OnLyricLineClick;

        return new LyricsLineVisual(border, highlight, text);
    }

    private void OnLyricLineClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TimeSpan time })
        {
            _ = _media.SeekAsync(time);
            e.Handled = true;
        }
    }

    private void UpdatePlayback()
    {
        var document = _lyrics.Current;
        if (document?.IsSynced != true || _lineVisuals.Count == 0 || !IsVisible)
        {
            return;
        }

        var position = _media.EstimatedPosition;
        var index = FindLineIndex(document.Lines, position);
        if (index == _currentLine)
        {
            return;
        }

        SetCurrentLine(index);
    }

    private void SetCurrentLine(int index)
    {
        var oldIndex = _currentLine;
        _currentLine = index;
        ApplyLineState(index, oldIndex);

        Dispatcher.BeginInvoke(new Action(ScrollToCurrentLine), DispatcherPriority.Background);
    }

    private void ApplyLineState(int index, int oldIndex)
    {
        for (var i = 0; i < _lineVisuals.Count; i++)
        {
            var visual = _lineVisuals[i];
            var active = i == index;
            var distance = index < 0 ? 0 : Math.Abs(i - index);

            visual.Text.Foreground = active ? ActiveLineBrush : InactiveLineBrush;
            visual.Text.Opacity = active
                ? 1.0
                : index < 0
                    ? 0.7
                    : distance switch
                    {
                        1 => 0.8,
                        2 => 0.56,
                        _ => 0.36
                    };

            if (active)
            {
                AnimateHighlight(visual, 1);
            }
            else if (i == oldIndex)
            {
                AnimateHighlight(visual, 0);
            }
            else
            {
                visual.Highlight.BeginAnimation(OpacityProperty, null);
                visual.Highlight.Opacity = 0;
            }
        }
    }

    private static void AnimateHighlight(LyricsLineVisual visual, double target)
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(180);

        visual.Highlight.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(target, duration) { EasingFunction = ease });
    }

    private void ScrollToCurrentLine()
    {
        if (_currentLine < 0 || _currentLine >= _lineVisuals.Count || !LyricsScroll.IsVisible)
        {
            return;
        }

        if (LyricsScroll.ViewportHeight <= 0)
        {
            return;
        }

        try
        {
            var visual = _lineVisuals[_currentLine];
            var y = visual.Border
                .TransformToAncestor(LyricsStack)
                .Transform(new Point(0, 0)).Y;

            var target = y - (LyricsScroll.ViewportHeight - visual.Border.ActualHeight) / 2;
            target = Math.Clamp(target, 0, LyricsScroll.ScrollableHeight);
            AnimateScrollTo(target);
        }
        catch
        {
            // Layout can be transiently invalid while the panel is resizing.
        }
    }

    private void AnimateScrollTo(double target)
    {
        var from = LyricsScroll.VerticalOffset;
        if (Math.Abs(target - from) < 0.5)
        {
            LyricsScroll.ScrollToVerticalOffset(target);
            return;
        }

        _scrollFrom = from;
        _scrollTo = target;
        _scrollStartTicks = Stopwatch.GetTimestamp();
        _scrollTimer.Start();
    }

    private void OnScrollTick(object? sender, EventArgs e)
    {
        if (!LyricsScroll.IsVisible)
        {
            _scrollTimer.Stop();
            return;
        }

        var elapsedMs =
            (Stopwatch.GetTimestamp() - _scrollStartTicks) * 1000.0 / Stopwatch.Frequency;
        var progress = Math.Clamp(elapsedMs / ScrollDurationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        LyricsScroll.ScrollToVerticalOffset(_scrollFrom + (_scrollTo - _scrollFrom) * eased);

        if (progress >= 1)
        {
            _scrollTimer.Stop();
        }
    }

    private void OnLyricsWheel(object sender, MouseWheelEventArgs e)
    {
        _scrollTimer.Stop();
    }

    private static int FindLineIndex(IReadOnlyList<LyricsLine> lines, TimeSpan position)
    {
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;
        var seconds = position.TotalSeconds;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (lines[mid].TimeSeconds <= seconds)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    private void SetStatus(string text, Brush dotBrush, string tooltip)
    {
        LyricsStatus.Text = text;
        LyricsStatus.ToolTip = tooltip;
        StatusDot.Fill = dotBrush;
        StatusDot.Opacity = 1;
    }

    private void StartLoadingVisuals()
    {
        if (_loadingVisuals)
        {
            return;
        }

        _loadingVisuals = true;

        var pulse = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(650))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        StatusDot.BeginAnimation(OpacityProperty, pulse);

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        RefreshRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void StopLoadingVisuals()
    {
        if (!_loadingVisuals)
        {
            return;
        }

        _loadingVisuals = false;
        StatusDot.BeginAnimation(OpacityProperty, null);
        StatusDot.Opacity = 1;
        RefreshRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        RefreshRotate.Angle = 0;
    }

    private void ShowMessage(string message, string glyph = "\uE8D6")
    {
        LyricsMessage.Text = message;
        LyricsMessageGlyph.Text = glyph;
        LyricsMessagePanel.Visibility = Visibility.Visible;
        LyricsScroll.Visibility = Visibility.Collapsed;
    }

    private void HideMessage()
    {
        LyricsMessagePanel.Visibility = Visibility.Collapsed;
        LyricsScroll.Visibility = Visibility.Visible;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _ = _lyrics.LoadAsync(_media.Current, force: true);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed record LyricsLineVisual(
        Border Border,
        Border Highlight,
        TextBlock Text);
}
