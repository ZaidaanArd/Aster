using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.Core.Settings;

namespace Aster.Core.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aster-settings-" + Guid.NewGuid().ToString("N"));
    private readonly IAppLog _log = new NullLog();

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_directory);

    private string PathToSettings => Path.Combine(_directory, "settings.json");

    [Fact]
    public async Task MissingFileLoadsDefaults()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.Equal(SettingsSchema.Default, store.Current);
        Assert.True(store.CanSave);
    }

    [Fact]
    public async Task SaveThenLoadRoundTrips()
    {
        var store = new JsonSettingsStore(PathToSettings, _log);
        await store.LoadAsync();
        var changed = new AppSettings(1, true, true, false, false, false);

        await store.SaveAsync(changed);

        var reloaded = new JsonSettingsStore(PathToSettings, _log);
        await reloaded.LoadAsync();
        Assert.Equal(changed, reloaded.Current);
    }

    [Fact]
    public async Task MissingFieldsUseDefaults()
    {
        var json = """{"schemaVersion":1,"minimalNotch":true}""";
        await File.WriteAllTextAsync(PathToSettings, json);
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.True(store.Current.MinimalNotch);
        Assert.True(store.Current.HideInFullscreen);
        Assert.True(store.Current.GlassBackground);
        Assert.True(store.Current.BouncySpring);
        Assert.False(store.Current.StartWithWindows);
    }

    [Fact]
    public async Task CorruptFileFallsBackToDefaultsAndIsBackedUp()
    {
        await File.WriteAllTextAsync(PathToSettings, "{ not json");
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.Equal(SettingsSchema.Default, store.Current);
        Assert.True(store.CanSave);
        Assert.Contains(Directory.GetFiles(_directory), path => path.Contains(".corrupt-"));
    }

    [Fact]
    public async Task NewerSchemaDisablesSaveAndPreservesFile()
    {
        var newer = """{"schemaVersion":2,"startWithWindows":true}""";
        await File.WriteAllTextAsync(PathToSettings, newer);
        var store = new JsonSettingsStore(PathToSettings, _log);

        await store.LoadAsync();

        Assert.False(store.CanSave);
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(newer, await File.ReadAllTextAsync(PathToSettings));
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
