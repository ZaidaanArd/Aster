using Aster.Avalonia;
using Xunit;

namespace Aster.Avalonia.Tests;

public class HitTestTests
{
    [Fact]
    public void CenterOfPillIsInteractive()
    {
        Assert.True(HitTest.IsInteractive(380, 19, 760, 460, 1.0));
    }

    [Fact]
    public void LeftEdgeIsTransparent()
    {
        Assert.False(HitTest.IsInteractive(10, 19, 760, 460, 1.0));
    }

    [Fact]
    public void CenterAt150PercentIsInteractive()
    {
        Assert.True(HitTest.IsInteractive(500, 28, 760, 460, 1.5));
    }
}
