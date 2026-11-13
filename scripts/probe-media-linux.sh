#!/usr/bin/env bash
set -euo pipefail

if ! command -v playerctl >/dev/null 2>&1; then
    echo "playerctl is not installed" >&2
    exit 1
fi

format=$'{{title}}\t{{artist}}\t{{album}}'
playerctl metadata --format "$format" 2>/dev/null || echo "NO_SESSION"
