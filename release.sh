#!/bin/bash
# release.sh - Cut a release from the Mac.
#
# Bumps the version in OniAccess.csproj and mod_info.yaml, turns the
# "Unreleased changes" section of changes.md into the new version's section,
# builds and deploys the mod locally (build.sh), commits "Release <version>",
# tags v<version>, pushes, publishes the GitHub release with the manual-install
# release.zip and the changelog entries as notes, and fills release/ with the
# same files for the Steam Workshop upload.
#
# The Workshop upload itself is manual: in the Windows VM, point the Oxygen Not
# Included Mod Uploader at C:\Users\rashadnaqeeb\Documents\VisionNotIncluded\release
# (the same folder, shared with the Mac).
set -euo pipefail

usage() {
	echo "Usage: ./release.sh <version> [--dry-run]"
	echo "  <version>   The new version, e.g. 1.7.3"
	echo "  --dry-run   Bump, build, and package, print the release notes, then undo"
	echo "              the file edits. Nothing is committed, pushed, or published."
}

VERSION=""
DRY_RUN=0
for arg in "$@"; do
	case "$arg" in
		--dry-run) DRY_RUN=1 ;;
		-h|--help) usage; exit 0 ;;
		-*) echo "Unknown option: $arg" >&2; usage >&2; exit 1 ;;
		*)
			if [ -n "$VERSION" ]; then echo "Only one version may be given." >&2; exit 1; fi
			VERSION="$arg" ;;
	esac
done
if [ -z "$VERSION" ]; then usage >&2; exit 1; fi
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "ERROR: version must look like 1.7.3, got '$VERSION'" >&2
	exit 1
fi

REPO="$(cd "$(dirname "$0")" && pwd)"
cd "$REPO"

CSPROJ="OniAccess/OniAccess.csproj"
MOD_INFO="OniAccess/mod_info.yaml"
CHANGELOG="changes.md"
VERSIONED_FILES=("$CSPROJ" "$MOD_INFO" "$CHANGELOG")
RELEASE_DIR="$REPO/release"
TAG="v$VERSION"
WORKSHOP_URL="https://steamcommunity.com/sharedfiles/filedetails/?id=3683507975"

# Until the release commit exists, any failure puts the edited files back so
# the script can simply be rerun after the fix.
COMMITTED=0
TMP="$(mktemp -d)"
on_exit() {
	local status=$?
	if [ "$status" -ne 0 ] && [ "$COMMITTED" -eq 0 ]; then
		git checkout -q -- "${VERSIONED_FILES[@]}" 2>/dev/null || true
		echo "Failed. Version and changelog edits were reverted." >&2
	fi
	rm -rf "$TMP"
}
trap on_exit EXIT

# --- Preflight ---
echo "Checking the repository..."
if [ "$(git rev-parse --abbrev-ref HEAD)" != "main" ]; then
	echo "ERROR: releases are cut from main." >&2
	exit 1
fi
if [ -n "$(git status --porcelain)" ]; then
	echo "ERROR: the working tree is not clean. Commit or stash first:" >&2
	git status --short >&2
	exit 1
fi
git fetch -q origin main
if [ "$(git rev-parse HEAD)" != "$(git rev-parse origin/main)" ]; then
	echo "ERROR: main and origin/main differ. Pull or push first." >&2
	exit 1
fi
if git rev-parse -q --verify "refs/tags/$TAG" >/dev/null || [ -n "$(git ls-remote --tags origin "refs/tags/$TAG")" ]; then
	echo "ERROR: tag $TAG already exists." >&2
	exit 1
fi
if [ "$DRY_RUN" -eq 0 ]; then
	if ! gh auth status >/dev/null 2>&1; then
		echo "ERROR: gh is not logged in. Run: gh auth login" >&2
		exit 1
	fi
	if gh release view "$TAG" >/dev/null 2>&1; then
		echo "ERROR: GitHub release $TAG already exists." >&2
		exit 1
	fi
fi

CURRENT="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ")"
if [ -z "$CURRENT" ]; then
	echo "ERROR: no <Version> in $CSPROJ" >&2
	exit 1
fi
if [ "$CURRENT" = "$VERSION" ]; then
	echo "ERROR: $CSPROJ is already at $VERSION" >&2
	exit 1
fi
if [ "$(sed -n 3p "$CHANGELOG")" != "## Unreleased changes since $CURRENT" ]; then
	echo "ERROR: line 3 of $CHANGELOG must be '## Unreleased changes since $CURRENT', found:" >&2
	sed -n 3p "$CHANGELOG" >&2
	exit 1
fi

# The unreleased entries: non-blank lines between the heading on line 3 and
# the next "## " heading. They become the new section and the release notes.
NOTES="$(awk 'NR > 3 && /^## / { exit } NR > 3 && NF' "$CHANGELOG")"
if [ -z "$NOTES" ]; then
	echo "ERROR: $CHANGELOG has no unreleased entries; nothing to release." >&2
	exit 1
fi

echo "Checking translations..."
python3 sync-translations.py --check

# --- Bump the version and roll the changelog ---
echo "Bumping $CURRENT to $VERSION..."
sed -i '' "s|<Version>$CURRENT</Version>|<Version>$VERSION</Version>|" "$CSPROJ"
sed -i '' "s/^version: \".*\"/version: \"$VERSION\"/" "$MOD_INFO"
awk -v v="$VERSION" 'NR == 3 { print "## Unreleased changes since " v; print ""; print "## " v; next } { print }' \
	"$CHANGELOG" > "$TMP/changes.md"
cp "$TMP/changes.md" "$CHANGELOG"
if ! grep -q "<Version>$VERSION</Version>" "$CSPROJ" || ! grep -q "^version: \"$VERSION\"" "$MOD_INFO"; then
	echo "ERROR: version bump did not take." >&2
	exit 1
fi

# --- Build and deploy the local copy (Release: no dev server) ---
./build.sh --release

# --- Fill release/ for the Workshop uploader ---
echo "Packaging release/..."
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"
cp "OniAccess/bin/Release/net48/OniAccess.dll" "$MOD_INFO" "OniAccess/mod.yaml" "$RELEASE_DIR/"
# The feature module the host byte-loads; its build output name is timestamped,
# the deployed file name is fixed (see OniAccess.Module/OniAccess.Module.csproj).
MODULE_DLL="$(ls -t OniAccess.Module/bin/Release/net48/OniAccess.Module_*.dll | head -1)"
mkdir -p "$RELEASE_DIR/Module"
cp "$MODULE_DLL" "$RELEASE_DIR/Module/OniAccess.Module.dll"
for entry in win-x64/prism.dll linux-x64/libprism.so osx/libprism.dylib; do
	src="prism/native/$entry"
	if [ ! -f "$src" ]; then
		echo "ERROR: native library missing: $src" >&2
		exit 1
	fi
	mkdir -p "$RELEASE_DIR/native/$(dirname "$entry")"
	cp "$src" "$RELEASE_DIR/native/$entry"
done
shopt -s nullglob
PO_FILES=(translations/*.po)
OGG_FILES=(audio/*.ogg)
shopt -u nullglob
if [ "${#PO_FILES[@]}" -eq 0 ] || [ "${#OGG_FILES[@]}" -eq 0 ]; then
	echo "ERROR: expected translations/*.po and audio/*.ogg to exist." >&2
	exit 1
fi
mkdir -p "$RELEASE_DIR/translations" "$RELEASE_DIR/audio"
cp "${PO_FILES[@]}" "$RELEASE_DIR/translations/"
cp "${OGG_FILES[@]}" "$RELEASE_DIR/audio/"
find "$RELEASE_DIR" -name .DS_Store -delete

# The manual-install zip holds the same files at its top level, so extracting
# it straight into mods/local/OniAccess works.
ZIP="$TMP/release.zip"
(cd "$RELEASE_DIR" && zip -q -r -X "$ZIP" .)

# --- Release notes ---
{
	echo "Install via [Steam Workshop]($WORKSHOP_URL)."
	echo
	echo "New to OniAccess? See the [getting started guide]($WORKSHOP_URL#:~:text=Getting%20started)."
	echo
	echo "$NOTES"
	echo
	echo "Manual zip attached for regions where Steam Workshop is unavailable."
} > "$TMP/notes.md"

if [ "$DRY_RUN" -eq 1 ]; then
	echo
	echo "Dry run. Release notes for $TAG would be:"
	echo
	cat "$TMP/notes.md"
	echo
	echo "Zip contents:"
	unzip -Z1 "$ZIP" | grep -v '/$' | sed 's/^/  /'
	git checkout -q -- "${VERSIONED_FILES[@]}"
	echo
	echo "Reverted the version and changelog edits. release/ was left in place with the $VERSION build."
	exit 0
fi

# --- Commit, tag, push, publish ---
echo "Committing and tagging $TAG..."
git add "${VERSIONED_FILES[@]}"
git commit -q -m "Release $VERSION"
git tag "$TAG"
COMMITTED=1
git push -q origin main "$TAG"

echo "Publishing the GitHub release..."
gh release create "$TAG" "$ZIP" --verify-tag --title "$VERSION" --notes-file "$TMP/notes.md"

echo
echo "Released $VERSION."
echo "Next: in the Windows VM, run the Oxygen Not Included Mod Uploader on"
echo "  C:\\Users\\rashadnaqeeb\\Documents\\VisionNotIncluded\\release"
echo "to replace the Workshop build."
