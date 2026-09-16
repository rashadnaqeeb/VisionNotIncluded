#!/bin/bash
# run-game.sh - Build + deploy OniAccess, launch the game with the dev server on,
# and BLOCK until it exits. Run it as a background task: the blocking wait means
# the task completes the instant the game quits or crashes, and meanwhile the
# live game is driven over http://127.0.0.1:8772 from another shell (see the
# dev server section of CLAUDE.md).
#
# The dev server is DEBUG-only and gated on a marker file in the mod's data
# folder (dropped here, removed on exit) or ONIACCESS_DEV=1. The game is
# started through Steam (a direct launch of the binary never gets Steam
# initialized and the game quits during boot), so the env var cannot reach it
# and the marker file is the gate that matters.
#
#   --no-build   launch the already-deployed build without rebuilding
set -euo pipefail

NO_BUILD=0
for arg in "$@"; do
	case "$arg" in
		--no-build) NO_BUILD=1 ;;
		-h|--help)
			echo "Usage: scripts/run-game.sh [--no-build]"
			echo "  Builds + deploys (Debug), launches the game with the dev server on, blocks until exit."
			echo "  Drive it over http://127.0.0.1:8772 while it runs. --no-build skips the rebuild."
			exit 0 ;;
		*) echo "Unknown option: $arg" >&2; exit 1 ;;
	esac
done

REPO="$(cd "$(dirname "$0")/.." && pwd)"
source "$REPO/scripts/oni-env.sh"

PORT="${ONIACCESS_DEV_PORT:-8772}"
STEAM_URL="steam://rungameid/457140"
PROCESS_PATTERN="Contents/MacOS/Oxygen Not Included"
DATA_DIR="$HOME/Library/Application Support/unity.Klei.Oxygen Not Included"
MARKER="$DATA_DIR/mods/OniAccess/devserver.enable"
if ! pgrep -x steam_osx >/dev/null; then
	echo "ERROR: Steam is not running; the game must be launched through Steam." >&2
	exit 1
fi

# --- Stop any running instance first, and wait for it and the port to go away ---
if pgrep -f "$PROCESS_PATTERN" >/dev/null; then
	echo "Stopping the running game..."
	pkill -f "$PROCESS_PATTERN" || true
fi
deadline=$((SECONDS + 15))
while pgrep -f "$PROCESS_PATTERN" >/dev/null || lsof -nP -iTCP:"$PORT" -sTCP:LISTEN >/dev/null 2>&1; do
	if [ "$SECONDS" -ge "$deadline" ]; then
		echo "WARNING: the old game or port $PORT did not free up in time; launching anyway." >&2
		break
	fi
	sleep 0.25
done

# --- Build + deploy before launching, so a restart never tests a stale DLL ---
if [ "$NO_BUILD" -eq 0 ]; then
	"$REPO/build.sh"
fi

# --- Enable the dev server for this launch ---
mkdir -p "$(dirname "$MARKER")"
echo 1 > "$MARKER"

TRACKED=""
cleanup() {
	rm -f "$MARKER"
	if [ -n "$TRACKED" ] && kill -0 "$TRACKED" 2>/dev/null; then
		echo "Launcher stopping; killing the game (PID $TRACKED)..."
		kill "$TRACKED" 2>/dev/null || true
	fi
}
trap cleanup EXIT

open "$STEAM_URL"
echo "Asked Steam to launch the game; waiting for the process..."
deadline=$((SECONDS + 60))
while ! pgrep -f "$PROCESS_PATTERN" >/dev/null; do
	if [ "$SECONDS" -ge "$deadline" ]; then
		echo "ERROR: the game process did not appear within 60s." >&2
		exit 1
	fi
	sleep 0.5
done
TRACKED="$(pgrep -f "$PROCESS_PATTERN" | head -1)"
echo "Game running (PID $TRACKED); dev server -> http://127.0.0.1:$PORT. Waiting for /health..."

# The game takes a while to boot to the mod's entry point.
if curl -s --retry 180 --retry-connrefused --retry-delay 1 --max-time 2 "http://127.0.0.1:$PORT/health" | grep -q ok; then
	echo "READY: dev server is up on port $PORT."
else
	echo "WARNING: the dev server never answered /health (Debug build deployed? marker read? mod enabled?)." >&2
fi

# Block for the game; cancelling this launcher kills it (cleanup above).
while kill -0 "$TRACKED" 2>/dev/null; do sleep 1; done
TRACKED=""
echo "The game exited."
