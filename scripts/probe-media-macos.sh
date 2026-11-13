#!/usr/bin/env bash
set -euo pipefail

if osascript -e 'application "Music" is running' 2>/dev/null | grep -qi true; then
    osascript -e 'tell application "Music" to return (name of current track) & tab & (artist of current track) & tab & (album of current track)'
elif osascript -e 'application "Spotify" is running' 2>/dev/null | grep -qi true; then
    osascript -e 'tell application "Spotify" to return (name of current track) & tab & (artist of current track) & tab & (album of current track)'
else
    echo "NO_SESSION"
fi
