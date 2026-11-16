using Aster.Core.System;

namespace Aster.Core.Tests;

public class MutexSingleInstanceTests
{
    [Fact]
    public void SecondInstanceIsNotFirst()
    {
        var name = "AsterTest-" + Guid.NewGuid().ToString("N");

        using var first = new MutexSingleInstance(name);
        using var second = new MutexSingleInstance(name);

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }
}
