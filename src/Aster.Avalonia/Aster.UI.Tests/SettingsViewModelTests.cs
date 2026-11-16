using Aster.UI.ViewModels;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.UI.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task ToggleSavesAllFields()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows")
        {
            StartWithWindows = true,
            MinimalNotch = true,
            HideInFullscreen = false,
            GlassBackground = false,
            BouncySpring = false
        };

        await vm.SaveNowAsync();

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.StartWithWindows);
        Assert.True(store.Saved.MinimalNotch);
        Assert.False(store.Saved.HideInFullscreen);
        Assert.False(store.Saved.GlassBackground);
        Assert.False(store.Saved.BouncySpring);
    }

    [Fact]
    public async Task FlushSavesPendingChange()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store, new NullLog(), "Start with test")
        {
            MinimalNotch = true
        };

        await vm.FlushAsync();

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.MinimalNotch);
    }

    [Fact]
    public void NewerSchemaDisablesSave()
    {
        var store = new FakeStore { CanSave = false, Warning = "newer" };
        var vm = new SettingsViewModel(store, new NullLog(), "Start with Windows");

        Assert.False(vm.CanSave);
        Assert.Equal("newer", vm.Warning);
    }

    private sealed class FakeStore : ISettingsStore
    {
        public AppSettings Current { get; private set; } = SettingsSchema.Default;
        public bool CanSave { get; set; } = true;
        public string? Warning { get; set; }
        string? ISettingsStore.LoadWarning => Warning;
        public event EventHandler<AppSettings>? Changed;

        public AppSettings? Saved { get; private set; }

        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Saved = settings;
            Current = settings;
            Changed?.Invoke(this, settings);
            return Task.CompletedTask;
        }
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
