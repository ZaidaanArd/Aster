using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aster.Avalonia;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            PositionAtTopCenter();
            WriteReadyFile();
        };
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm");
        _clock.Start();
    }

    private static void WriteReadyFile()
    {
        var path = Environment.GetEnvironmentVariable("ASTER_READY_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, Program.StartupClock.ElapsedMilliseconds.ToString());
        }
    }

    private void PositionAtTopCenter()
    {
        // Screens.Primary can be null on X11 servers without a primary-output hint
        // (for example Xvfb), so fall back to the first reported screen.
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var left = WindowPlacement.CenteredLeft(
            screen.Bounds.X,
            screen.Bounds.Width,
            Width,
            screen.Scaling);

        Position = new PixelPoint(left, screen.Bounds.Y);
    }
}
