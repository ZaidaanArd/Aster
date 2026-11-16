namespace Aster.UI.Abstractions;

public interface IWindowPlacement
{
    bool IsCursorInside(IntPtr handle);
    uint GetDpi(IntPtr handle);
    void SetBounds(IntPtr handle, int x, int y, int width, int height);
}
