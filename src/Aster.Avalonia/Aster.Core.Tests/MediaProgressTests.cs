using Aster.Core.Logic;
using Aster.Core.Models;

namespace Aster.Core.Tests;

public class MediaProgressTests
{
    private static MediaSnapshot Snapshot(
        PlaybackState state,
        TimeSpan position,
        TimeSpan duration,
        DateTimeOffset updatedAt) =>
        new("T", "A", "", null, "k", state, position, duration, "test", updatedAt);

    [Fact]
    public void PlayingAddsElapsedTime()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(PlaybackState.Playing, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), now.AddSeconds(-5));

        Assert.Equal(TimeSpan.FromSeconds(15), MediaProgress.Estimate(snapshot, now));
    }

    [Fact]
    public void PausedDoesNotAdvance()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(PlaybackState.Paused, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), now.AddSeconds(-5));

        Assert.Equal(TimeSpan.FromSeconds(10), MediaProgress.Estimate(snapshot, now));
    }

    [Fact]
    public void EstimateClampsToDuration()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(PlaybackState.Playing, TimeSpan.FromSeconds(179), TimeSpan.FromMinutes(3), now.AddSeconds(-5));

        Assert.Equal(TimeSpan.FromMinutes(3), MediaProgress.Estimate(snapshot, now));
    }

    [Fact]
    public void ClampSeekHandlesUnknownDuration()
    {
        Assert.Equal(TimeSpan.Zero, MediaProgress.ClampSeek(TimeSpan.FromSeconds(5), TimeSpan.Zero));
    }

    [Fact]
    public void ClampSeekKeepsValidPosition()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), MediaProgress.ClampSeek(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(10), MediaProgress.ClampSeek(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10)));
    }
}
