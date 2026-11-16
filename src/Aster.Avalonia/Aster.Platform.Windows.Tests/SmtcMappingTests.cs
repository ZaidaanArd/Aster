using Aster.Platform.Windows.Media;

namespace Aster.Platform.Windows.Tests;

public class SmtcMappingTests
{
    [Theory]
    [InlineData("SpotifyAB!Spotify", "Spotify")]
    [InlineData("MSEdge", "Edge")]
    [InlineData("Chrome", "Chrome")]
    [InlineData("1234567890ABCDEF", "Browser")]
    [InlineData("", "Unknown")]
    public void FriendlySourceMapsKnownApps(string appId, string expected)
    {
        Assert.Equal(expected, SmtcMediaSession.FriendlySource(appId));
    }

    [Fact]
    public async Task NullArtworkReturnsNull()
    {
        Assert.Null(await ArtworkReader.ReadAsync(null));
    }
}

public class SmtcTimelineTests
{
    [Theory]
    [InlineData(0, 0, 120, 0, 120)]
    [InlineData(5, 5, 65, 0, 60)]
    [InlineData(5, 30, 65, 25, 60)]
    public void NormalizesStartTime(
        double startSeconds,
        double positionSeconds,
        double endSeconds,
        double expectedPosition,
        double expectedDuration)
    {
        var result = SmtcTimeline.Normalize(
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(positionSeconds),
            TimeSpan.FromSeconds(endSeconds));

        Assert.Equal(expectedPosition, result.Position.TotalSeconds, 3);
        Assert.Equal(expectedDuration, result.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void SeekAddsStartTime()
    {
        var target = SmtcTimeline.ToSeekPosition(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30));

        Assert.Equal(35, target.TotalSeconds, 3);
    }
}
