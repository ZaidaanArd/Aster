using Aster.Core.Logic;

namespace Aster.Core.Tests;

public class SpringStateTests
{
    [Fact]
    public void SpringSettlesOnTarget()
    {
        var spring = new SpringState { Value = 300, Velocity = 0 };

        var settled = false;
        for (var i = 0; i < 600 && !settled; i++)
        {
            settled = spring.Step(500, 1.0 / 60.0, bouncy: false);
        }

        Assert.True(settled);
        Assert.Equal(500, spring.Value, 3);
        Assert.Equal(0, spring.Velocity, 3);
    }

    [Fact]
    public void BouncySpringCanOvershoot()
    {
        var spring = new SpringState { Value = 300, Velocity = 0 };
        var max = 300.0;

        for (var i = 0; i < 30; i++)
        {
            spring.Step(500, 1.0 / 60.0, bouncy: true);
            max = Math.Max(max, spring.Value);
        }

        Assert.True(max > 500);
    }
}
