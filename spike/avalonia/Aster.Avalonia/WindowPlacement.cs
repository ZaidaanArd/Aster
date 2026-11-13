namespace Aster.Avalonia;

public static class WindowPlacement
{
    public static int CenteredLeft(int screenX, int screenWidth, double logicalWidth, double scaling)
    {
        var width = (int)Math.Round(logicalWidth * scaling);
        return screenX + (screenWidth - width) / 2;
    }
}
