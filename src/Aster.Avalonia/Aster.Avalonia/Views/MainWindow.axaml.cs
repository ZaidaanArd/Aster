using Avalonia;
using Avalonia.Controls;

namespace Aster.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => PositionAtTopCenter();
    }

    private void PositionAtTopCenter()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var physicalWidth = (int)Math.Round(Width * screen.Scaling);
        var left = screen.Bounds.X + (screen.Bounds.Width - physicalWidth) / 2;

        Position = new PixelPoint(left, screen.Bounds.Y);
    }
}
