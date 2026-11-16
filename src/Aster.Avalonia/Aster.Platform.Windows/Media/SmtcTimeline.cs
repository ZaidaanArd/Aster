namespace Aster.Platform.Windows.Media;

public static class SmtcTimeline
{
    public static (TimeSpan Position, TimeSpan Duration) Normalize(
        TimeSpan start,
        TimeSpan position,
        TimeSpan end)
    {
        if (start < TimeSpan.Zero)
        {
            start = TimeSpan.Zero;
        }

        var duration = end > start ? end - start : TimeSpan.Zero;
        var relative = position - start;
        if (relative < TimeSpan.Zero)
        {
            relative = TimeSpan.Zero;
        }
        if (relative > duration)
        {
            relative = duration;
        }

        return (relative, duration);
    }

    public static TimeSpan ToSeekPosition(TimeSpan start, TimeSpan relative) =>
        start + relative;
}
