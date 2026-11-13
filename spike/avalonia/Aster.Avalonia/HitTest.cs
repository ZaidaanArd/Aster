namespace Aster.Avalonia;

public static class HitTest
{
    public static bool IsInteractive(
        double x,
        double y,
        double windowWidth,
        double windowHeight,
        double scaling)
    {
        var islandWidth = 300 * scaling;
        var islandHeight = 38 * scaling;
        var left = (windowWidth * scaling - islandWidth) / 2;

        return x >= left
            && x <= left + islandWidth
            && y >= 0
            && y <= islandHeight
            && y <= windowHeight * scaling;
    }
}
