using Aster.Core.Models;
using Tmds.DBus.Protocol;

namespace Aster.Platform.Linux.Media;

public sealed class DBusMprisClient : IMprisClient
{
    private const string PlayerPath = "/org/mpris/MediaPlayer2";
    private const string PlayerInterface = "org.mpris.MediaPlayer2.Player";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    public async Task<IReadOnlyList<string>> ListPlayersAsync(CancellationToken ct = default)
    {
        var services = await DBusConnection.Session.ListServicesAsync();
        return services
            .Where(name => name.StartsWith("org.mpris.MediaPlayer2.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<MprisPlayerState?> GetStateAsync(string busName, CancellationToken ct = default)
    {
        var values = await GetAllAsync(busName, PlayerInterface, ct);
        if (values is null)
        {
            return null;
        }

        var metadata = new Dictionary<string, object?>();
        if (values.TryGetValue("Metadata", out var metadataValue) &&
            metadataValue.Type == VariantValueType.Dictionary)
        {
            foreach (var entry in metadataValue.GetDictionary<string, VariantValue>())
            {
                metadata[entry.Key] = ToObject(entry.Value);
            }
        }

        var parsed = MprisMetadataParser.Parse(metadata);
        var playback = values.TryGetValue("PlaybackStatus", out var status)
            ? status.GetString() switch
            {
                "Playing" => PlaybackState.Playing,
                "Paused" => PlaybackState.Paused,
                "Stopped" => PlaybackState.Stopped,
                _ => PlaybackState.None
            }
            : PlaybackState.None;

        var position = values.TryGetValue("Position", out var pos)
            ? TimeSpan.FromMicroseconds(pos.GetInt64())
            : TimeSpan.Zero;

        return new MprisPlayerState(
            busName,
            FriendlySource(busName),
            parsed,
            playback,
            position,
            GetBool(values, "CanSeek"),
            GetBool(values, "CanGoNext"),
            GetBool(values, "CanGoPrevious"),
            DateTimeOffset.UtcNow);
    }

    public Task PlayPauseAsync(string busName, CancellationToken ct = default) =>
        CallAsync(busName, "PlayPause", string.Empty, ct);

    public Task NextAsync(string busName, CancellationToken ct = default) =>
        CallAsync(busName, "Next", string.Empty, ct);

    public Task PreviousAsync(string busName, CancellationToken ct = default) =>
        CallAsync(busName, "Previous", string.Empty, ct);

    public async Task SetPositionAsync(
        string busName, string trackId, TimeSpan position, CancellationToken ct = default)
    {
        MessageBuffer message;
        using (var writer = DBusConnection.Session.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                busName, PlayerPath, PlayerInterface, "SetPosition", "ox", MessageFlags.None);
            writer.WriteObjectPath(trackId);
            writer.WriteInt64(position.Ticks / 10);
            message = writer.CreateMessage();
        }

        await DBusConnection.Session.CallMethodAsync(message);
    }

    public async Task SeekAsync(
        string busName, TimeSpan offset, CancellationToken ct = default)
    {
        MessageBuffer message;
        using (var writer = DBusConnection.Session.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                busName, PlayerPath, PlayerInterface, "Seek", "x", MessageFlags.None);
            writer.WriteInt64(offset.Ticks / 10);
            message = writer.CreateMessage();
        }

        await DBusConnection.Session.CallMethodAsync(message);
    }

    private static async Task<Dictionary<string, VariantValue>?> GetAllAsync(
        string busName, string iface, CancellationToken ct)
    {
        try
        {
            MessageBuffer message;
            using (var writer = DBusConnection.Session.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(
                    busName, PlayerPath, PropertiesInterface, "GetAll", "s", MessageFlags.None);
                writer.WriteString(iface);
                message = writer.CreateMessage();
            }

            return await DBusConnection.Session.CallMethodAsync(
                message,
                static (reply, _) =>
                {
                    var reader = reply.GetBodyReader();
                    return reader.ReadDictionaryOfStringToVariantValue();
                },
                null);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task CallAsync(
        string busName, string member, string signature, CancellationToken ct)
    {
        MessageBuffer message;
        using (var writer = DBusConnection.Session.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                busName, PlayerPath, PlayerInterface, member, signature, MessageFlags.None);
            message = writer.CreateMessage();
        }

        await DBusConnection.Session.CallMethodAsync(message);
    }

    private static object? ToObject(VariantValue value) => value.Type switch
    {
        VariantValueType.String => value.GetString(),
        VariantValueType.Int64 => value.GetInt64(),
        VariantValueType.Int32 => value.GetInt32(),
        VariantValueType.UInt64 => value.GetUInt64(),
        VariantValueType.UInt32 => value.GetUInt32(),
        VariantValueType.ObjectPath => value.GetObjectPathAsString(),
        VariantValueType.Array when value.ItemType == VariantValueType.String =>
            value.GetArray<string>(),
        _ => null
    };

    private static bool GetBool(
        IReadOnlyDictionary<string, VariantValue> values, string key) =>
        values.TryGetValue(key, out var value) &&
        value.Type == VariantValueType.Bool &&
        value.GetBool();

    private static string FriendlySource(string busName)
    {
        var suffix = busName["org.mpris.MediaPlayer2.".Length..];
        if (suffix.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (suffix.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (suffix.Contains("vlc", StringComparison.OrdinalIgnoreCase)) return "VLC";
        if (suffix.Contains("mpv", StringComparison.OrdinalIgnoreCase)) return "mpv";
        return suffix;
    }
}
