#!/usr/bin/env bash
set -euo pipefail

exe="${1:?usage: measure-macos.sh <binary> <label> [runs] [idle-seconds]}"
label="${2:?label required}"
runs="${3:-5}"
idle="${4:-10}"

for i in $(seq 1 "$runs"); do
    ready_file="$(mktemp)"
    rm -f "$ready_file"
    start=$(python3 -c 'import time; print(int(time.time() * 1000))')
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

    rss_mb=$(ps -o rss= -p "$pid" | awk '{printf "%.1f", $1/1024}')
    cpu=$(ps -o %cpu= -p "$pid" | tr -d ' ')
    echo "$label,$i,$startup,$rss_mb,$cpu"
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    rm -f "$ready_file"
done
