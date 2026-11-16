#!/usr/bin/env bash
set -euo pipefail

exe="${1:-src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
scale="${EXPECTED_SCALE:-2}"
ready="$(mktemp)"
log="$(mktemp)"
mkdir -p docs/results/screenshots/phase2
rm -f "$ready"

ASTER_READY_FILE="$ready" "$exe" >"$log" 2>&1 &
pid=$!
cleanup() {
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    rm -f "$ready" "$log"
}
trap cleanup EXIT

for _ in $(seq 1 150); do
    [ -f "$ready" ] && break
    kill -0 "$pid" 2>/dev/null || { echo "app exited"; cat "$log"; exit 1; }
    sleep 0.1
done
[ -f "$ready" ] || { echo "ready file not written"; cat "$log"; exit 1; }

wid="$(xdotool search --name '^Aster Lab Avalonia$' | tail -1)"
[ -n "$wid" ] || { echo "island window not found"; exit 1; }

eval "$(xdotool getwindowgeometry --shell "$wid")"
expected_width=$(awk -v s="$scale" 'BEGIN {printf "%d", 300 * s}')
expected_height=$(awk -v s="$scale" 'BEGIN {printf "%d", 38 * s}')
screen_width=$(xdotool getdisplaygeometry | awk '{print $1}')
expected_x=$(((screen_width - expected_width) / 2))
# The dock window type is placed at the top of the screen; a utility window
# would be constrained to the work area (below a top panel).
expected_y=0

[ "$WIDTH" -ge $((expected_width - 2)) ] && [ "$WIDTH" -le $((expected_width + 2)) ] || {
    echo "width mismatch: got $WIDTH expected $expected_width"; exit 1;
}
[ "$HEIGHT" -ge $((expected_height - 2)) ] && [ "$HEIGHT" -le $((expected_height + 2)) ] || {
    echo "height mismatch: got $HEIGHT expected $expected_height"; exit 1;
}
[ "$X" -ge $((expected_x - 2)) ] && [ "$X" -le $((expected_x + 2)) ] || {
    echo "x mismatch: got $X expected $expected_x"; exit 1;
}
[ "$Y" -ge $((expected_y - 2)) ] && [ "$Y" -le $((expected_y + 2)) ] || {
    echo "y mismatch: got $Y expected $expected_y"; exit 1;
}

xprop -id "$wid" _NET_WM_STATE | grep -q '_NET_WM_STATE_ABOVE' || {
    echo "missing _NET_WM_STATE_ABOVE"; exit 1;
}
xprop -id "$wid" _NET_WM_STATE | grep -q '_NET_WM_STATE_SKIP_TASKBAR' || {
    echo "missing _NET_WM_STATE_SKIP_TASKBAR"; exit 1;
}
xprop -id "$wid" WM_HINTS | grep -q 'Client accepts input or input focus: False' || {
    echo "missing WM_HINTS input focus False"; exit 1;
}
xprop -id "$wid" _NET_WM_WINDOW_TYPE | grep -q '_NET_WM_WINDOW_TYPE_DOCK' || {
    echo "missing utility window type"; exit 1;
}
motif="$(xprop -id "$wid" _MOTIF_WM_HINTS)"
if echo "$motif" | grep -Eq 'decorations: (0|0x0)'; then
    :
else
    decorations="$(echo "$motif" | sed 's/^[^=]*= //' | awk -F', ' '{print $3}')"
    case "$decorations" in
        0|0x0) ;;
        *) echo "missing _MOTIF_WM_HINTS decorations=0: $motif"; exit 1 ;;
    esac
fi

xmessage -geometry 300x200+50+300 -buttons OK "underneath" >/dev/null 2>&1 &
under_pid=$!
trap 'kill $under_pid 2>/dev/null || true; cleanup' EXIT
sleep 0.5
under_wid="$(xdotool search --class Xmessage | tail -1)"
[ -n "$under_wid" ] || { echo "underlying xmessage window not found"; exit 1; }

before="$(xdotool getactivewindow 2>/dev/null || true)"
xdotool mousemove --window "$wid" 30 20 click 1
sleep 0.4
after="$(xdotool getactivewindow 2>/dev/null || true)"
[ -n "$before" ] && [ "$before" = "$after" ] || { echo "pill click moved focus: $before -> $after"; exit 1; }

eval "$(xdotool getwindowgeometry --shell "$under_wid")"
under_x=$((X + WIDTH / 2))
under_y=$((Y + HEIGHT / 2))
xdotool mousemove "$under_x" "$under_y" click 1
sleep 0.4
after_under="$(xdotool getactivewindow 2>/dev/null || true)"
focus_check="PASS"
if [ "$after_under" != "$under_wid" ]; then
    # KDE Wayland may not deliver synthetic XTest clicks to XWayland windows.
    # That makes the focus hand-off unobservable; do not claim PASS.
    focus_check="PARTIAL"
    echo "warning: synthetic click focus hand-off not observed: $after_under != $under_wid"
fi
kill "$under_pid" 2>/dev/null || true
wait "$under_pid" 2>/dev/null || true

spectacle -b -n -o "docs/results/screenshots/phase2/linux-window-${scale}x.png"
if [ "$focus_check" = "PASS" ]; then
    echo "Linux window probe PASS at scale=$scale"
else
    echo "Linux window probe PARTIAL at scale=$scale (focus hand-off not observable)"
    exit 3
fi
