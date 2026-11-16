#!/usr/bin/env bash
set -euo pipefail

log="${1:-artifacts/soak-activity-linux.log}"
wid="$(xdotool search --name '^Aster Lab Avalonia$' | tail -1)"
[ -n "$wid" ] || { echo "island window not found"; exit 1; }

for i in $(seq 1 100); do
    xdotool mousemove --window "$wid" 30 20
    sleep 0.4
    xdotool mousemove 200 500
    sleep 0.4
done

service="$(busctl --user list | awk '/org\.mpris\.MediaPlayer2\.FakePlayer/ {print $1}')"
service=${service%%$'\n'*}
if [ -z "$service" ]; then
    service="$(busctl --user list | awk '/org\.mpris\.MediaPlayer2\./ {print $1}')"
    service=${service%%$'\n'*}
fi
[ -n "$service" ] || { echo "no MPRIS player for track changes"; exit 1; }
for i in $(seq 1 20); do
    gdbus call --session --dest "$service" \
      --object-path /org/mpris/MediaPlayer2 \
      --method org.mpris.MediaPlayer2.Player.Next >/dev/null
    sleep 1
done

printf 'cycles=100\ntracks=20\n' > "$log"
echo "activity complete: $log"
