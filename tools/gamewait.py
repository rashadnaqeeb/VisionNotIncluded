#!/usr/bin/env python3
"""Wait for an OniAccess dev-server state, polling fast and exiting the moment it is ready.

  python3 tools/gamewait.py health     # dev server answering
  python3 tools/gamewait.py menu       # main menu handler active
  python3 tools/gamewait.py ingame     # a colony is loaded and interactive
  python3 tools/gamewait.py loadsave   # POST /loadsave (newest save), then wait for ingame

Exit 0 when reached, 1 on timeout (--timeout seconds, default 240), 2 when the game process is gone.
"""

import argparse
import os
import subprocess
import sys
import time
import urllib.request

BASE = "http://127.0.0.1:" + os.environ.get("ONIACCESS_DEV_PORT", "8772")


def http(path, body=None, timeout=5):
    req = urllib.request.Request(BASE + path, data=body.encode() if body is not None else None,
                                 method="POST" if body is not None else "GET")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def screen():
    try:
        return http("/eval", "OniAccess.Dev.DevApi.Screen()")
    except OSError:
        return ""


def game_alive():
    if sys.platform == "win32":
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq OxygenNotIncluded.exe", "/NH"],
                             capture_output=True, text=True).stdout
        return "OxygenNotIncluded.exe" in out
    return subprocess.run(["pgrep", "-f", "Contents/MacOS/Oxygen Not Included"],
                          capture_output=True).returncode == 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("state", choices=["health", "menu", "ingame", "loadsave"])
    ap.add_argument("--timeout", type=float, default=240)
    args = ap.parse_args()

    if args.state == "loadsave":
        print(http("/loadsave", "", timeout=200).strip())
        args.state = "ingame"

    def health():
        try:
            return "ok" in http("/health", timeout=2)
        except OSError:
            return False

    check = {
        "health": health,
        "menu": lambda: "MainMenuHandler" in screen(),
        "ingame": lambda: screen().startswith("ingame"),
    }[args.state]

    start = time.time()
    grace = start + 20  # a just-started launcher needs a moment to spawn the process
    while time.time() - start < args.timeout:
        if check():
            print(f"{args.state} ready after {time.time() - start:.1f}s")
            return 0
        if time.time() > grace and not game_alive():
            print(f"game process not running; aborting wait for {args.state}", file=sys.stderr)
            return 2
        time.sleep(0.3)
    print(f"timeout waiting for {args.state}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
