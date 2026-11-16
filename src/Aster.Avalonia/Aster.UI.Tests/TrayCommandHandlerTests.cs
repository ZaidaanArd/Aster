using Aster.UI.Services;

namespace Aster.UI.Tests;

public class TrayCommandHandlerTests
{
    [Fact]
    public void ToggleAutostartUpdatesState()
    {
        var enabled = false;
        var handler = new TrayCommandHandler(
            toggleVisibility: () => { },
            isAutostart: () => enabled,
            setAutostart: value => { enabled = value; return true; },
            openSettings: () => { },
            quit: () => { });

        Assert.True(handler.ToggleAutostart());
        Assert.True(enabled);
    }

    [Fact]
    public void FailedAutostartKeepsPreviousState()
    {
        var enabled = false;
        var handler = new TrayCommandHandler(
            toggleVisibility: () => { },
            isAutostart: () => enabled,
            setAutostart: _ => false,
            openSettings: () => { },
            quit: () => { });

        Assert.False(handler.ToggleAutostart());
        Assert.False(enabled);
    }
}
