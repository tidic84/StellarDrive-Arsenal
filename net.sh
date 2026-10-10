#!/usr/bin/env bash
# Dev multiplayer: builds, then starts a host and a client instance of the game on this PC.
set -e
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
GAME="/c/Program Files (x86)/Steam/steamapps/common/StellarDrive Demo"
W="/c/Users/tidic/AppData/LocalLow/CuriousOwlGames/StellarDrive/WorldSaves"
cd "$(dirname "$0")"
taskkill //IM StellarDrive.exe //F > /dev/null 2>&1 || true
dotnet build src/Arsenal.csproj -c Release -p:DefineConstants=DEV -nologo -v q 2>&1 | grep -E "rror CS" && exit 1
sleep 2
cp src/bin/Release/Arsenal.dll "$GAME/Mods/"
rm -rf "$W/ARS_Test" && mkdir -p "$W/ARS_Test" && cp "$W/WB_Dump/world.json" "$W/WB_Dump/world.jpg" "$W/ARS_Test/"
(cd "$GAME" && ARSENAL_NET=host ./StellarDrive.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 > /dev/null 2>&1 &)
sleep 25
(cd "$GAME" && ARSENAL_NET=client ./StellarDrive.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 > /dev/null 2>&1 &)
sleep 30
ls -t "$GAME/MelonLoader/Logs" | head -3
