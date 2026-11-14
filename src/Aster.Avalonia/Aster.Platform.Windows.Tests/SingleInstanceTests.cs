using Aster.Platform.Windows;

namespace Aster.Platform.Windows.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void SecondInstanceIsNotFirst()
    {
        var name = "AsterTest-" + Guid.NewGuid().ToString("N");

        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }
}
