#!/usr/bin/env bash
set -euo pipefail

exe="${1:?usage: soak-phase2-linux.sh <exe> [minutes] [activity-log]}"
minutes="${2:-60}"
activity_log="${3:-artifacts/soak-activity-linux.log}"
csv="docs/results/phase2-soak-linux.csv"
mkdir -p docs/results
rm -f "$csv"

ASTER_READY_FILE=/tmp/aster-soak.ready "$exe" >/tmp/aster-soak.log 2>&1 &
pid=$!
cleanup() { kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true; }
trap cleanup EXIT

for _ in $(seq 1 150); do
    [ -f /tmp/aster-soak.ready ] && break
    kill -0 "$pid" 2>/dev/null || { echo "process exited before ready"; cat /tmp/aster-soak.log; exit 1; }
    sleep 0.1
done
rm -f /tmp/aster-soak.ready

clk=$(getconf CLK_TCK)
prev_cpu=$(awk '{print $14+$15}' "/proc/$pid/stat")
prev_time=$(date +%s)

for i in $(seq 1 "$minutes"); do
    sleep 60
    kill -0 "$pid" 2>/dev/null || { echo "process exited at minute $i"; exit 1; }
    rss=$(awk '/VmRSS/ {printf "%.1f", $2/1024}' "/proc/$pid/status")
    private=$(awk '/RssAnon/ {printf "%.1f", $2/1024}' "/proc/$pid/status")
    fds=$(ls "/proc/$pid/fd" | wc -l)
    now=$(date +%s)
    cpu_ticks=$(awk '{print $14+$15}' "/proc/$pid/stat")
    cpu=$(awk -v d="$((cpu_ticks - prev_cpu))" -v h="$clk" -v t="$((now - prev_time))" \
        'BEGIN { printf "%.2f", (t > 0 ? (d / h) / t * 100 : 0) }')
    prev_cpu=$cpu_ticks
    prev_time=$now
    if [ "$i" -eq 1 ]; then
        echo "Minute,WorkingSetMB,PrivateMB,Fds,CpuPercent" > "$csv"
    fi
    echo "$i,$rss,$private,$fds,$cpu" >> "$csv"
    echo "minute $i: ${rss} MB, ${fds} fds"
done

busctl --user list | awk -v p="$pid" '$1 ~ ("org\\.kde\\.StatusNotifierItem-" p "-") {found=1} END {exit !found}' || {
    echo "Aster tray icon missing at end of soak"; exit 1;
}

[ -f "$activity_log" ] || { echo "activity log missing: $activity_log"; exit 1; }
grep -Eq 'cycles=([1-9][0-9]{2,}|100)' "$activity_log" || {
    echo "activity log does not evidence 100 cycles"; exit 1;
}
grep -Eq 'tracks=([2-9][0-9]|[1-9][0-9]{2,}|20)' "$activity_log" || {
    echo "activity log does not evidence 20 tracks"; exit 1;
}

if [ "$minutes" -lt 60 ]; then
    echo "Dry run complete; no PASS verdict."
    exit 0
fi

baseline=$(awk -F, 'NR>1 && $1<=10 {sum+=$2; n++} END {if(n) printf "%.1f", sum/n}' "$csv")
last=$(tail -1 "$csv" | cut -d, -f2)
fds10=$(awk -F, 'NR>1 && $1>=10 {if(min==""||$4<min)min=$4; if($4>max)max=$4} END {print max-min}' "$csv")
slope=$(awk -F, 'NR>1 && $1>=30 {x[++n]=$1; y[n]=$2; sx+=x[n]; sy+=y[n]; sxy+=x[n]*y[n]; sx2+=x[n]*x[n]} END {d=n*sx2-sx*sx; if(d==0) print 0; else printf "%.1f", ((n*sxy-sx*sy)/d)*60}' "$csv")

echo "baseline=$baseline minute60=$last fds_range=$fds10 slope30_60=$slope"
awk -v b="$baseline" -v l="$last" 'BEGIN {exit !(l > b*1.10)}' && {
    echo "working set exceeded 110%"; exit 1;
}
[ "$fds10" -le 10 ] || { echo "fd range exceeded 10"; exit 1; }
awk -v s="$slope" 'BEGIN {exit !(s > 5)}' && {
    echo "working set slope exceeded 5 MB/hour"; exit 1;
}
echo "Linux soak PASS"
