using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aster.Avalonia.ViewModels;

namespace Aster.Avalonia.Views;

public partial class ExpandedMediaView : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    public ExpandedMediaView()
    {
        InitializeComponent();

        // The slider's Thumb handles pointer events, so the bubbling handlers in XAML
        // never fired. Tunnel handlers with handledEventsToo keep IsSeeking and the
        // seek command in sync with a real thumb drag.
        SeekSlider.AddHandler(PointerPressedEvent, OnSeekPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SeekSlider.AddHandler(PointerReleasedEvent, OnSeekReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClick(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnSeekPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShellViewModel { Media: not null } vm)
        {
            vm.Media.IsSeeking = true;
        }
    }

    private void OnSeekReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ShellViewModel { Media: not null } vm)
        {
            vm.Media.IsSeeking = false;
            vm.Media.SeekCommand.Execute(SeekSlider.Value);
        }
    }
}
