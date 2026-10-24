using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class FocusView : UserControl
{
    private static readonly Brush ActiveChipBrush = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
    private static readonly Brush InactiveChipBrush = Brushes.Transparent;
    private static readonly Brush ActiveTextBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush InactiveTextBrush = new SolidColorBrush(Color.FromRgb(0x94, 0x94, 0x9C));

    private readonly FocusService _focus;
    private bool _initialized;

    public FocusView(FocusService focus)
    {
        _focus = focus;
        InitializeComponent();

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
        _focus.Tick += OnFocusTick;
        _focus.StateChanged += OnFocusStateChanged;
        UpdateAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _focus.Tick -= OnFocusTick;
        _focus.StateChanged -= OnFocusStateChanged;
        _initialized = false;
    }

    private void OnFocusTick() => UpdateProgress();

    private void OnFocusStateChanged() => UpdateAll();

    private void UpdateAll()
    {
        UpdateProgress();

        StartGlyph.Text = _focus.IsRunning ? "\uE769" : "\uE768";

        FocusChip.Content = $"Focus {Minutes(_focus.DurationFor(FocusPhase.Focus))}";
        ShortChip.Content = $"Short {Minutes(_focus.DurationFor(FocusPhase.ShortBreak))}";
        LongChip.Content = $"Long {Minutes(_focus.DurationFor(FocusPhase.LongBreak))}";

        SetChipActive(FocusChip, _focus.Phase == FocusPhase.Focus);
        SetChipActive(ShortChip, _focus.Phase == FocusPhase.ShortBreak);
        SetChipActive(LongChip, _focus.Phase == FocusPhase.LongBreak);

        if (_focus.IsComplete)
        {
            SessionText.Text = _focus.Phase == FocusPhase.Focus
                ? "Complete \u00B7 take a break"
                : "Break complete \u00B7 back to focus";
            BreakButton.Visibility = Visibility.Visible;
            BreakButton.Content = _focus.Phase == FocusPhase.Focus
                ? (_focus.SuggestBreakPhase() == FocusPhase.LongBreak ? "Start long break" : "Start short break")
                : "Start focus";
        }
        else
        {
            SessionText.Text = _focus.CompletedSessionsToday == 1
                ? "1 session today"
                : $"{_focus.CompletedSessionsToday} sessions today";
            BreakButton.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateProgress()
    {
        var countdown = FormatTime(_focus.Remaining);
        if (CountdownText.Text != countdown)
        {
            CountdownText.Text = countdown;
        }

        var total = _focus.Duration.TotalMilliseconds;
        var ratio = total > 0
            ? Math.Clamp(1 - _focus.Remaining.TotalMilliseconds / total, 0, 1)
            : 0;

        var width = ProgressHit.ActualWidth;
        if (width > 0)
        {
            ProgressFill.Width = width * ratio;
        }
    }

    private static void SetChipActive(Button chip, bool active)
    {
        chip.Background = active ? ActiveChipBrush : InactiveChipBrush;
        chip.Foreground = active ? ActiveTextBrush : InactiveTextBrush;
    }

    private static int Minutes(TimeSpan duration) => (int)Math.Round(duration.TotalMinutes);

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

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        _focus.Toggle();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _focus.Reset();
    }

    private void OnFocusChipClick(object sender, RoutedEventArgs e)
    {
        _focus.SetPhase(FocusPhase.Focus);
    }

    private void OnShortChipClick(object sender, RoutedEventArgs e)
    {
        _focus.SetPhase(FocusPhase.ShortBreak);
    }

    private void OnLongChipClick(object sender, RoutedEventArgs e)
    {
        _focus.SetPhase(FocusPhase.LongBreak);
    }

    private void OnBreakClick(object sender, RoutedEventArgs e)
    {
        _focus.StartSuggestedNext();
    }

    private void OnProgressSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateProgress();
    }
}
