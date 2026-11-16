#!/usr/bin/env bash
set -euo pipefail

exe="${1:-src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
ready="$(mktemp)"
rm -f "$ready"
ASTER_READY_FILE="$ready" "$exe" >/tmp/aster-single-1.log 2>&1 &
first=$!
cleanup() { kill "$first" 2>/dev/null || true; wait "$first" 2>/dev/null || true; rm -f "$ready"; }
trap cleanup EXIT

for _ in $(seq 1 150); do
    [ -f "$ready" ] && break
    kill -0 "$first" 2>/dev/null || { echo "first instance exited"; exit 1; }
    sleep 0.1
done
[ -f "$ready" ] || { echo "first instance not ready"; exit 1; }

second_ready="$(mktemp)"
set +e
ASTER_READY_FILE="$second_ready" "$exe" >/tmp/aster-single-2.log 2>&1
code=$?
set -e
rm -f "$second_ready"
[ "$code" -eq 1 ] || { echo "second instance exit code $code"; exit 1; }
echo "Linux single instance PASS"
