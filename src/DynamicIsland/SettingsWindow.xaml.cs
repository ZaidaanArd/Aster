using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly MainWindow _mainWindow;
    private bool _loading;

    public SettingsWindow(SettingsService settings, MainWindow mainWindow)
    {
        _settings = settings;
        _mainWindow = mainWindow;

        InitializeComponent();
        PositionBelowIsland();
        LoadFromSettings();

        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionText.Text = $"Dynamic Island {version?.ToString(3) ?? "0.2.0"}";
        DataPathText.Text = _settings.FilePath;
    }

    private void PositionBelowIsland()
    {
        var workArea = SystemParameters.WorkArea;
        const double islandHeight = 252;
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
        ChkChime.IsChecked = settings.FocusChime;

        TxtFocus.Text = settings.FocusMinutes.ToString();
        TxtShort.Text = settings.ShortBreakMinutes.ToString();
        TxtLong.Text = settings.LongBreakMinutes.ToString();

        _loading = false;
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
        else if (ReferenceEquals(sender, ChkChime))
        {
            _settings.Current.FocusChime = ChkChime.IsChecked == true;
            _settings.Save();
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

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(_settings.FilePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
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
