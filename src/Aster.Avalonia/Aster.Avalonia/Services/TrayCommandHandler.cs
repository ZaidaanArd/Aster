namespace Aster.Avalonia.Services;

public sealed class TrayCommandHandler
{
    private readonly Action _toggleVisibility;
    private readonly Func<bool> _isAutostart;
    private readonly Func<bool, bool> _setAutostart;
    private readonly Action _openSettings;
    private readonly Action _quit;

    public TrayCommandHandler(
        Action toggleVisibility,
        Func<bool> isAutostart,
        Func<bool, bool> setAutostart,
        Action openSettings,
        Action quit)
    {
        _toggleVisibility = toggleVisibility;
        _isAutostart = isAutostart;
        _setAutostart = setAutostart;
        _openSettings = openSettings;
        _quit = quit;
    }

    public void ToggleVisibility() => _toggleVisibility();

    public bool ToggleAutostart()
    {
        var next = !_isAutostart();
        return _setAutostart(next) ? next : _isAutostart();
    }

    public void OpenSettings() => _openSettings();
    public void Quit() => _quit();
}
