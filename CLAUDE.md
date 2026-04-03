# OniAccess - Claude Code Instructions

OniAccess is an accessibility mod for Oxygen Not Included that makes the game playable for blind users. It uses Harmony patches to hook into the game's UI and provides speech output as the sole interface — there is no visual fallback. Every decision should be weighed against the fact that if something fails silently or speaks stale data, the player has no way to know.

## Build

Always use the build script, never `dotnet build` directly. 

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The script builds the DLL, deploys it to the game's local mods directory, and patches mods.json to keep the mod enabled.

When a build fails on a type or method signature, look it up in `ONI-Decompiled/` before guessing at fixes.

## Project Structure

- `OniAccess/` - mod source code (C#, .NET Framework 4.8, Harmony patches)
- `ONI-Decompiled/` - decompiled game source for reference (read-only, not part of build)
- `docs/` - design documentation
- `docs/game-mechanics/` - game mechanics reference (topic files, wiki articles, strategy guides). See its CLAUDE.md for details.
- `docs/CODEBASE_INDEX.md` - complete namespace reference for decompiled ONI source
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
powershell -ExecutionPolicy Bypass -File test.ps1
```

Builds and runs the offline test suite (`OniAccess.Tests`). Tests run without the game. All new tests must work offline — never add tests that require launching the game. Don't test individual screen handlers.

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
All user-facing text must come from a `LocString` reference. Never inline string literals for text that gets spoken. Prefer the game's `STRINGS` namespace — search `docs/CODEBASE_INDEX.md` and `ONI-Decompiled/` for existing localized text before adding to `OniAccessStrings.cs`. The game already has strings for common labels ("Embark", "Close", "Cancel", etc.). Only add mod-authored strings when no game equivalent exists.

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

## Architecture Gotchas
- **Edit discipline** - always Read the exact lines immediately before editing. Never compose old_string from memory or earlier reads; tab depth is easy to miscount. Working tree files use CRLF on Windows (`core.autocrlf=true`, `.gitattributes: * text=auto`); the Edit tool matches bytes exactly, so stale reads will fail on line endings too
- New screen handlers must be registered in `ContextDetector.RegisterMenuHandlers()` or they will never activate
- Key detection goes in `Tick()` via `UnityEngine.Input.GetKeyDown()`. `HandleKeyDown()` is primarily for Escape interception through KButtonEvent
- `UnityEngine.Input` must be fully qualified inside the `OniAccess.Input` namespace. Bare `Input` resolves to the namespace, not the Unity class
- **Show-lifecycle patches**: Always check the decompiled source to see whether the screen declares `Show` or `OnShow`, then patch whichever it declares. If it declares neither (e.g. `CodexScreen`), patch `typeof(KScreen)` instead and filter with `__instance is ScreenType` in the postfix — Harmony requires the target method to be declared on the patched type, not just inherited

## Game Log

The Unity player log is at `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`. Lines prefixed with `[OniAccess]` are mod debug output.

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
