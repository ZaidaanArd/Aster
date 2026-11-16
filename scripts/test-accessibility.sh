#!/usr/bin/env bash
set -euo pipefail

if ! command -v orca >/dev/null 2>&1; then
    echo "orca not installed; accessibility PARTIAL"
    exit 2
fi

if ! busctl --user list | awk '/org\.a11y/ {found=1} END {exit !found}'; then
    echo "AT-SPI bus not available; accessibility PARTIAL"
    exit 2
fi

if grep -q '^Status: PASS' docs/results/phase2-linux-accessibility.md 2>/dev/null; then
    echo "AT-SPI available and manual walkthrough recorded"
    exit 0
fi

echo "AT-SPI bus available; manual Orca walkthrough required"
exit 2
