#!/bin/bash
# build.sh - Build and deploy OniAccess to the Mac game's local mods directory.
# Builds the host (OniAccess.dll, loaded by the game) and the module
# (Module/OniAccess.Module.dll, byte-loaded by the host and hot-reloadable),
# deploys them, and keeps the mod enabled in mods.json (the game disables it
# after crashes or version changes). windows/build.ps1 is the Windows
# counterpart.
#
# Debug is the default: the dev server is compiled in but stays inert unless
# scripts/run-game.sh drops its marker file. Release builds carry none of it.
set -euo pipefail

NO_BUILD=0
MODULE_ONLY=0
CONFIG=Debug
for arg in "$@"; do
	case "$arg" in
		--no-build) NO_BUILD=1 ;;
		--module) MODULE_ONLY=1 ;;
		--release) CONFIG=Release ;;
		-h|--help)
			echo "Usage: ./build.sh [--module] [--release] [--no-build]"
			echo "  --module    Rebuild only the module and hot-reload it into the running game"
			echo "              (POST /reload); the host and the game are left alone"
			echo "  --release   Release configuration (no dev server); the shipping build"
			echo "  --no-build  Skip building, just deploy the last build and patch mods.json"
			exit 0 ;;
		*) echo "Unknown option: $arg" >&2; exit 1 ;;
	esac
done

REPO="$(cd "$(dirname "$0")" && pwd)"

source "$REPO/scripts/oni-env.sh"

HOST_PROJECT="$REPO/OniAccess"
MODULE_PROJECT="$REPO/OniAccess.Module"
HOST_DLL="$HOST_PROJECT/bin/$CONFIG/net48/OniAccess.dll"
DATA_DIR="$HOME/Library/Application Support/unity.Klei.Oxygen Not Included"
MOD_DIR="$DATA_DIR/mods/local/OniAccess"
MODULE_DIR="$MOD_DIR/Module"
DEV_PORT="${ONIACCESS_DEV_PORT:-8772}"

# --- Sync version from .csproj to mod_info.yaml ---
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$HOST_PROJECT/OniAccess.csproj")"
sed -i '' "s/version: \".*\"/version: \"$VERSION\"/" "$HOST_PROJECT/mod_info.yaml"

# --- Build (the module project references the host, so one build makes both) ---
if [ "$NO_BUILD" -eq 0 ]; then
	echo "Building OniAccess ($CONFIG)..."
	dotnet build "$MODULE_PROJECT/OniAccess.Module.csproj" -c "$CONFIG"
fi

if [ ! -f "$HOST_DLL" ]; then
	echo "ERROR: host DLL not found at $HOST_DLL" >&2
	exit 1
fi
# The module's assembly name is timestamped per build (see its csproj); the
# project prunes older outputs, so the newest file is the one just built.
MODULE_DLL="$(ls -t "$MODULE_PROJECT/bin/$CONFIG/net48"/OniAccess.Module_*.dll 2>/dev/null | head -1 || true)"
if [ -z "$MODULE_DLL" ]; then
	echo "ERROR: module DLL not found under $MODULE_PROJECT/bin/$CONFIG/net48" >&2
	exit 1
fi

# --- Deploy the module (a hot-reload only ever needs this part) ---
# Copy then rename so the host never byte-loads a half-written file.
mkdir -p "$MODULE_DIR"
cp "$MODULE_DLL" "$MODULE_DIR/OniAccess.Module.dll.tmp"
mv -f "$MODULE_DIR/OniAccess.Module.dll.tmp" "$MODULE_DIR/OniAccess.Module.dll"
echo "Deployed module $(basename "$MODULE_DLL")"
# The dev server's REPL compiler must sit at the mod root: the game's loader
# resolves the host's dependencies from there (and calls GetTypes on the host
# before any mod code could redirect the lookup). Release ships without it.
if [ "$CONFIG" = "Debug" ]; then
	cp "$REPO/vendor/Mono.CSharp.dll" "$MOD_DIR/Mono.CSharp.dll"
else
	rm -f "$MOD_DIR/Mono.CSharp.dll"
fi
rm -f "$MODULE_DIR/Mono.CSharp.dll"

if [ "$MODULE_ONLY" -eq 1 ]; then
	if ! cmp -s "$HOST_DLL" "$MOD_DIR/OniAccess.dll"; then
		echo "WARNING: the built host differs from the deployed one. Host changes need a full ./build.sh and a game restart." >&2
	fi
	if curl -s --max-time 2 "http://127.0.0.1:$DEV_PORT/health" | grep -q ok; then
		echo "Reloading the module in the running game..."
		curl -s --max-time 90 -X POST "http://127.0.0.1:$DEV_PORT/reload"
	else
		echo "Dev server not answering on port $DEV_PORT; the new module loads on the next game launch."
	fi
	exit 0
fi

# --- Copy the host DLL, metadata, and the Mac Prism native library ---
mkdir -p "$MOD_DIR/native/osx"
cp "$HOST_DLL" "$MOD_DIR/OniAccess.dll"
cp "$HOST_PROJECT/mod_info.yaml" "$HOST_PROJECT/mod.yaml" "$MOD_DIR/"
cp "$REPO/prism/native/osx/libprism.dylib" "$MOD_DIR/native/osx/libprism.dylib"
echo "Deployed Prism native library to $MOD_DIR/native/osx"

# --- Copy translation and audio files ---
shopt -s nullglob
PO_FILES=("$REPO"/translations/*.po)
if [ "${#PO_FILES[@]}" -gt 0 ]; then
	mkdir -p "$MOD_DIR/translations"
	cp "${PO_FILES[@]}" "$MOD_DIR/translations/"
	echo "Deployed ${#PO_FILES[@]} translation file(s)"
fi
OGG_FILES=("$REPO"/audio/*.ogg)
if [ "${#OGG_FILES[@]}" -gt 0 ]; then
	mkdir -p "$MOD_DIR/audio"
	cp "${OGG_FILES[@]}" "$MOD_DIR/audio/"
	echo "Deployed ${#OGG_FILES[@]} audio file(s)"
fi
shopt -u nullglob

# --- Patch mods.json ---
# Same logic the EnableMod applet uses: enabled for base game and Spaced Out,
# crash count reset, status Installed.
case "$(osascript -l JavaScript "$REPO/EnableMod/mac/EnableMod.js" --quiet)" in
	OK) echo "Patched mods.json - mod is enabled." ;;
	NOT_FOUND)
		echo "Mod entry not found in mods.json - game will discover it on next launch."
		echo "Then run ./build.sh --no-build (or EnableMod.app) to enable it." ;;
	NO_FILE)
		echo "mods.json not found - game will create it on first launch."
		echo "Then run ./build.sh --no-build (or EnableMod.app) to enable it." ;;
	*) echo "ERROR: unexpected result from EnableMod.js" >&2; exit 1 ;;
esac

echo
echo "Done. Launch the game (scripts/run-game.sh launches it with the dev server on)."
