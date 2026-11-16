#!/usr/bin/env bash
set -euo pipefail

service=$(busctl --user list | awk '/org\.mpris\.MediaPlayer2\./ {print $1}')
service=${service%%$'\n'*}
[ -n "$service" ] || { echo "No MPRIS player"; exit 2; }

metadata=$(gdbus call --session --dest "$service" \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player Metadata)

echo "$metadata" | grep -q 'xesam:title' || { echo "No title in metadata"; exit 1; }
echo "$metadata" | grep -q 'xesam:artist' || true

if [ "${ASTER_MPRIS_READ_ONLY:-0}" = "1" ]; then
    echo "MPRIS read probe PASS ($service; PlayPause skipped)"
    exit 0
fi

status_before=$(gdbus call --session --dest "$service" \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player PlaybackStatus)
gdbus call --session --dest "$service" \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.mpris.MediaPlayer2.Player.PlayPause >/dev/null
sleep 1
status_after=$(gdbus call --session --dest "$service" \
  --object-path /org/mpris/MediaPlayer2 \
  --method org.freedesktop.DBus.Properties.Get \
  org.mpris.MediaPlayer2.Player PlaybackStatus)

[ "$status_before" != "$status_after" ] || {
    echo "PlayPause did not change state"; exit 1;
}
echo "MPRIS probe PASS ($service)"
