using System.Globalization;
using System.Windows.Media;

namespace DynamicIsland.Services;

public static class ColorHex
{
    public const string DefaultLyricsHighlight = "#7C9CFF";

    public static bool TryParse(string? text, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6 ||
            !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
        return true;
    }

    public static string Format(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
