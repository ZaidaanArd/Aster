#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: package-linux.sh <version> <output-dir> [--smoke]}"
output="${2:?usage: package-linux.sh <version> <output-dir> [--smoke]}"
smoke=0
for arg in "${@:3}"; do
    case "$arg" in
        --smoke) smoke=1 ;;
        *) echo "unknown option: $arg" >&2; exit 2 ;;
    esac
done

if [ -n "${DOTNET_ROOT:-}" ] && [ -d "$DOTNET_ROOT" ]; then
    export PATH="$DOTNET_ROOT:$PATH"
fi

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
mkdir -p "$output"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

publish="$stage/publish"
log="$stage/publish.log"
dotnet publish src/Aster.Avalonia/Aster.Desktop.Linux/Aster.Desktop.Linux.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:Version="$version" \
    -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial \
    -p:StripSymbols=true -p:InvariantGlobalization=false \
    -p:DebugType=None -p:CopyOutputSymbolsToPublishDirectory=false \
    -o "$publish" 2>&1 | tee "$log"

if grep -Eq 'IL2[0-9]{3}|IL3[0-9]{3}' "$log"; then
    echo "trim warning in AOT publish" >&2
    exit 1
fi

for file in Aster.Avalonia libSkiaSharp.so libHarfBuzzSharp.so; do
    [ -f "$publish/$file" ] || { echo "missing publish output: $file" >&2; exit 1; }
done
[ -x "$publish/Aster.Avalonia" ] || { echo "Aster.Avalonia is not executable" >&2; exit 1; }

name="Aster-$version-linux-x64"
dir="$stage/$name"
mkdir -p "$dir"

cp "$publish/Aster.Avalonia" "$dir/Aster"
chmod +x "$dir/Aster"
cp "$publish/libSkiaSharp.so" "$publish/libHarfBuzzSharp.so" "$dir/"
cp "$root/LICENSE" "$dir/LICENSE"
sed "s/{{VERSION}}/$version/g" "$root/packaging/README.txt" > "$dir/README.txt"
cp "$root/THIRD-PARTY-NOTICES.txt" "$dir/"

archive="$output/$name.tar.gz"
tar -czf "$archive" -C "$stage" "$name"
[ -s "$archive" ] || { echo "archive missing or empty: $archive" >&2; exit 1; }

expected="Aster libHarfBuzzSharp.so libSkiaSharp.so LICENSE README.txt THIRD-PARTY-NOTICES.txt"
expected_sorted="$(printf '%s\n' $expected | sort | tr '\n' ' ' | sed 's/ $//')"
actual_sorted="$(tar -tzf "$archive" | sed "s|^$name/||" | grep -v '^$' | grep -v '/$' | sort | tr '\n' ' ' | sed 's/ $//')"
if [ "$actual_sorted" != "$expected_sorted" ]; then
    echo "unexpected archive files: $actual_sorted" >&2
    echo "expected: $expected_sorted" >&2
    exit 1
fi

if [ "$smoke" = "1" ]; then
    smoke_dir="$stage/smoke"
    mkdir -p "$smoke_dir"
    tar -xzf "$archive" -C "$smoke_dir"
    ready="$stage/ready"
    rm -f "$ready"
    smoke_exe="$smoke_dir/$name/Aster"
    (
        cd "$smoke_dir/$name"
        export ASTER_READY_FILE="$ready"
        exec setsid dbus-run-session -- xvfb-run -a "$smoke_exe" >"$stage/smoke.log" 2>&1
    ) &
    pid=$!
    found=0
    for _ in $(seq 1 200); do
        [ -f "$ready" ] && { found=1; break; }
        kill -0 "$pid" 2>/dev/null || break
        sleep 0.1
    done
    kill -- -"$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    if pgrep -f "$smoke_exe" >/dev/null 2>&1; then
        pkill -f "$smoke_exe" 2>/dev/null || true
        echo "archive smoke leaked a process" >&2
        exit 1
    fi
    if [ "$found" != "1" ]; then
        echo "archive smoke failed; log follows" >&2
        cat "$stage/smoke.log" >&2 || true
        exit 1
    fi
fi

sha256sum "$archive"
echo "$archive"
