using Microsoft.Win32;

namespace DynamicIsland.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Aster";
    private const string LegacyValueName = "DynamicIsland";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return HasValue(key, ValueName) || HasValue(key, LegacyValueName);
        }
        catch
        {
            return false;
        }
    }

    public static void Migrate()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key?.GetValue(LegacyValueName) is null)
            {
                return;
            }

            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                key.SetValue(ValueName, $"\"{exe}\"");
            }

            key.DeleteValue(LegacyValueName, false);
        }
        catch
        {
            // Registry access can be blocked by policy; ignore.
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    key.SetValue(ValueName, $"\"{exe}\"");
                }

                if (key.GetValue(LegacyValueName) is not null)
                {
                    key.DeleteValue(LegacyValueName, false);
                }
            }
            else
            {
                if (key.GetValue(ValueName) is not null)
                {
                    key.DeleteValue(ValueName, false);
                }

                if (key.GetValue(LegacyValueName) is not null)
                {
                    key.DeleteValue(LegacyValueName, false);
                }
            }
        }
        catch
        {
            // Registry access can be blocked by policy; ignore.
        }
    }

    private static bool HasValue(RegistryKey? key, string name) =>
        key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value);
}
