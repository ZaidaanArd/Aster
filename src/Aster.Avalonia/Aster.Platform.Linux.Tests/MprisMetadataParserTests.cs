using Aster.Platform.Linux.Media;

namespace Aster.Platform.Linux.Tests;

public class MprisMetadataParserTests
{
    [Fact]
    public void ParsesTitleArtistAlbumDurationAndArtwork()
    {
        var metadata = new Dictionary<string, object?>
        {
            ["xesam:title"] = "Tone A",
            ["xesam:artist"] = new[] { "Aster Lab", "Guest" },
            ["xesam:album"] = "Phase 2",
            ["mpris:length"] = 120_000_000L,
            ["mpris:artUrl"] = "file:///tmp/cover.png",
            ["mpris:trackid"] = "/org/mpris/MediaPlayer2/Track/1"
        };

        var parsed = MprisMetadataParser.Parse(metadata);

        Assert.Equal("Tone A", parsed.Title);
        Assert.Equal("Aster Lab, Guest", parsed.Artist);
        Assert.Equal("Phase 2", parsed.Album);
        Assert.Equal(TimeSpan.FromMinutes(2), parsed.Duration);
        Assert.Equal("file:///tmp/cover.png", parsed.ArtUrl);
        Assert.Equal("/org/mpris/MediaPlayer2/Track/1", parsed.TrackId);
    }

    [Fact]
    public void UnknownMetadataUsesFallbacks()
    {
        var parsed = MprisMetadataParser.Parse(new Dictionary<string, object?>());

        Assert.Equal("Unknown track", parsed.Title);
        Assert.Equal(string.Empty, parsed.Artist);
        Assert.Equal(TimeSpan.Zero, parsed.Duration);
        Assert.Null(parsed.ArtUrl);
    }
}
