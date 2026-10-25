using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class LyricsView : UserControl
{
    private static readonly Brush InactiveLineBrush = CreateFrozenBrush(Color.FromRgb(0x8C, 0x8C, 0x96));
    private static readonly Brush ActiveLineBrush = CreateFrozenBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush ActiveLineBackground = CreateFrozenBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
    private static readonly FontFamily UiFont = new("Segoe UI Variable Display, Segoe UI");

    private readonly MediaService _media;
    private readonly LyricsService _lyrics;
    private readonly DispatcherTimer _timer;
    private readonly List<LyricsLineVisual> _lineVisuals = new();

    private int _currentLine = -1;
    private bool _initialized;
    private bool _rendering;

    public LyricsView(MediaService media, LyricsService lyrics)
    {
        _media = media;
        _lyrics = lyrics;

        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => UpdatePlayback();

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
        RenderLyrics();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _lyrics.Changed -= OnLyricsChanged;
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
            LyricsStack.Children.Clear();
            _lineVisuals.Clear();
            _currentLine = -1;
            LyricsScroll.ScrollToTop();

            RefreshButton.IsEnabled = _media.Current is not null && !_lyrics.IsLoading;

            if (_lyrics.IsLoading)
            {
                LyricsStatus.Text = "Searching...";
                ShowMessage("Searching lyrics...");
                return;
            }

            var document = _lyrics.Current;
            if (document is null)
            {
                LyricsStatus.Text = "LRCLIB";
                ShowMessage(_media.Current is null
                    ? "Play something to see lyrics"
                    : _lyrics.StatusMessage ?? "Lyrics unavailable");
                return;
            }

            if (document.IsInstrumental)
            {
                LyricsStatus.Text = "Instrumental";
                ShowMessage("Instrumental track");
                return;
            }

            if (document.IsSynced && document.Lines.Count > 0)
            {
                LyricsStatus.Text = "Synced \u00B7 LRCLIB";
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
                LyricsStatus.Text = "Plain \u00B7 LRCLIB";
                HideMessage();

                var text = new TextBlock
                {
                    Text = document.PlainLyrics.Trim(),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = UiFont,
                    FontSize = 12,
                    Foreground = InactiveLineBrush,
                    LineHeight = 19,
                    Margin = new Thickness(2, 0, 2, 0)
                };
                LyricsStack.Children.Add(text);
                return;
            }

            LyricsStatus.Text = "LRCLIB";
            ShowMessage("No lyrics found for this track");
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
            LineHeight = 18
        };

        var border = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 3, 6, 3),
            Margin = new Thickness(0, 1, 0, 1),
            Cursor = Cursors.Hand,
            Tag = line.Time,
            Child = text
        };
        border.MouseLeftButtonDown += OnLyricLineClick;

        return new LyricsLineVisual(border, text);
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
        if (_currentLine >= 0 && _currentLine < _lineVisuals.Count)
        {
            var old = _lineVisuals[_currentLine];
            old.Border.Background = Brushes.Transparent;
            old.Text.Foreground = InactiveLineBrush;
            old.Text.FontSize = 11.5;
            old.Text.FontWeight = FontWeights.Normal;
        }

        _currentLine = index;

        if (index >= 0 && index < _lineVisuals.Count)
        {
            var current = _lineVisuals[index];
            current.Border.Background = ActiveLineBackground;
            current.Text.Foreground = ActiveLineBrush;
            current.Text.FontSize = 13;
            current.Text.FontWeight = FontWeights.SemiBold;
        }

        Dispatcher.BeginInvoke(new Action(ScrollToCurrentLine), DispatcherPriority.Background);
    }

    private void ScrollToCurrentLine()
    {
        if (_currentLine < 0 || _currentLine >= _lineVisuals.Count || !LyricsScroll.IsVisible)
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
            LyricsScroll.ScrollToVerticalOffset(target);
        }
        catch
        {
            // Layout can be transiently invalid while the panel is resizing.
        }
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

    private void ShowMessage(string message)
    {
        LyricsMessage.Text = message;
        LyricsMessage.Visibility = Visibility.Visible;
        LyricsScroll.Visibility = Visibility.Collapsed;
    }

    private void HideMessage()
    {
        LyricsMessage.Visibility = Visibility.Collapsed;
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

    private sealed record LyricsLineVisual(Border Border, TextBlock Text);
}
