using System.Text.Json;
using Aster.Core.Models;
using Aster.Core.Settings;

namespace Aster.Core.Tests;

public class SettingsSchemaTests
{
    [Fact]
    public void DefaultsMatchL2Contract()
    {
        var settings = SettingsSchema.Default;

        Assert.Equal(1, settings.SchemaVersion);
        Assert.False(settings.StartWithWindows);
        Assert.False(settings.MinimalNotch);
        Assert.True(settings.HideInFullscreen);
        Assert.True(settings.GlassBackground);
        Assert.True(settings.BouncySpring);
    }

    [Fact]
    public void JsonRoundTripPreservesValues()
    {
        var original = new AppSettings(1, true, true, false, false, false);
        var json = JsonSerializer.Serialize(original, SettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

        Assert.Equal(original, restored);
        Assert.Contains("\"schemaVersion\": 1", json);
    }
}
