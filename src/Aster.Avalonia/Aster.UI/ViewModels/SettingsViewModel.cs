using Aster.Core.Interfaces;
using Aster.Core.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAppLog _log;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimalNotch;
    [ObservableProperty] private bool _hideInFullscreen;
    [ObservableProperty] private bool _glassBackground;
    [ObservableProperty] private bool _bouncySpring;

    public SettingsViewModel(ISettingsStore store, IAppLog log, string autostartLabel)
    {
        _store = store;
        _log = log;
        AutostartLabel = autostartLabel;
        var current = store.Current;
        _startWithWindows = current.StartWithWindows;
        _minimalNotch = current.MinimalNotch;
        _hideInFullscreen = current.HideInFullscreen;
        _glassBackground = current.GlassBackground;
        _bouncySpring = current.BouncySpring;
        _saveTimer.Tick += async (_, _) =>
        {
            _saveTimer.Stop();
            await SaveNowAsync();
        };

        _store.Changed += (_, settings) =>
        {
            if (settings.StartWithWindows == StartWithWindows)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _startWithWindows = settings.StartWithWindows;
                OnPropertyChanged(nameof(StartWithWindows));
            });
        };
    }

    public string AutostartLabel { get; }
    public bool CanSave => _store.CanSave;
    public string? Warning => _store.LoadWarning;

    public event EventHandler<AppSettings>? Saved;

    public async Task FlushAsync()
    {
        _saveTimer.Stop();
        await SaveNowAsync();
    }

    public async Task SaveNowAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var settings = new AppSettings(
            SettingsSchema.CurrentVersion,
            StartWithWindows,
            MinimalNotch,
            HideInFullscreen,
            GlassBackground,
            BouncySpring);

        await _store.SaveAsync(settings);
        Saved?.Invoke(this, settings);
    }

    partial void OnStartWithWindowsChanged(bool value) => ScheduleSave();
    partial void OnMinimalNotchChanged(bool value) => ScheduleSave();
    partial void OnHideInFullscreenChanged(bool value) => ScheduleSave();
    partial void OnGlassBackgroundChanged(bool value) => ScheduleSave();
    partial void OnBouncySpringChanged(bool value) => ScheduleSave();

    private void ScheduleSave()
    {
        if (!CanSave)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
