#!/bin/bash
# Build Kerbal Skins against the local KSP install.
#
#   ./build.sh            build into GameData/KerbalSkins here, then install it into KSP
#   ./build.sh check      compile only, install nothing
#   ./build.sh dist       build into GameData/ here without installing anything
#
# It is built against Keystone.dll (https://github.com/IshiakiZ/ksp-keystone), looked for in this order:
# KEYSTONE=/path/to/Keystone.dll, a built copy of that repository beside this one, the copy installed in the game.
# Needs the .NET SDK (`brew install dotnet`). Override the game location with KSP_DIR=/path/to/KSP.
set -euo pipefail

cd "$(dirname "$0")"
MODE="${1:-install}"
. tools/build/common.sh
OUT=GameData/KerbalSkins

KEYSTONE="${KEYSTONE:-}"
for candidate in ../ksp-keystone/GameData/Keystone/Keystone.dll "$KSP_DIR/GameData/Keystone/Keystone.dll"; do
  [ -z "$KEYSTONE" ] && [ -f "$candidate" ] && KEYSTONE="$candidate"
done
[ -n "$KEYSTONE" ] && [ -f "$KEYSTONE" ] || { echo "error: Keystone.dll not found: install Keystone into the game, build ../ksp-keystone, or set KEYSTONE=/path/to/Keystone.dll" >&2; exit 1; }

case "$MODE" in
  check)
    WITH="$KEYSTONE" compile "$TMP/KerbalSkins.dll" "" src/KerbalSkins
    echo "ok: compiles"
    ;;
  dist|install)
    mkdir -p "$OUT"
    WITH="$KEYSTONE" compile "$OUT/KerbalSkins.dll" "" src/KerbalSkins
    # (the mod's own shaders, if it has any: made by tools/shaderpack/make_bundle.py and kept ready made in src/KerbalSkins/Shaders)
    if ls src/KerbalSkins/Shaders/*.bundle >/dev/null 2>&1; then mkdir -p "$OUT/PluginData"; cp src/KerbalSkins/Shaders/*.bundle "$OUT/PluginData/"; fi
    if [ "$MODE" = dist ]; then echo "ok: built into $OUT"; exit 0; fi
    mkdir -p "$KSP_DIR/GameData/KerbalSkins"
    cp "$OUT/KerbalSkins.dll" "$KSP_DIR/GameData/KerbalSkins/"
    if [ -d "$OUT/PluginData" ]; then mkdir -p "$KSP_DIR/GameData/KerbalSkins/PluginData"; cp "$OUT/PluginData"/*.bundle "$KSP_DIR/GameData/KerbalSkins/PluginData/"; fi
    echo "ok: Kerbal Skins installed to $KSP_DIR/GameData/KerbalSkins (restart KSP to load it; it needs Keystone there too)"
    ;;
  *)
    echo "usage: ./build.sh [install|check|dist]" >&2; exit 2
    ;;
esac
