using Aster.Core.Models;

namespace Aster.Platform.Linux.Media;

public sealed record MprisPlayerState(
    string BusName,
    string SourceApp,
    MprisMetadata Metadata,
    PlaybackState Playback,
    TimeSpan Position,
    bool CanSeek,
    bool CanGoNext,
    bool CanGoPrevious,
    DateTimeOffset UpdatedAt);
