using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland.Services;

public static class ArtworkAccent
{
    public static readonly Color Fallback = Color.FromRgb(0xF6, 0xA6, 0xC8);

    private static BitmapImage? _cachedArtwork;
    private static Color _cachedColor = Fallback;
    private static bool _hasCache;

    public static Color Extract(BitmapImage? artwork)
    {
        if (artwork is null)
        {
            return Fallback;
        }

        if (_hasCache && ReferenceEquals(artwork, _cachedArtwork))
        {
            return _cachedColor;
        }

        var color = Sample(artwork);
        _cachedArtwork = artwork;
        _cachedColor = color;
        _hasCache = true;
        return color;
    }

    private static Color Sample(BitmapImage artwork)
    {
        try
        {
            BitmapSource source = artwork;
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            var scale = Math.Min(1.0, 48.0 / Math.Max(1, source.PixelWidth));
            if (scale < 1)
            {
                source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            }

            var width = source.PixelWidth;
            var height = source.PixelHeight;
            if (width < 2 || height < 2)
            {
                return Fallback;
            }

            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            const int bucketCount = 16 * 16 * 16;
            var weights = new double[bucketCount];
            var reds = new double[bucketCount];
            var greens = new double[bucketCount];
            var blues = new double[bucketCount];

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                var a = pixels[i + 3];
                if (a < 128)
                {
                    continue;
                }

                var rf = r / 255.0;
                var gf = g / 255.0;
                var bf = b / 255.0;
                var max = Math.Max(rf, Math.Max(gf, bf));
                var min = Math.Min(rf, Math.Min(gf, bf));
                var l = (max + min) / 2;
                if (l < 0.10 || l > 0.93)
                {
                    continue;
                }

                var s = max <= min ? 0 : (max - min) / (1 - Math.Abs(2 * l - 1));
                if (s < 0.06)
                {
                    continue;
                }

                var weight = (0.03 + s * s) * (0.35 + l);
                var bucket = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
                weights[bucket] += weight;
                reds[bucket] += r * weight;
                greens[bucket] += g * weight;
                blues[bucket] += b * weight;
            }

            var best = -1;
            var bestWeight = 0.0;
            for (var i = 0; i < bucketCount; i++)
            {
                if (weights[i] > bestWeight)
                {
                    bestWeight = weights[i];
                    best = i;
                }
            }

            if (best < 0 || bestWeight <= 0)
            {
                return Fallback;
            }

            var rAvg = reds[best] / bestWeight;
            var gAvg = greens[best] / bestWeight;
            var bAvg = blues[best] / bestWeight;

            var color = Color.FromRgb((byte)rAvg, (byte)gAvg, (byte)bAvg);
            RgbToHsl(color, out var hue, out var saturation, out var lightness);

            if (saturation < 0.10)
            {
                return Fallback;
            }

            saturation = Math.Clamp(Math.Max(saturation, 0.48) * 1.08, 0.44, 0.84);
            lightness = Math.Clamp(lightness, 0.56, 0.72);

            var adjusted = HslToRgb(hue, saturation, lightness);
            return Color.FromRgb(
                (byte)Math.Clamp(adjusted.R, 0, 255),
                (byte)Math.Clamp(adjusted.G, 0, 255),
                (byte)Math.Clamp(adjusted.B, 0, 255));
        }
        catch
        {
            return Fallback;
        }
    }

    private static void RgbToHsl(Color color, out double hue, out double saturation, out double lightness)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        lightness = (max + min) / 2;
        if (delta <= 0.00001)
        {
            hue = 0;
            saturation = 0;
            return;
        }

        saturation = delta / (1 - Math.Abs(2 * lightness - 1));
        if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * ((b - r) / delta + 2);
        }
        else
        {
            hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }
    }

    private static (double R, double G, double B) HslToRgb(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return ((r + m) * 255, (g + m) * 255, (b + m) * 255);
    }
}
