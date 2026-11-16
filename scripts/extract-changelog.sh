#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: extract-changelog.sh <version>}"
version="${version%%-*}"

root="$(cd "$(dirname "$0")/.." && pwd)"
file="$root/CHANGELOG.md"
[ -f "$file" ] || { echo "CHANGELOG.md not found: $file" >&2; exit 1; }

out="$(
    awk -v ver="$version" '
        index($0, "## [" ver "]") == 1 { found = 1; next }
        found && /^## \[/ { exit }
        found { print }
    ' "$file"
)"

if [ -z "$out" ]; then
    echo "no changelog section for $version" >&2
    exit 1
fi

printf '%s\n' "$out"
