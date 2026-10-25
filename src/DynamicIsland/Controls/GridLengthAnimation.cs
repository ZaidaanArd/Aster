using System.Windows;
using System.Windows.Media.Animation;

namespace DynamicIsland.Controls;

public sealed class GridLengthAnimation : AnimationTimeline
{
    public GridLength From { get; set; }
    public GridLength To { get; set; }
    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue,
        object defaultDestinationValue,
        AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        var from = From.Value;
        var to = To.Value;
        var value = from + (to - from) * progress;
        return new GridLength(value, GridUnitType.Pixel);
    }
}
