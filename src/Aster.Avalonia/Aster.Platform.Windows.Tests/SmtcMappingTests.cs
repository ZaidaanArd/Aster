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
