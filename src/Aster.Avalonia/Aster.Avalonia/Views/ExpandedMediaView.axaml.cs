using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aster.Avalonia.ViewModels;

namespace Aster.Avalonia.Views;

public partial class ExpandedMediaView : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    public ExpandedMediaView() => InitializeComponent();

    private void OnSettingsClick(object? sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClick(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnSeekReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ShellViewModel { Media: not null } vm)
        {
            vm.Media.SeekCommand.Execute(vm.Media.PositionSeconds);
        }
    }
}
