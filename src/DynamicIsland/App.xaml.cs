using System.Threading;
using System.Windows;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private SettingsService? _settings;
    private MediaService? _media;
    private AudioService? _audio;
    private BrightnessService? _brightness;
    private BatteryService? _battery;
    private NetworkService? _network;
    private FullscreenWatcher? _fullscreen;
    private TrayService? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "DynamicIsland.SingleInstance", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _settings = new SettingsService();
        _settings.Current.StartWithWindows = StartupService.IsEnabled();

        _media = new MediaService();
        _audio = new AudioService();
        _brightness = new BrightnessService();
        _brightness.Start();
        _battery = new BatteryService();
        _network = new NetworkService();
        _fullscreen = new FullscreenWatcher();

        var window = new MainWindow(
            _settings,
            _media,
            _audio,
            _brightness,
            _battery,
            _network,
            _fullscreen);
        MainWindow = window;
        window.Show();

        _tray = new TrayService(window, _settings, () => Shutdown());

        await _media.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _media?.Dispose();
        _audio?.Dispose();
        _brightness?.Dispose();
        _battery?.Dispose();
        _network?.Dispose();
        _fullscreen?.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore.
            }
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
