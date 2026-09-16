# OniAccess - Claude Code Instructions

OniAccess is an accessibility mod for Oxygen Not Included that makes the game playable for blind users. It uses Harmony patches to hook into the game's UI and provides speech output as the sole interface — there is no visual fallback. Every decision should be weighed against the fact that if something fails silently or speaks stale data, the player has no way to know.

## Build

Development is Mac-first. Always use the build script, never `dotnet build` directly.

```
./build.sh            # Debug: host + module, deploy, patch mods.json (needs a game restart)
./build.sh --module   # rebuild the module only and hot-reload it into the running game
./build.sh --release  # the shipping build, no dev server
```

The script deploys to the game's local mods directory and patches mods.json to keep the mod enabled (it also clears `mod_load_in_progress`: a crash while mods load leaves that set, and the next boot then enters mod safe mode and disables every mod). `scripts/oni-env.sh` (sourced by `build.sh` and `test.sh`) finds the game's `Managed` folder and exports `ONI_MANAGED`; set it yourself if the game is somewhere unusual. `EnableMod/mac/package.sh` rebuilds `EnableMod.app.zip` from `EnableMod/mac/EnableMod.js`.

Debug is the default and is what the player build on this machine runs: the dev server is compiled in but stays inert without its marker file. Release strips every `#if DEBUG` file and the Mono.CSharp reference; `release.sh` uses it.

### Host/module split (hot reload)

The mod is two assemblies:

- **HOST** (`OniAccess/OniAccess.csproj`, `OniAccess.dll` at the mod root; loaded and locked by ONI's `DLLLoader`; changing it needs a full `./build.sh` and a game restart, so keep it minimal): `Mod.cs` (the `UserMod2` entry point, native Prism preload, the dev server's per-frame `Ticker`), `Modularity/` (`IModModule` + `ModuleLoader`), `Util/LogHelper.cs`, `Util/LogUnityBackend.cs`, and the dev server core (`Dev/DevServer.cs`, `DevHttpServer.cs`, `CSharpEvaluator.cs`, `SpeechLog.cs`). The host carries no Harmony patches. Its dependency `Mono.CSharp.dll` (vendored in `vendor/`) must sit at the mod root: the loader calls `GetTypes` on the host before any mod code runs, so nothing we install can redirect that lookup.
- **MODULE** (`OniAccess.Module/OniAccess.Module.csproj`, compiled from everything else under `OniAccess/`; deployed as `Module/OniAccess.Module.dll`, a subfolder the loader never scans; **byte-loaded, never file-locked**): `ModuleMain.cs` is its `IModModule` (`Load` = the boot sequence, `Dispose` = teardown). **Day-to-day feature work goes here and hot-reloads with no restart**: `./build.sh --module` deploys it and POSTs `/reload` (`GET /module` shows the live generation). The module references the host dll directly (`Mod.DataDir`, `Log`, `DevServer.Instance`); the host knows the module only through `IModModule`.

Rules that keep the reload honest:
- The module's `AssemblyName` is TIMESTAMPED per build (`OniAccess.Module_yyyyMMddHHmmss`). Mono binds non-strong-named assemblies by simple name, so an unchanged name would silently reload the OLD image; do not "fix" this. The deployed file name stays stable. The test project pins the name with `OniModuleName=OniAccess.Module` so it can reference the output.
- `ModuleMain.Dispose` must undo every persistent hook `Load` created: Harmony patches (per-generation id `OniAccess.gen<N>`, unpatched by id), handler game-event subscriptions (`HandlerStack.DeactivateAll`), `ModInputRouter` in the game's input tree, the mod's GameObjects, the speech backend, the `Localization` registration, dev routes. A missed one shows up as doubled speech after a reload. Anything new that hooks into the game or the host needs its undo step there.
- One-shot game events (`InputInit.Awake`, `Localization.Initialize`) do not fire again after a reload; `ModuleMain.Reattach` redoes their work and rebuilds the handler stack with `ContextDetector.DetectAndActivate`. A new one-shot patch needs the same treatment.
- Old generations leak (net48 has no collectible load contexts); fine for a dev loop, and players never reload.
- `Log.Error` still ends the session, so a failed module load or a failed dispose step shows the crash dialog. That is the intended signal; restart with `scripts/run-game.sh`.

## Dev server

A Debug build carries a loopback HTTP server (`OniAccess/Dev/`, all `#if DEBUG`; a Release build has none of it) that lets Claude introspect and drive the live game without hearing the screen reader. It is inert unless `mods/OniAccess/devserver.enable` exists in the game's data folder or `ONIACCESS_DEV=1` is set. `scripts/run-game.sh` drops the marker, builds and deploys, launches the game through Steam (a direct launch of the binary never gets Steam initialized and the game quits during boot, so the env var cannot reach it; the marker is the gate that matters), waits for `/health`, and blocks until the game exits, removing the marker. Run it as a background task: the task finishing is the "game exited" signal, and cancelling it kills the game. Port 8772, override with `ONIACCESS_DEV_PORT`.

```
curl -s 127.0.0.1:8772/health                          # ok
curl -s --data-binary @probe.cs 127.0.0.1:8772/eval    # C# on the main thread; REPL state persists; the last expression's value is on the "=> " line
curl -s 127.0.0.1:8772/speech?since=0                  # "cursor: N" then "index: text" lines; pass the cursor back to get only new lines
curl -s 127.0.0.1:8772/gui                             # game state, handler stack (top first), KScreen stack, hotkeys
curl -s -d 'key DownArrow' 127.0.0.1:8772/input        # raw Unity key for one frame; modifiers +ctrl +shift +alt are the mod's logical ones (ctrl = Option on Mac)
curl -s -d 'action Escape' 127.0.0.1:8772/input        # a game Action through the game's input tree; empty body lists both forms and every Action
curl -s -X POST 127.0.0.1:8772/loadsave                # from the main menu: load the newest save and block until the colony is interactive (or pass a save path)
curl -s 127.0.0.1:8772/screenshot                      # PNG path
curl -s -X POST 127.0.0.1:8772/reload                  # hot-swap the module (build.sh --module does this)
python3 tools/gamewait.py health|menu|ingame|loadsave  # poll until a state is reached; exits early if the game died
```

Eval sees only PUBLIC members. `OniAccess.Dev.DevApi` exposes the module assembly (`DevApi.Asm`, reflect into internals from there), `DevApi.Say(text)`, and `DevApi.Screen()`; any new helper meant for eval must be public. Eval sessions reset on reload. The REPL reads `a * b` as a pointer declaration; write the multiplication another way. Speech reaches `/speech` through the tap in `SpeechPipeline`, right before the backend call, so the log holds exactly what the engine was handed. Key injection works by a DEBUG-only Harmony prefix on `UnityEngine.Input.GetKeyDown`/`GetKey`, applied only when the server is up.

Mac prerequisites beyond the .NET SDK: `brew install mono powershell` (Mono runs the tests, PowerShell runs `validate-reflection.ps1`).

`windows/build.ps1` and `windows/test.ps1` are the Windows counterparts (`powershell -ExecutionPolicy Bypass -File windows\build.ps1`). A checkout built on one OS needs its `obj/` directories deleted before building on the other; the intermediate files are not portable.

## Release

On the Mac, `./release.sh <version>` bumps the version in the csproj and `mod_info.yaml`, turns the unreleased section of `changes.md` into the new version's section, builds, commits `Release <version>`, tags `v<version>`, pushes, publishes the GitHub release with `release.zip` and the changelog entries as notes, and fills `release/` with the same files. The Steam Workshop upload is manual: on Windows, point the Oxygen Not Included Mod Uploader at the repo's `release/` folder. `--dry-run` does everything up to the commit, prints the notes, and reverts the edits.

When a build fails on a type or method signature, look it up in `ONI-Decompiled/` before guessing at fixes.

## Translations

`strings_template.pot` and `translations/*.po` must always carry the same keys in the same order. After adding, removing, or rewording any `LocString`: build, launch the game once so it regenerates the template in the game's `mods/strings_templates/` folder, then run `python3 sync-translations.py` (works on Mac and Windows). It copies the template into the repo, rewrites every `.po` to match it, and lists the keys whose `msgstr` is empty; translate those, then rerun with `--check` to confirm nothing is left. Entries whose English text changed are blanked (the game speaks a stale `msgstr` verbatim; an empty one falls back to English) and keep the old text as `#|` comment lines until retranslated.

## Project Structure

- `OniAccess/` - mod source code (C#, .NET Framework 4.8, Harmony patches); its csproj builds only the host files listed in the Build section
- `OniAccess.Module/` - the module project; compiles everything else under `OniAccess/` into the hot-reloadable module
- `OniAccess/Dev/` - the dev server (host core + module routes), all `#if DEBUG`
- `vendor/` - `Mono.CSharp.dll`, the dev server's REPL compiler (Debug only)
- `scripts/` - `oni-env.sh`, `run-game.sh` (launch with the dev server on)
- `tools/` - `gamewait.py`, the dev-server wait helper
- `ONI-Decompiled/` - decompiled game source for reference (read-only, not part of build)
- `docs/` - design documentation
- `docs/game-mechanics/` - game mechanics reference (topic files, wiki articles, strategy guides). See its CLAUDE.md for details.
- `.planning/` - project planning files
- `changes.md` - changelog for user-facing features and bug fixes

## Changelog

When committing a new feature or bug fix, add an entry to `changes.md`. Keep entries short — one line per change, written from the player's perspective (what changed for them, not implementation details).

## Code Style

- Harmony patch classes: `GameType_MethodName_Patch` (e.g., `KScreen_Activate_Patch`)
- All speech goes through `SpeechPipeline`, never call `SpeechEngine.Say()` directly
- All logging goes through `Log.Info/Debug/Warn/Error`, never use `Debug.Log` directly

## Test

```
./test.sh
```

Builds and runs the offline test suite (`OniAccess.Tests`) under Mono. Tests run without the game. The test project references the module project with a pinned assembly name (`OniModuleName=OniAccess.Module`) and gets the host through it. All new tests must work offline — never add tests that require launching the game. Don't test individual screen handlers.

- Every test should have a plausible failure mode not covered by another test — don't test the same invariant twice
- Always test real code paths; never test local helpers that simulate production behavior
- Exception: TextFilter-style regression suites keep full coverage (chain of replacements where any change can break unrelated cases)
- Guard speech-boundary code even when it looks simple — a wrong value reaching the speech engine is a silent failure

## Project Rules

### Reuse game data, avoid hardcoding
Use the game's localized text (`STRINGS` namespace, `LocText` components), UI state, and entity data wherever possible. Hardcoded text becomes stale across game updates and blocks translation. Only hardcode when no game data source exists.

### Never cache game state
Do not copy game data into mod-side dictionaries, lists, or string fields for later use. Always re-query the game when you need a value. A sighted player can see when the screen contradicts itself; a blind player trusts speech absolutely. Stale data is worse than no data. The only acceptable "cache" is holding a reference to a live Unity component (e.g., a `KSlider` or `LocText`) and reading its properties at speech time.

### No inline non-punctuation string literals
All user-facing text must come from a `LocString` reference. Never inline string literals for text that gets spoken. Prefer the game's `STRINGS` namespace — search `ONI-Decompiled/` for existing localized text before adding to `OniAccessStrings.cs`. The game already has strings for common labels ("Embark", "Close", "Cancel", etc.). Only add mod-authored strings when no game equivalent exists.

### Concise announcements
**These rules apply to mod-authored text only; never alter, truncate, or reword game text.** Users are experienced screen reader users. Strip fluff, never strip information.
- No positional item counts ("3 of 10") — the screen reader already tracks list position
- No navigation hints ("press Enter to select") unless unusual controls, and on a delay
- No redundant context ("You are now in...")
- No type suffixes when obvious ("Lumber button")
- DO include all gameplay-relevant details (traits, difficulty, descriptions). Concise means no fluff, not less information
- The sooner a message's varying part appears, the faster the user can keep going. Put the distinguishing word first.
  - WRONG: "cursor anchored" / "cursor unanchored" - user must listen through "cursor" before hearing the difference.
  - CORRECT: "anchored cursor" / "unanchored cursor" - first syllable already differs.
- Avoid emdash. Screen readers announce it as "dash" which breaks the flow of speech

### Conscious hotkey management
ONI has extensive hotkeys. Many are useless to blind players and can be overwritten. But every overwrite is a deliberate decision; document what the original hotkey did and why it's being replaced. See `docs/hotkey-reference.md` for the complete ONI key binding map, safe keys, and screen reader keys to avoid.

### No silent failures
This mod runs on Harmony patches and reflection. Both fail in ways that produce no visible error unless we log it. A swallowed exception in a patch means the feature silently stops working and the user has no idea why. **Every catch block must log via `Log.Warn` or `Log.Error`.** Never write an empty catch, never catch-and-return-default without logging. If something fails, the player log must say what and where. A logged failure is actionable; a silent one is invisible.

`Log.Error` ends the session: it maps to `Debug.LogError`, which ONI's `KCrashReporter` treats as a crash, showing its crash dialog and quitting when it is closed. That is deliberate. Anything that affects the player's experience (lost or degraded speech, a feature that stopped working, a fallback to something worse) is an error worth ending the session over, because otherwise nobody finds out and it never gets fixed. Use `Log.Warn` only for what does not touch play (a config value clamped, a release failing at shutdown). Never downgrade an error to a warning to keep the game running.

## Architecture Gotchas
- **Edit discipline** - always Read the exact lines immediately before editing. Never compose old_string from memory or earlier reads; tab depth is easy to miscount. Files are LF everywhere; the Edit tool matches bytes exactly
- New screen handlers must be registered in `ContextDetector.RegisterMenuHandlers()` or they will never activate
- Key detection goes in `Tick()` via `UnityEngine.Input.GetKeyDown()`. `HandleKeyDown()` is primarily for Escape interception through KButtonEvent
- `UnityEngine.Input` must be fully qualified inside the `OniAccess.Input` namespace. Bare `Input` resolves to the namespace, not the Unity class
- **Show-lifecycle patches**: Always check the decompiled source to see whether the screen declares `Show` or `OnShow`, then patch whichever it declares. If it declares neither (e.g. `CodexScreen`), patch `typeof(KScreen)` instead and filter with `__instance is ScreenType` in the postfix — Harmony requires the target method to be declared on the patched type, not just inherited

## Game Log

The Unity player log is at `~/Library/Logs/Klei/Oxygen Not Included/Player.log` on the Mac (`%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log` on Windows). Lines prefixed with `[OniAccess]` are mod debug output.

## Common LLM Antipatterns

### Comments referring to what changed
Comments should describe the current state, not the change history. Consider whether a comment is needed at all.

**WRONG**: `// Removed the old UI system. Now x does y.`
**WRONG**: `// Changed to use controllers. Now handles force_close`
**CORRECT**: `// Can be closed with the controller`

### Defensive null handling
Excessive validation hides bugs. Only null-check where null is a legitimate, expected state (e.g., after `FirstOrDefault()`, at public API boundaries). Let code crash otherwise — a crash is visible, a silently swallowed null is not. Trust private callers.

**WRONG** — silently returning empty instead of crashing:
```csharp
if (entity == null) return new List();
var controller = entity.GetControlBehavior();
if (controller == null) return new List();
```

**WRONG** — `?.` on things that should never be null:
`var name = entity?.GetController()?.Sections?.FirstOrDefault()?.Name ?? "default";`

**CORRECT**: `var name = entity.GetController().Sections.FirstOrDefault()?.Name ?? "default";`

### Padding and false balance
Don't invent concerns to appear thorough. If there are no problems, say "no issues." Don't present two options as equally valid out of fairness when one is clearly better — just recommend the better one. 
