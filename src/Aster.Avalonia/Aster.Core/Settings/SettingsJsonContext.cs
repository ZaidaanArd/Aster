using System.Text.Json.Serialization;
using Aster.Core.Models;

namespace Aster.Core.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
public partial class SettingsJsonContext : JsonSerializerContext
{
}
