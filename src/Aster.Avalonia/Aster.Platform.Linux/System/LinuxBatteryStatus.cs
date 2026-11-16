using Aster.Core.Interfaces;
using Aster.Core.Models;
using Tmds.DBus.Protocol;

namespace Aster.Platform.Linux.System;

public sealed class LinuxBatteryStatus : IBatteryStatus, IDisposable
{
    private readonly string _root;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cts = new();
    private BatterySnapshot _current = new(100, false, false);

    public LinuxBatteryStatus(
        string? root, IUiDispatcher dispatcher, IAppLog log)
    {
        _root = root ?? "/sys/class/power_supply";
        _dispatcher = dispatcher;
        _log = log;
        _ = LoopAsync(_cts.Token);
    }

    public BatterySnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<BatterySnapshot>? Changed;

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshAsync(ct);
            try
            {
                await Task.Delay(4000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        BatterySnapshot next;
        try
        {
            var bat = Directory.Exists(_root)
                ? Directory.GetDirectories(_root, "BAT*").FirstOrDefault()
                : null;
            if (bat is not null)
            {
                var percent = ReadInt(Path.Combine(bat, "capacity"), 100);
                var status = ReadText(Path.Combine(bat, "status"));
                next = new BatterySnapshot(
                    Math.Clamp(percent, 0, 100),
                    status is "Charging" or "Full",
                    true);
            }
            else
            {
                next = await ReadUPowerAsync(ct) ?? new BatterySnapshot(100, false, false);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Battery refresh failed.", ex);
            next = _current;
        }

        if (next == Current)
        {
            return;
        }

        Volatile.Write(ref _current, next);
        _dispatcher.Post(() => Changed?.Invoke(this, next));
    }

    private static async Task<BatterySnapshot?> ReadUPowerAsync(CancellationToken ct)
    {
        try
        {
            MessageBuffer message;
            using (var writer = DBusConnection.Session.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(
                    "org.freedesktop.UPower",
                    "/org/freedesktop/UPower/devices/DisplayDevice",
                    "org.freedesktop.DBus.Properties",
                    "GetAll",
                    "s",
                    MessageFlags.None);
                writer.WriteString("org.freedesktop.UPower.Device");
                message = writer.CreateMessage();
            }

            var values = await DBusConnection.Session.CallMethodAsync(
                message,
                static (reply, _) =>
                {
                    var reader = reply.GetBodyReader();
                    return reader.ReadDictionaryOfStringToVariantValue();
                },
                null);

            var percent = values.TryGetValue("Percentage", out var p) ? p.GetDouble() : 100;
            var state = values.TryGetValue("State", out var s) ? s.GetUInt32() : 0;
            return new BatterySnapshot(
                Math.Clamp((int)Math.Round(percent), 0, 100),
                state is 1 or 4,
                true);
        }
        catch
        {
            return null;
        }
    }

    private static int ReadInt(string path, int fallback) =>
        int.TryParse(File.Exists(path) ? File.ReadAllText(path).Trim() : null, out var value)
            ? value
            : fallback;

    private static string ReadText(string path) =>
        File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;

    public void Dispose() => _cts.Cancel();
}
