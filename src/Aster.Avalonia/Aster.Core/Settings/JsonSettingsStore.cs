using System.Text.Json;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly IAppLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(string path, IAppLog log)
    {
        _path = path;
        _log = log;
    }

    public AppSettings Current { get; private set; } = SettingsSchema.Default;
    public bool CanSave { get; private set; } = true;
    public string? LoadWarning { get; private set; }
    public event EventHandler<AppSettings>? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path))
            {
                Current = SettingsSchema.Default;
                CanSave = true;
                LoadWarning = null;
                return;
            }

            var json = await File.ReadAllTextAsync(_path, ct);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

            if (loaded is null)
            {
                BackupCorrupt();
                Current = SettingsSchema.Default;
                CanSave = true;
                LoadWarning = "Settings file was empty; defaults loaded.";
                return;
            }

            if (loaded.SchemaVersion > SettingsSchema.CurrentVersion)
            {
                CopyBackup(".newer");
                Current = SettingsSchema.Default;
                CanSave = false;
                LoadWarning = "Settings file is from a newer version; changes will not be saved.";
                _log.Warn(LoadWarning);
                return;
            }

            Current = loaded with
            {
                SchemaVersion = SettingsSchema.CurrentVersion,
                StartWithWindows = ReadBool(root, "startWithWindows", SettingsSchema.Default.StartWithWindows),
                MinimalNotch = ReadBool(root, "minimalNotch", SettingsSchema.Default.MinimalNotch),
                HideInFullscreen = ReadBool(root, "hideInFullscreen", SettingsSchema.Default.HideInFullscreen),
                GlassBackground = ReadBool(root, "glassBackground", SettingsSchema.Default.GlassBackground),
                BouncySpring = ReadBool(root, "bouncySpring", SettingsSchema.Default.BouncySpring)
            };
            CanSave = true;
            LoadWarning = null;
        }
        catch (JsonException)
        {
            BackupCorrupt();
            Current = SettingsSchema.Default;
            CanSave = true;
            LoadWarning = "Settings file was corrupt; defaults loaded.";
            _log.Warn(LoadWarning);
        }
        catch (IOException ex)
        {
            Current = SettingsSchema.Default;
            CanSave = false;
            LoadWarning = "Settings file could not be read; defaults loaded.";
            _log.Error(LoadWarning, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        if (!CanSave)
        {
            _log.Warn("Settings save skipped because the store is read-only.");
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            await File.WriteAllTextAsync(temp, json, ct);

            if (File.Exists(_path))
            {
                File.Replace(temp, _path, _path + ".bak");
            }
            else
            {
                File.Move(temp, _path);
            }

            Current = settings;
        }
        catch (Exception ex)
        {
            _log.Error("Settings save failed.", ex);
            return;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, settings);
    }

    private static bool ReadBool(JsonElement root, string name, bool fallback)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return fallback;
    }

    private void BackupCorrupt(string suffix = "")
    {
        try
        {
            var backup = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + suffix;
            File.Move(_path, backup, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to back up corrupt settings.", ex);
        }
    }

    private void CopyBackup(string suffix)
    {
        try
        {
            var backup = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + suffix;
            File.Copy(_path, backup, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to copy the newer settings file.", ex);
        }
    }
}
