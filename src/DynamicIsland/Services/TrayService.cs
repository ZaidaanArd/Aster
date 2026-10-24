using System.Drawing;
using Forms = System.Windows.Forms;

namespace DynamicIsland.Services;

public sealed class TrayService : IDisposable
{
    private readonly MainWindow _window;
    private readonly SettingsService _settings;
    private readonly Action _exit;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _showHideItem;
    private readonly Forms.ToolStripMenuItem _startWithWindowsItem;
    private readonly Forms.ToolStripMenuItem _minimalItem;
    private readonly Forms.ToolStripMenuItem _mascotItem;
    private readonly Forms.ToolStripMenuItem _fullscreenItem;
    private readonly Icon _icon;

    public TrayService(MainWindow window, SettingsService settings, Action exit)
    {
        _window = window;
        _settings = settings;
        _exit = exit;

        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) => Refresh();

        _showHideItem = new Forms.ToolStripMenuItem("Hide island");
        _showHideItem.Click += (_, _) =>
        {
            _window.ToggleVisibility();
            Refresh();
        };
        menu.Items.Add(_showHideItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        _startWithWindowsItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true
        };
        _startWithWindowsItem.Click += (_, _) =>
            _window.SetStartWithWindows(_startWithWindowsItem.Checked);
        menu.Items.Add(_startWithWindowsItem);

        _minimalItem = new Forms.ToolStripMenuItem("Minimal notch")
        {
            CheckOnClick = true
        };
        _minimalItem.Click += (_, _) => _window.SetMinimalNotch(_minimalItem.Checked);
        menu.Items.Add(_minimalItem);

        _mascotItem = new Forms.ToolStripMenuItem("Show mascot")
        {
            CheckOnClick = true
        };
        _mascotItem.Click += (_, _) => _window.SetShowMascot(_mascotItem.Checked);
        menu.Items.Add(_mascotItem);

        _fullscreenItem = new Forms.ToolStripMenuItem("Hide in fullscreen")
        {
            CheckOnClick = true
        };
        _fullscreenItem.Click += (_, _) => _window.SetHideInFullscreen(_fullscreenItem.Checked);
        menu.Items.Add(_fullscreenItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var aboutItem = new Forms.ToolStripMenuItem("About Dynamic Island");
        aboutItem.Click += (_, _) => _window.ShowAbout();
        menu.Items.Add(aboutItem);

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => _exit();
        menu.Items.Add(exitItem);

        _icon = LoadIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Dynamic Island",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) =>
        {
            _window.ToggleVisibility();
            Refresh();
        };

        Refresh();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/icon.ico"))?.Stream;
            if (stream is not null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
            // Fall through to the system icon.
        }

        return SystemIcons.Application;
    }

    private void Refresh()
    {
        _showHideItem.Text = _window.IslandVisible ? "Hide island" : "Show island";
        _startWithWindowsItem.Checked = _settings.Current.StartWithWindows;
        _minimalItem.Checked = _settings.Current.MinimalNotch;
        _mascotItem.Checked = _settings.Current.ShowMascot;
        _fullscreenItem.Checked = _settings.Current.HideInFullscreen;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
