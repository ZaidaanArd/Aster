using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Aster.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _start;

    public TrayService(TrayCommandHandler commands, Func<bool> isAutostart)
    {
        var menu = new NativeMenu();

        var toggle = new NativeMenuItem("Show/Hide");
        toggle.Click += (_, _) => commands.ToggleVisibility();

        _start = new NativeMenuItem("Start with system")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isAutostart()
        };
        _start.Click += (_, _) =>
        {
            _start.IsChecked = commands.ToggleAutostart();
        };

        var settings = new NativeMenuItem("Settings");
        settings.Click += (_, _) => commands.OpenSettings();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => commands.Quit();

        menu.Items.Add(toggle);
        menu.Items.Add(_start);
        menu.Items.Add(settings);
        menu.Items.Add(quit);

        var iconPath = OperatingSystem.IsLinux()
            ? "avares://Aster.UI/Assets/tray.png"
            : "avares://Aster.UI/Assets/tray.ico";

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(iconPath))),
            ToolTipText = "Aster Lab Avalonia",
            Menu = menu,
            IsVisible = true
        };

        if (Application.Current is { } app)
        {
            TrayIcon.SetIcons(app, new TrayIcons { _tray });
        }
    }

    public void SetAutostartChecked(bool value) => _start.IsChecked = value;

    public void Dispose() => _tray.Dispose();
}
