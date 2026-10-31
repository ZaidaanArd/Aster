using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly LyricsService _lyrics;
    private readonly WaterService _water;
    private readonly MainWindow _mainWindow;
    private bool _loading;

    public SettingsWindow(
        SettingsService settings,
        LyricsService lyrics,
        WaterService water,
        MainWindow mainWindow,
        double islandHeight)
    {
        _settings = settings;
        _lyrics = lyrics;
        _water = water;
        _mainWindow = mainWindow;

        InitializeComponent();
        PositionBelowIsland(islandHeight);
        LoadFromSettings();

        _water.StateChanged += OnWaterStateChanged;
        Closed += OnSettingsWindowClosed;

        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionText.Text = $"Dynamic Island {version?.ToString(3) ?? "0.8.0"}";
        DataPathText.Text = _settings.FilePath;
    }

    private void PositionBelowIsland(double islandHeight)
    {
        var workArea = SystemParameters.WorkArea;
        const double gap = 8;

        var top = islandHeight + gap;
        if (top + Height > workArea.Bottom)
        {
            top = workArea.Bottom - Height;
        }

        if (top < workArea.Top)
        {
            top = workArea.Top;
        }

        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = top;
    }

    private void LoadFromSettings()
    {
        _loading = true;
        var settings = _settings.Current;

        ChkStartup.IsChecked = settings.StartWithWindows;
        ChkFullscreen.IsChecked = settings.HideInFullscreen;
        ChkMascot.IsChecked = settings.ShowMascot;
        ChkMinimal.IsChecked = settings.MinimalNotch;
        ChkVolume.IsChecked = settings.VolumeToast;
        ChkBrightness.IsChecked = settings.BrightnessToast;
        ChkGlass.IsChecked = settings.GlassBackground;
        ChkBouncy.IsChecked = settings.BouncySpring;
        ChkLyrics.IsChecked = settings.LyricsEnabled;
        ChkChime.IsChecked = settings.FocusChime;
        ChkWater.IsChecked = settings.WaterReminderEnabled;

        TxtFocus.Text = settings.FocusMinutes.ToString();
        TxtShort.Text = settings.ShortBreakMinutes.ToString();
        TxtLong.Text = settings.LongBreakMinutes.ToString();
        TxtWaterInterval.Text = Math.Clamp(settings.WaterIntervalMinutes, 1, 240).ToString();
        TxtWaterStart.Text = Math.Clamp(settings.WaterActiveStartHour, 0, 23).ToString();
        TxtWaterEnd.Text = Math.Clamp(settings.WaterActiveEndHour, 0, 23).ToString();
        TxtWaterGoal.Text = Math.Clamp(settings.WaterDailyGoal, 1, 20).ToString();

        var lyricColor = ColorHex.TryParse(settings.LyricsHighlightColor, out var parsedColor)
            ? parsedColor
            : Color.FromRgb(0x7C, 0x9C, 0xFF);
        var lyricHex = ColorHex.Format(lyricColor);
        TxtLyricsColor.Text = lyricHex;
        UpdateLyricsColorPreview(lyricColor);
        UpdateLyricsColorSwatches(lyricHex);

        _loading = false;
        UpdateWaterStatus();
    }

    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (ReferenceEquals(sender, ChkStartup))
        {
            _mainWindow.SetStartWithWindows(ChkStartup.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkFullscreen))
        {
            _mainWindow.SetHideInFullscreen(ChkFullscreen.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkMascot))
        {
            _mainWindow.SetShowMascot(ChkMascot.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkMinimal))
        {
            _mainWindow.SetMinimalNotch(ChkMinimal.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkVolume))
        {
            _settings.Current.VolumeToast = ChkVolume.IsChecked == true;
            _settings.Save();
        }
        else if (ReferenceEquals(sender, ChkBrightness))
        {
            _settings.Current.BrightnessToast = ChkBrightness.IsChecked == true;
            _settings.Save();
        }
        else if (ReferenceEquals(sender, ChkGlass))
        {
            _mainWindow.SetGlassBackground(ChkGlass.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkBouncy))
        {
            _mainWindow.SetBouncySpring(ChkBouncy.IsChecked == true);
        }
        else if (ReferenceEquals(sender, ChkLyrics))
        {
            _settings.Current.LyricsEnabled = ChkLyrics.IsChecked == true;
            _settings.Save();
        }
        else if (ReferenceEquals(sender, ChkChime))
        {
            _settings.Current.FocusChime = ChkChime.IsChecked == true;
            _settings.Save();
        }
        else if (ReferenceEquals(sender, ChkWater))
        {
            _settings.Current.WaterReminderEnabled = ChkWater.IsChecked == true;
            _settings.Save();
            UpdateWaterStatus();
        }
    }

    private void OnLyricsColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            ApplyLyricsColor(hex);
        }
    }

    private void OnLyricsColorLostFocus(object sender, RoutedEventArgs e)
    {
        ApplyLyricsColor(TxtLyricsColor.Text);
    }

    private void OnLyricsColorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyLyricsColor(TxtLyricsColor.Text);
            Keyboard.ClearFocus();
        }
    }

    private void ApplyLyricsColor(string text)
    {
        if (_loading)
        {
            return;
        }

        if (!ColorHex.TryParse(text, out var color))
        {
            var fallback = ColorHex.TryParse(_settings.Current.LyricsHighlightColor, out var current)
                ? current
                : Color.FromRgb(0x7C, 0x9C, 0xFF);
            var fallbackHex = ColorHex.Format(fallback);
            TxtLyricsColor.Text = fallbackHex;
            UpdateLyricsColorPreview(fallback);
            UpdateLyricsColorSwatches(fallbackHex);
            return;
        }

        var hex = ColorHex.Format(color);
        _settings.Current.LyricsHighlightColor = hex;
        _settings.Save();

        TxtLyricsColor.Text = hex;
        UpdateLyricsColorPreview(color);
        UpdateLyricsColorSwatches(hex);
    }

    private void UpdateLyricsColorPreview(Color color)
    {
        LyricsColorPreview.Background = new SolidColorBrush(color);
    }

    private void UpdateLyricsColorSwatches(string hex)
    {
        var idleBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));

        foreach (var button in LyricsColorSwatches.Children.OfType<Button>())
        {
            var selected = button.Tag is string tag &&
                string.Equals(tag, hex, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = selected ? Brushes.White : idleBrush;
            button.BorderThickness = new Thickness(selected ? 1.5 : 1);
        }
    }

    private void ApplyDurations()
    {
        if (_loading)
        {
            return;
        }

        var focus = ParseMinutes(TxtFocus.Text, _settings.Current.FocusMinutes);
        var shortBreak = ParseMinutes(TxtShort.Text, _settings.Current.ShortBreakMinutes);
        var longBreak = ParseMinutes(TxtLong.Text, _settings.Current.LongBreakMinutes);

        _settings.Current.FocusMinutes = focus;
        _settings.Current.ShortBreakMinutes = shortBreak;
        _settings.Current.LongBreakMinutes = longBreak;

        TxtFocus.Text = focus.ToString();
        TxtShort.Text = shortBreak.ToString();
        TxtLong.Text = longBreak.ToString();

        _settings.Save();
    }

    private static int ParseMinutes(string text, int fallback)
    {
        if (!int.TryParse(text, out var value))
        {
            value = fallback;
        }

        return Math.Clamp(value, 1, 180);
    }

    private void OnNumericPreview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => !char.IsDigit(character));
    }

    private void OnDurationLostFocus(object sender, RoutedEventArgs e)
    {
        ApplyDurations();
    }

    private void OnDurationKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyDurations();
            Keyboard.ClearFocus();
        }
    }

    private void ApplyWaterSettings()
    {
        if (_loading)
        {
            return;
        }

        var interval = ParseRange(TxtWaterInterval.Text, _settings.Current.WaterIntervalMinutes, 1, 240);
        var start = ParseRange(TxtWaterStart.Text, _settings.Current.WaterActiveStartHour, 0, 23);
        var end = ParseRange(TxtWaterEnd.Text, _settings.Current.WaterActiveEndHour, 0, 23);
        var goal = ParseRange(TxtWaterGoal.Text, _settings.Current.WaterDailyGoal, 1, 20);

        _settings.Current.WaterIntervalMinutes = interval;
        _settings.Current.WaterActiveStartHour = start;
        _settings.Current.WaterActiveEndHour = end;
        _settings.Current.WaterDailyGoal = goal;

        TxtWaterInterval.Text = interval.ToString();
        TxtWaterStart.Text = start.ToString();
        TxtWaterEnd.Text = end.ToString();
        TxtWaterGoal.Text = goal.ToString();

        _settings.Save();
        UpdateWaterStatus();
    }

    private static int ParseRange(string text, int fallback, int minimum, int maximum)
    {
        if (!int.TryParse(text, out var value))
        {
            value = fallback;
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private void OnWaterLostFocus(object sender, RoutedEventArgs e)
    {
        ApplyWaterSettings();
    }

    private void OnWaterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyWaterSettings();
            Keyboard.ClearFocus();
        }
    }

    private void OnWaterLogClick(object sender, RoutedEventArgs e)
    {
        _water.LogDrink();
        UpdateWaterStatus();
    }

    private void OnWaterPreviewClick(object sender, RoutedEventArgs e)
    {
        _water.Preview();
        UpdateWaterStatus();
    }

    private void OnWaterResetClick(object sender, RoutedEventArgs e)
    {
        _water.ResetToday();
        UpdateWaterStatus();
    }

    private void OnWaterStateChanged()
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateWaterStatus();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(UpdateWaterStatus));
        }
    }

    private void UpdateWaterStatus()
    {
        var settings = _settings.Current;
        var count = _water.GlassesToday;
        var goal = _water.Goal;
        var text = $"{count} of {goal} glasses today";

        if (!settings.WaterReminderEnabled)
        {
            text += " \u00B7 reminders off";
        }
        else if (_water.GoalReached)
        {
            text += " \u00B7 goal reached";
        }
        else if (_water.NextReminderAt is { } next)
        {
            text += $" \u00B7 next {next:HH:mm}";
        }

        WaterStatusText.Text = text;
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        _water.StateChanged -= OnWaterStateChanged;
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(_settings.FilePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
    }

    private void OnClearLyricsCacheClick(object sender, RoutedEventArgs e)
    {
        _lyrics.ClearCache();
        MessageBox.Show(
            this,
            "Lyrics cache cleared. The next song will be fetched from LRCLIB again.",
            "Dynamic Island",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse is released immediately.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
