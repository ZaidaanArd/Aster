using Aster.Avalonia.ViewModels;

namespace Aster.Avalonia.Tests;

public class ShellViewModelTests
{
    [Fact]
    public void ClockUsesCurrentCultureFormat()
    {
        var vm = new ShellViewModel();
        vm.UpdateClock(new DateTimeOffset(2026, 11, 14, 14, 5, 0, TimeSpan.Zero));

        Assert.Equal("14:05", vm.ClockText);
    }

    [Fact]
    public void DateUsesCurrentCulture()
    {
        var vm = new ShellViewModel();
        vm.UpdateClock(new DateTimeOffset(2026, 11, 14, 14, 5, 0, TimeSpan.Zero));

        Assert.False(string.IsNullOrWhiteSpace(vm.DateText));
    }

    [Fact]
    public void OpacityTracksExpansionProgress()
    {
        var vm = new ShellViewModel();

        vm.ExpansionProgress = 0;
        Assert.Equal(1, vm.CollapsedOpacity);
        Assert.Equal(0, vm.ExpandedOpacity);

        vm.ExpansionProgress = 1;
        Assert.Equal(0, vm.CollapsedOpacity);
        Assert.Equal(1, vm.ExpandedOpacity);
    }
}
