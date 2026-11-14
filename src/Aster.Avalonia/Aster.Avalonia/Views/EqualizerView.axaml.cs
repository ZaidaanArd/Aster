using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aster.Avalonia.Views;

public partial class EqualizerView : UserControl
{
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<EqualizerView, bool>(nameof(IsPlaying));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Random _random = new();

    public EqualizerView()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Animate();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsPlayingProperty)
            {
                UpdateTimer();
            }
        };
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private void UpdateTimer()
    {
        if (IsPlaying)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            Bar1.Height = 6;
            Bar2.Height = 10;
            Bar3.Height = 4;
        }
    }

    private void Animate()
    {
        Bar1.Height = 4 + _random.Next(0, 10);
        Bar2.Height = 4 + _random.Next(0, 14);
        Bar3.Height = 4 + _random.Next(0, 8);
    }
}
