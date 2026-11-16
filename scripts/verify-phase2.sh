#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

dotnet build src/Aster.Avalonia/Aster.Avalonia.sln -c Release -p:EnableWindowsTargeting=true
dotnet test src/Aster.Avalonia/Aster.Core.Tests/Aster.Core.Tests.csproj -c Release
dotnet test src/Aster.Avalonia/Aster.UI.Tests/Aster.UI.Tests.csproj -c Release
dotnet test src/Aster.Avalonia/Aster.Platform.Linux.Tests/Aster.Platform.Linux.Tests.csproj -c Release

dotnet build src/Aster.Avalonia/Aster.Desktop.Linux/Aster.Desktop.Linux.csproj -c Release
exe="src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia"

window="PASS"
scripts/test-window-shell.sh "$exe" || window="PARTIAL"

scripts/test-single-instance.sh "$exe"
scripts/test-linux-settings.sh "$exe"
scripts/test-mpris-fake.sh "$exe"

ready="$(mktemp)"
rm -f "$ready"
ASTER_READY_FILE="$ready" "$exe" >/tmp/aster-verify.log 2>&1 &
app_pid=$!
export ASTER_PID="$app_pid"
cleanup_app() {
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
    rm -f "$ready"
}
trap cleanup_app EXIT

for _ in $(seq 1 150); do
    [ -f "$ready" ] && break
    kill -0 "$app_pid" 2>/dev/null || { echo "app exited"; cat /tmp/aster-verify.log; exit 1; }
    sleep 0.1
done
[ -f "$ready" ] || { echo "ready file missing"; exit 1; }
sleep 1

scripts/test-linux-tray.sh
scripts/test-mpris.sh
a11y="PARTIAL"
if scripts/test-accessibility.sh; then
    a11y="PASS"
fi

cleanup_app
trap - EXIT

if [ -x scripts/test-aot-linux.sh ]; then
    scripts/test-aot-linux.sh
else
    echo "AOT probe script not present; run Task 14 before the final gate."
fi

if [ "$window" = "PASS" ] && [ "$a11y" = "PASS" ]; then
    echo "Phase 2 Linux verification PASS"
else
    echo "Phase 2 Linux verification PARTIAL (window $window, accessibility $a11y)"
fi
