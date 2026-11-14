using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aster.Avalonia.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private double _expansionProgress;
    [ObservableProperty] private string _clockText = "--:--";
    [ObservableProperty] private bool _isMinimal;

    public ShellViewModel()
    {
    }

    public double ExpandedOpacity => Math.Clamp((ExpansionProgress - 0.18) / 0.82, 0, 1);
    public double CollapsedOpacity => Math.Clamp(1 - ExpansionProgress * 3, 0, 1);

    partial void OnExpansionProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ExpandedOpacity));
        OnPropertyChanged(nameof(CollapsedOpacity));
    }

    public void UpdateClock(DateTimeOffset now) =>
        ClockText = now.ToString("HH:mm", CultureInfo.CurrentCulture);
}
