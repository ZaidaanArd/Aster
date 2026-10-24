using System.IO;
using System.Text.Json;

namespace DynamicIsland.Services;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool MinimalNotch { get; set; }
    public bool ShowMascot { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool VolumeToast { get; set; } = true;
    public bool BrightnessToast { get; set; } = true;
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath { get; }
    public AppSettings Current { get; private set; }
    public event Action? Changed;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamicIsland");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt settings should never stop the island from starting.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch
        {
            // Ignore disk errors.
        }

        Changed?.Invoke();
    }
}
