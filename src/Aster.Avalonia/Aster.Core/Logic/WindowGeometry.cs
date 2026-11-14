namespace Aster.Core.Logic;

public static class WindowGeometry
{
    public const double CollapsedWidth = 300;
    public const double CollapsedHeight = 38;
    public const double ExpandedWidth = 500;
    public const double ExpandedHeight = 252;
    public const double MinimalWidth = 160;
    public const double MinimalHeight = 12;
    public const double CornerRadius = 22;

    public static double CollapsedWidthFor(bool minimal) => minimal ? MinimalWidth : CollapsedWidth;
    public static double CollapsedHeightFor(bool minimal) => minimal ? MinimalHeight : CollapsedHeight;

    public static int CenteredLeft(int screenX, int screenWidth, double logicalWidth, double scaling)
    {
        var width = (int)Math.Round(logicalWidth * scaling);
        return screenX + (screenWidth - width) / 2;
    }
}
