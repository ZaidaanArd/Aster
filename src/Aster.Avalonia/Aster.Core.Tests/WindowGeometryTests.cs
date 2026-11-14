using Aster.Core.Logic;

namespace Aster.Core.Tests;

public class WindowGeometryTests
{
    [Theory]
    [InlineData(0, 1000, 300, 1.0, 350)]
    [InlineData(0, 1000, 300, 1.25, 312)]
    [InlineData(0, 1000, 300, 1.5, 275)]
    public void CentersLeft(int screenX, int screenWidth, double logicalWidth, double scaling, int expected)
    {
        Assert.Equal(expected, WindowGeometry.CenteredLeft(screenX, screenWidth, logicalWidth, scaling));
    }

    [Fact]
    public void MinimalCollapsedSizesAreUsed()
    {
        Assert.Equal(160, WindowGeometry.CollapsedWidthFor(minimal: true));
        Assert.Equal(12, WindowGeometry.CollapsedHeightFor(minimal: true));
        Assert.Equal(300, WindowGeometry.CollapsedWidthFor(minimal: false));
        Assert.Equal(38, WindowGeometry.CollapsedHeightFor(minimal: false));
    }
}
