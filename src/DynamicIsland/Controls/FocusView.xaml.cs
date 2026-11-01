using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public partial class FocusView : UserControl
{
    private const double RingCenter = 75;
    private const double RingRadius = 72;

    private static readonly Brush ActiveChipBrush = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xB8, 0x6B));
    private static readonly Brush InactiveChipBrush = Brushes.Transparent;
    private static readonly Brush ActiveTextBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x6B));
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

        StartButton.Content = _focus.IsRunning ? "Pause" : "Start";
        StartButton.ToolTip = _focus.IsRunning ? "Pause" : "Start";
        StartButton.Visibility = _focus.IsComplete ? Visibility.Collapsed : Visibility.Visible;

        FocusChip.Content = $"Focus {Minutes(_focus.DurationFor(FocusPhase.Focus))}";
        ShortChip.Content = $"Short {Minutes(_focus.DurationFor(FocusPhase.ShortBreak))}";
        LongChip.Content = $"Long {Minutes(_focus.DurationFor(FocusPhase.LongBreak))}";

        SetChipActive(FocusChip, _focus.Phase == FocusPhase.Focus);
        SetChipActive(ShortChip, _focus.Phase == FocusPhase.ShortBreak);
        SetChipActive(LongChip, _focus.Phase == FocusPhase.LongBreak);

        ModeLabelText.Text = _focus.Phase == FocusPhase.Focus ? "FOCUS SESSION" : "BREAK";
        FocusTitleText.Text = _focus.Phase switch
        {
            FocusPhase.Focus => "Focus session",
            FocusPhase.ShortBreak => "Short break",
            FocusPhase.LongBreak => "Long break",
            _ => "Focus session"
        };
        RingPhaseText.Text = _focus.Phase switch
        {
            FocusPhase.Focus => "DEEP WORK",
            FocusPhase.ShortBreak => "SHORT BREAK",
            FocusPhase.LongBreak => "LONG BREAK",
            _ => "DEEP WORK"
        };

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
            SessionText.Text = _focus.Phase switch
            {
                FocusPhase.Focus => $"Notifications muted while the timer runs. {SessionsText()}.",
                FocusPhase.ShortBreak => $"Step away for a moment. {SessionsText()}.",
                FocusPhase.LongBreak => $"A longer pause to reset. {SessionsText()}.",
                _ => $"Notifications muted while the timer runs. {SessionsText()}."
            };
            BreakButton.Visibility = Visibility.Collapsed;
        }
    }

    private string SessionsText()
    {
        var count = _focus.CompletedSessionsToday;
        return count == 1 ? "1 session today" : $"{count} sessions today";
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
            ? Math.Clamp(_focus.Remaining.TotalMilliseconds / total, 0, 1)
            : 0;

        UpdateRing(ratio);
    }

    private void UpdateRing(double ratio)
    {
        if (ratio >= 0.999)
        {
            ProgressRingFull.Visibility = Visibility.Visible;
            ProgressArc.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressRingFull.Visibility = Visibility.Collapsed;

        if (ratio <= 0.001)
        {
            ProgressArc.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressArc.Visibility = Visibility.Visible;

        var start = new Point(RingCenter, RingCenter - RingRadius);
        var angle = -90 + ratio * 360;
        var radians = angle * Math.PI / 180;
        var end = new Point(
            RingCenter + RingRadius * Math.Cos(radians),
            RingCenter + RingRadius * Math.Sin(radians));

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(RingRadius, RingRadius),
            0,
            ratio > 0.5,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ProgressArc.Data = geometry;
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
}
