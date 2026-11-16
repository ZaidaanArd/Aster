#!/usr/bin/env bash
set -euo pipefail

mkdir -p docs/results/screenshots/phase2
pid="${ASTER_PID:-$(pgrep -n -f 'Aster.Avalonia')}"
[ -n "$pid" ] || { echo "Aster process not found"; exit 1; }

name=""
for _ in $(seq 1 20); do
    name=$(busctl --user list | awk -v p="$pid" '$1 ~ ("org\\.kde\\.StatusNotifierItem-" p "-") {print $1}')
    name=${name%%$'\n'*}
    [ -n "$name" ] && break
    sleep 0.25
done
[ -n "$name" ] || { echo "Aster SNI item for pid $pid not found"; exit 1; }

sni=""
for _ in $(seq 1 20); do
    sni=$(busctl --user introspect "$name" /StatusNotifierItem 2>/dev/null || true)
    echo "$sni" | grep -q 'org.kde.StatusNotifierItem' && break
    sleep 0.25
done
echo "$sni" | grep -q 'org.kde.StatusNotifierItem' || {
    echo "StatusNotifierItem interface missing"; exit 1;
}
echo "$sni" | grep -q '"Aster Lab Avalonia"' || {
    echo "SNI Id/Title missing"; exit 1;
}
menu=$(echo "$sni" | awk '$1==".Menu" {print $4}' | tr -d '"')
[ -n "$menu" ] || { echo "SNI Menu path missing"; exit 1; }
menu_info=$(busctl --user introspect "$name" "$menu")
echo "$menu_info" | grep -q 'com.canonical.dbusmenu' || {
    echo "DBusMenu interface missing at $menu"; exit 1;
}
spectacle -b -n -o docs/results/screenshots/phase2/linux-tray.png
echo "Linux tray probe PASS ($name)"
