#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

mkdir -p artifacts
out="artifacts/linux-x64"
rm -rf "$out"
dotnet publish src/Aster.Avalonia/Aster.Desktop.Linux/Aster.Desktop.Linux.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:StripSymbols=true -p:InvariantGlobalization=false -p:DebugType=None \
  -p:CopyOutputSymbolsToPublishDirectory=false -o "$out" 2>&1 | tee artifacts/linux-aot.log

if grep -Eq 'IL2[0-9]{3}|IL3[0-9]{3}' artifacts/linux-aot.log; then
    echo "Trim warning in AOT publish"; exit 1
fi

exe="$out/Aster.Avalonia"
[ -x "$exe" ] || { echo "AOT executable missing"; exit 1; }

ready="$(mktemp)"
rm -f "$ready"
ASTER_READY_FILE="$ready" "$exe" >/tmp/aster-aot-linux.log 2>&1 &
pid=$!
for _ in $(seq 1 150); do
    [ -f "$ready" ] && break
    kill -0 "$pid" 2>/dev/null || { echo "AOT exited"; cat /tmp/aster-aot-linux.log; exit 1; }
    sleep 0.1
done
[ -f "$ready" ] || { echo "AOT ready file not written"; cat /tmp/aster-aot-linux.log; exit 1; }
echo "AOT ready in $(cat "$ready") ms"
kill "$pid" 2>/dev/null || true
wait "$pid" 2>/dev/null || true
rm -f "$ready"

du -h "$exe"
echo "Linux AOT probe PASS"
