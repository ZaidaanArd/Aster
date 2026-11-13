#!/usr/bin/env bash
set -euo pipefail

exe="${1:?usage: measure-linux.sh <binary> <label> [runs] [idle-seconds]}"
label="${2:?label required}"
runs="${3:-5}"
idle="${4:-10}"

for i in $(seq 1 "$runs"); do
    ready_file="$(mktemp)"
    rm -f "$ready_file"
    start=$(date +%s%3N)
    ASTER_READY_FILE="$ready_file" "$exe" >/dev/null 2>&1 &
    pid=$!

    startup=-1
    for _ in $(seq 1 100); do
        if [ -f "$ready_file" ]; then
            startup=$(cat "$ready_file")
            break
        fi
        if ! kill -0 "$pid" 2>/dev/null; then
            break
        fi
        sleep 0.1
    done

    sleep "$idle"

    if ! kill -0 "$pid" 2>/dev/null; then
        echo "$label,$i,$startup,DEAD,DEAD"
        rm -f "$ready_file"
        continue
    fi

    rss_mb=$(awk '/VmRSS/ {printf "%.1f", $2/1024}' "/proc/$pid/status")
    cpu=$(ps -p "$pid" -o %cpu= | tr -d ' ')
    echo "$label,$i,$startup,$rss_mb,$cpu"
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    rm -f "$ready_file"
done
