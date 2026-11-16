#!/usr/bin/env bash
set -euo pipefail

exe="${1:?usage: measure-linux-phase2.sh <exe>}"
label="${2:-linux}"
runs="${3:-5}"

for i in $(seq 1 "$runs"); do
    ready="$(mktemp)"
    rm -f "$ready"
    ASTER_READY_FILE="$ready" "$exe" >/dev/null 2>&1 &
    pid=$!
    startup=-1
    for _ in $(seq 1 150); do
        [ -f "$ready" ] && { startup=$(cat "$ready"); break; }
        kill -0 "$pid" 2>/dev/null || break
        sleep 0.1
    done
    clk=$(getconf CLK_TCK)
    cpu_start=$(awk '{print $14+$15}' "/proc/$pid/stat" 2>/dev/null || echo 0)
    time_start=$(date +%s)
    sleep 10
    if kill -0 "$pid" 2>/dev/null; then
        rss=$(awk '/VmRSS/ {printf "%.1f", $2/1024}' "/proc/$pid/status")
        cpu_end=$(awk '{print $14+$15}' "/proc/$pid/stat")
        time_end=$(date +%s)
        cpu=$(awk -v d="$((cpu_end - cpu_start))" -v h="$clk" -v t="$((time_end - time_start))" \
            'BEGIN { printf "%.2f", (t > 0 ? (d / h) / t * 100 : 0) }')
        echo "$label,$i,$startup,$rss,$cpu"
    else
        echo "$label,$i,$startup,DEAD,DEAD"
    fi
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    rm -f "$ready"
done
