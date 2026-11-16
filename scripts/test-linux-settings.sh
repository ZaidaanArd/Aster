#!/usr/bin/env bash
set -euo pipefail

exe="${1:-src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
ready="$(mktemp)"
rm -f "$ready"
ASTER_READY_FILE="$ready" "$exe" --settings >/tmp/aster-settings.log 2>&1 &
pid=$!
cleanup() { kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true; rm -f "$ready"; }
trap cleanup EXIT

for _ in $(seq 1 150); do
    [ -f "$ready" ] && break
    kill -0 "$pid" 2>/dev/null || { cat /tmp/aster-settings.log; exit 1; }
    sleep 0.1
done
[ -f "$ready" ] || { cat /tmp/aster-settings.log; exit 1; }

for _ in $(seq 1 50); do
    wid="$(xdotool search --name '^Aster Lab Avalonia - Settings$' 2>/dev/null | tail -1)"
    [ -n "$wid" ] && break
    sleep 0.1
done
[ -n "${wid:-}" ] || { echo "settings window not found"; exit 1; }
mkdir -p docs/results/screenshots/phase2
spectacle -b -n -o docs/results/screenshots/phase2/linux-settings.png
echo "Linux settings probe PASS"
