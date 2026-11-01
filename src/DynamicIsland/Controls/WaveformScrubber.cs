using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland.Controls;

public sealed class WaveformScrubber : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(WaveformScrubber),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeedProperty = DependencyProperty.Register(
        nameof(Seed),
        typeof(int),
        typeof(WaveformScrubber),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayedBrushProperty = DependencyProperty.Register(
        nameof(PlayedBrush),
        typeof(Brush),
        typeof(WaveformScrubber),
        new FrameworkPropertyMetadata(
            new SolidColorBrush(Color.FromRgb(0xF6, 0xA6, 0xC8)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(WaveformScrubber),
        new FrameworkPropertyMetadata(
            new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public int Seed
    {
        get => (int)GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public Brush PlayedBrush
    {
        get => (Brush)GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public event EventHandler<double>? SeekRequested;

    public WaveformScrubber()
    {
        Height = 16;
        Cursor = Cursors.Hand;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 8 || height < 6)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        const double barWidth = 2.5;
        const double gap = 2.0;
        const double step = barWidth + gap;

        var count = Math.Max(1, (int)Math.Floor((width + gap) / step));
        if (count < 1)
        {
            return;
        }

        var total = count * step - gap;
        var left = (width - total) / 2;
        var progress = Math.Clamp(Progress, 0, 1);
        var playedEdge = progress * width;
        var played = PlayedBrush;
        var track = TrackBrush;

        for (var i = 0; i < count; i++)
        {
            var noise = Hash(Seed, i) / (double)int.MaxValue;
            var wave = 0.5 + 0.5 * Math.Sin(i * 0.63 + (Seed % 29) * 0.37);
            var mix = 0.58 * noise + 0.42 * wave;
            var barHeight = 3.5 + (height - 4.0) * (0.12 + 0.88 * mix);
            var x = left + i * step;
            var y = (height - barHeight) / 2;
            var brush = x + barWidth / 2 <= playedEdge ? played : track;

            drawingContext.DrawRoundedRectangle(
                brush,
                null,
                new Rect(x, y, barWidth, barHeight),
                barWidth / 2,
                barWidth / 2);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (ActualWidth <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        SeekRequested?.Invoke(this, ratio);
        e.Handled = true;
    }

    private static int Hash(int seed, int index)
    {
        unchecked
        {
            var value = seed * 374761393 + index * 668265263;
            value = (value ^ (value >> 13)) * 1274126177;
            return (value ^ (value >> 16)) & 0x7FFFFFFF;
        }
    }
}
