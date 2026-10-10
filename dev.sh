#!/usr/bin/env bash
# Dev loop: build with DEV, deploy to the game's Mods folder, (re)start the game and wait for a log marker.
# Usage: ./dev.sh [marker-regex] [timeout-seconds]
set -e
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
GAME="/c/Program Files (x86)/Steam/steamapps/common/StellarDrive Demo"
LOG="$GAME/MelonLoader/Latest.log"
MARKER="${1:-Materials probe written}"
TIMEOUT="${2:-120}"
cd "$(dirname "$0")"

taskkill //IM StellarDrive.exe //F > /dev/null 2>&1 || true
dotnet build src/Arsenal.csproj -c Release -p:DefineConstants=DEV -nologo -v q 2>&1 | grep -E "error|rror CS" && exit 1
sleep 2
cp src/bin/Release/Arsenal.dll "$GAME/Mods/"
rm -f "$LOG"
(cd "$GAME" && ./StellarDrive.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 > /dev/null 2>&1 &)
for i in $(seq 1 "$TIMEOUT"); do
  if [ -f "$LOG" ] && grep -qE "$MARKER|\[Arsenal\].*(Exception|failed)" "$LOG"; then break; fi
  sleep 1
done
grep -E "\[Arsenal\]|Exception|ERROR" "$LOG" | tail -40
