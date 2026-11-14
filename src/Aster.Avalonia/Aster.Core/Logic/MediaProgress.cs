using Aster.Core.Models;

namespace Aster.Core.Logic;

public static class MediaProgress
{
    public static TimeSpan Estimate(MediaSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.Duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var position = snapshot.Position;
        if (snapshot.Playback == PlaybackState.Playing)
        {
            var elapsed = now - snapshot.UpdatedAt;
            if (elapsed > TimeSpan.Zero)
            {
                position += elapsed;
            }
        }

        return ClampSeek(position, snapshot.Duration);
    }

    public static TimeSpan ClampSeek(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || position <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return position >= duration ? duration : position;
    }
}
