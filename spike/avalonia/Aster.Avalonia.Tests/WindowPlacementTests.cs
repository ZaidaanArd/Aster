using Aster.Avalonia;
using Xunit;

namespace Aster.Avalonia.Tests;

public class WindowPlacementTests
{
    [Fact]
    public void CentersAt100Percent()
    {
        Assert.Equal(350, WindowPlacement.CenteredLeft(0, 1000, 300, 1.0));
    }

    [Fact]
    public void CentersAt150Percent()
    {
        Assert.Equal(275, WindowPlacement.CenteredLeft(0, 1000, 300, 1.5));
    }
}
