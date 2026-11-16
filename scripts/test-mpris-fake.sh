#!/usr/bin/env bash
set -euo pipefail

exe="${1:-src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
python3 -c 'import dbus_next' 2>/dev/null || {
    echo "python3-dbus-next is required for the fake player"; exit 2;
}
export ASTER_EXE="$exe"
dbus-run-session -- bash -c '
set -euo pipefail
python3 tools/fake-mpris-player.py >/tmp/fake-mpris.log 2>&1 &
fake=$!
sleep 1
ready=$(mktemp)
ASTER_READY_FILE="$ready" "$ASTER_EXE" >/tmp/aster-fake-media.log 2>&1 &
app=$!
trap "kill $app $fake 2>/dev/null || true" EXIT
for _ in $(seq 1 150); do [ -f "$ready" ] && break; sleep 0.1; done
[ -f "$ready" ] || { cat /tmp/aster-fake-media.log; exit 1; }

gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player Metadata | grep -q "Tone 1"

gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.mpris.MediaPlayer2.Player.PlayPause >/dev/null
sleep 0.3
gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player PlaybackStatus | grep -q Paused

gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.mpris.MediaPlayer2.Player.Next >/dev/null
sleep 0.3
gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player Metadata | grep -q "Tone 2"

gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.mpris.MediaPlayer2.Player.SetPosition \
  /org/mpris/MediaPlayer2/Track/2 30000000 >/dev/null
gdbus call --session --dest org.mpris.MediaPlayer2.FakePlayer \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player Position | grep -q 30000000

# Restart the fake player so the integration test sees its initial Tone 1 state.
kill $fake 2>/dev/null || true
wait $fake 2>/dev/null || true
python3 tools/fake-mpris-player.py >/tmp/fake-mpris-restart.log 2>&1 &
fake=$!
sleep 1

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export ASTER_FAKE_MPRIS=1
dotnet test src/Aster.Avalonia/Aster.Platform.Linux.Tests/Aster.Platform.Linux.Tests.csproj \
  -c Release --filter MprisDbusIntegrationTests

echo "Fake MPRIS integration PASS"
'
