using Aster.UI.Abstractions;
using Avalonia.Controls;

namespace Aster.Platform.Linux.Window;

public sealed class NoopWindowPlacement : IWindowPlacement
{
    public bool IsCursorInside(IntPtr handle) => false;
    public uint GetDpi(IntPtr handle) => 96;
    public void SetBounds(IntPtr handle, int x, int y, int width, int height) { }
}

public sealed class NoopWindowChrome : IWindowChrome
{
    public void Attach(Avalonia.Controls.Window window) { }
}
