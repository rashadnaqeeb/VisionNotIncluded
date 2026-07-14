# ONI Hotkey Reference

Reference of all ONI key bindings and mod key assignments.
Source: decompiled `Global.GenerateDefaultBindings()` and mod source.

## How Input Works

The mod uses two mechanisms together:

- **Detection**: `UnityEngine.Input.GetKeyDown()` in handler `Tick()` methods (via `KeyPoller`). The mod only checks keys in the handler currently on top of its `HandlerStack`, so keys are naturally scoped to the active mod screen.
- **Suppression**: `ModInputRouter`, registered in the game's `KInputHandler` tree at priority 50 (above `PlayerController` at 20, `CameraController` at 1). It receives every `KButtonEvent` from the game's input pipeline and consumes events the mod claims, preventing game handlers from seeing them.

Suppression works through three mechanisms in `ModInputRouter`:
1. **`_globalBlockedActions`**: Always blocks `Plan1-14` and `PanUp/Down/Left/Right` regardless of handler state.
2. **`CapturesAllInput`**: When the active handler has this flag (screen handlers like menus, tables, cluster map), ALL non-passthrough keys are consumed.
3. **`ConsumedKeys`**: Per-handler list of specific physical keys to consume when `CapturesAllInput` is false (used by `TileCursorHandler`, `BuildToolHandler`, `ToolHandler`).

When the mod uses a key, it must also suppress the game's binding for that key via one of these mechanisms, or the game action will fire alongside the mod action.

## Game Binding Groups

| Group | When Active | Examples |
|-------|------------|---------|
| Root | Always during gameplay | Escape, Pause, Speed, Overlays, Camera Pan, Tool activations |
| Navigation | Always during gameplay | Camera bookmarks (Ctrl+1-0, Shift+1-0) |
| BuildingsMenu | Build submenu open | A-Z letter keys for building selection |
| Building | Building selected in details | \\, [, ], Enter (toggle), / (open/close) |
| Sandbox | Sandbox tools active | Shift+letter combos for sandbox tools |
| Debug | Debug mode enabled | Backtick, Backspace, Ctrl/Alt combos |
| CinematicCamera | After Alt+S | Camera controls rebound to WASD, I/O zoom |
| Management | Management screen open | L, F, V, R, ., J, E, U, Z |

## Keys NOT Bound by ONI (Root context)

These have no Root-context game binding, making them safe for the mod without needing to suppress anything during normal gameplay:

- **Arrow keys** -- natural choice for accessibility cursor
- **PageUp, PageDown, Home, End** -- used by mod scanner
- **Ctrl+R, Ctrl+B** -- used by mod (red alert, ruler)
- **Q** (bare, no modifier) -- bound only in BuildingsMenu context (build submenu letter key), not Root
- **Backspace** (bare) -- bound only in Debug context (DebugToggle), not Root
- **Backtick** -- bound only in Debug context (ToggleProfiler), not Root

## Screen Reader Keys -- NEVER Bind

| Key | Reason |
|-----|--------|
| Insert | NVDA modifier |
| CapsLock | NVDA/JAWS modifier |
| Insert+anything | NVDA commands |

## Mod Hotkeys by Context

### Global (KeyPoller -- always active when mod is enabled)

| Key | Action | Game Conflict |
|-----|--------|---------------|
| Ctrl+Shift+F12 | Toggle mod on/off | Debug Trigger Error in debug mode only -- negligible risk |
| F12 | Open settings | Steam screenshot -- outside game input system, most users disable or rebind it in Steam |
| Shift+/ (?) | Context help | / is Toggle Open in Building context, but Building context isn't active when help opens |

### Tile Cursor (TileCursorHandler -- main gameplay)

Camera pan (WASD) and build category keys (Plan1-14) are globally blocked by `ModInputRouter._globalBlockedActions`. Other game bindings are suppressed per-key via `ConsumedKeys` on the tile cursor handler. Tool activation keys (G, C, X, M, etc.) are NOT consumed and pass through to the game, which is intentional -- the mod detects tool activations via `OnActiveToolChanged` and pushes appropriate tool handlers.

**Movement:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| Arrow keys | Move cursor | None | N/A -- unbound |
| Ctrl+Arrows | Skip cursor | None | N/A -- unbound |
| Shift+Up/Down | Change cursor radius | None | N/A -- unbound |
| Ctrl+Shift+Down | Reset cursor radius | None | N/A -- unbound |

**Information:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| I | Read tooltip | Disinfect tool activation | ConsumedKey |
| Shift+K | Read coordinates | Sandbox Sample (sandbox only) | ConsumedKey |
| Q | Read cycle status | BuildMenuKeyQ (build submenu only) | ConsumedKey |
| Shift+Q | Read time played | None | ConsumedKey |
| S | Colony status summary | PanDown (camera) | Globally blocked |
| D | Read diagnostics | PanRight (camera) | Globally blocked |
| Shift+D | Open diagnostic browser | Disconnect tool activation | ConsumedKey |

**Duplicant Tracking:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| [ | Cycle dupe backward | BuildingUtility2 (Building context) | ConsumedKey |
| ] | Cycle dupe forward | BuildingUtility3 (Building context) | ConsumedKey |
| \\ | Jump to / select dupe | BuildingUtility1 (Building context) | ConsumedKey |
| Shift+\\ | Check pathability | None | ConsumedKey |

**Colony Status:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| W | Open world selector | PanUp (camera) | Globally blocked |
| Shift+N | Open notification menu | Sandbox Sprinkle (sandbox only) | ConsumedKey |
| Ctrl+R | Toggle red alert | None | N/A -- unbound |
| Shift+I | Open resource browser | None | ConsumedKey |
| Shift+P | Read pinned resources | None | ConsumedKey |

**Scanner:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| Ctrl+F | Open scanner search | Find (game search) | ConsumedKey |
| End | Refresh scanner | None | N/A -- unbound |
| Shift+End | Toggle auto-move | None | ConsumedKey |
| Home | Teleport to result | None | N/A -- unbound |
| Shift+Home | Orient toward result | None | ConsumedKey |
| Backspace | Jump back | DebugToggle (Debug only) | ConsumedKey |
| PageUp/Down | Cycle scanner items | None | N/A -- unbound |
| Ctrl+PageUp/Down | Cycle categories | None | ConsumedKey |
| Shift+PageUp/Down | Cycle subcategories | None | ConsumedKey |
| Alt+PageUp/Down | Cycle instances | None | ConsumedKey |

**Spatial Tools:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| Ctrl+B | Place ruler | None | N/A -- unbound |
| Ctrl+Shift+B | Clear ruler | None | ConsumedKey |
| H | Jump home | CameraHome (Root) | ConsumedKey |
| Alt+H | Orient toward home | None | ConsumedKey |

**Bookmarks:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| Ctrl+0-9 | Set bookmark | SetUserNav1-10 | Game binding passes through (both fire) |
| Shift+0-9 | Go to bookmark | GotoUserNav1-10 | ConsumedKey |
| Alt+0-9 | Orient toward bookmark | Debug actions (Debug only) | ConsumedKey |

**Misc:**

| Key | Action | Game Binding | Suppression |
|-----|--------|-------------|-------------|
| Backtick | Cycle game speed | ToggleProfiler (Debug only) | ConsumedKey |
| Return | Open entity picker | DialogSubmit / ToggleEnabled (Building) | ConsumedKey |
| Tab | Open action menu | CycleSpeed (Root) | ConsumedKey |

### Details Screen (DetailsScreenHandler)

| Key | Action | Game Binding | Why Safe |
|-----|--------|-------------|----------|
| \\ | Copy settings tool | BuildingUtility1 | Same function -- mod makes it accessible |
| Ctrl+Tab | Advance section | None | Unbound |
| Ctrl+Shift+Tab | Retreat section | None | Unbound |

### Build Tool (BuildToolHandler -- placing a building)

| Key | Action | Game Binding | Why Safe |
|-----|--------|-------------|----------|
| Space | Place building (repeat) | TogglePause | Pause is not useful mid-placement |
| Shift+Space | Clear / quick cancel | None | Unbound |
| Return | Place and exit | DialogSubmit | No dialog active during placement |
| R | Rotate | ManageResearch (Management) | Management screen not open during placement |
| Shift+R | Reverse rotate | None | Unbound |
| Tab | Return to building list | CycleSpeed | Speed cycling not useful during placement |
| I | Open info panel | Disinfect tool | Tool activation irrelevant during build |
| Shift+P | Announce ports | Prioritize tool | Tool activation irrelevant during build |
| Ctrl+G | Cycle rectangle mode: single, filled, hollow (1x1) | Dig tool activation | Tool activation irrelevant during build |
| 0-9 | Set build priority | Build categories | Build submenu not open during placement |

### Tool Handler (ToolHandler -- dig, mop, etc.)

| Key | Action | Game Binding | Why Safe |
|-----|--------|-------------|----------|
| Space | Set rectangle corner | TogglePause | Pause not useful mid-tool |
| Shift+Space | Clear rectangle | None | Unbound |
| Return | Confirm/cancel tool | DialogSubmit | No dialog during tool use |
| F | Open filter menu | ManageConsumables (Management) | Management not open during tool use |
| Ctrl+Delete | Cancel all on map (cancel tool only) | None | Unbound |
| 0-9 | Set priority | Build categories | Build menu not open during tool use |

### Cluster Map (ClusterMapHandler -- Spaced Out starmap)

| Key | Action | Game Binding | Why Safe |
|-----|--------|-------------|----------|
| U, O, J, L, N, . | Hex navigation | Various (ManageDatabase, RotateBuilding, ManageSkills, etc.) | Management/building bindings not active on starmap |
| Arrow keys | Hex navigation (alternate) | None | Unbound |
| H | Jump to active world | CameraHome | Starmap replaces camera home function |
| Return | Select / confirm | DialogSubmit | No dialog during starmap |
| Ctrl+Return | Switch to world | None | Unbound |
| K | Read coordinates | Clear (Sweep) | Tool activation irrelevant on starmap |
| I | Read entity details | Disinfect | Tool activation irrelevant on starmap |
| Space | Set path start | TogglePause | Pause not useful during pathfinding |
| D | Calculate path | PanRight (camera) | Camera irrelevant on starmap |
| Ctrl+F | Scanner search | Find | Game search not useful on starmap |
| Scanner keys | Same as tile cursor | Same | Same reasons |

### Menus (BaseMenuHandler -- shared by all list screens)

| Key | Action | Notes |
|-----|--------|-------|
| Up/Down | Navigate items | Arrow keys unbound by game |
| Ctrl+Up/Down | Jump groups | Unbound |
| Home/End | First/last item | Unbound |
| Return | Activate item | |
| Tab/Shift+Tab | Navigate tabs | Tab overwrites CycleSpeed on these screens |
| Left/Right | Adjust/collapse/expand | Unbound |
| A-Z | Type-ahead search | Overwrites build submenu keys, but build menu isn't open on these screens |

### Tables (BaseTableHandler -- vitals, consumables, priorities)

| Key | Action | Notes |
|-----|--------|-------|
| Up/Down/Left/Right | Navigate cells | Arrow keys unbound |
| Home/End | First/last cell | Unbound |
| Return | Sort column / activate | |
| A-Z | Column search | |

### Schedule Screen (SchedulesTab)

| Key | Action | Notes |
|-----|--------|-------|
| Space | Paint cell | Overwrites TogglePause -- acceptable on this screen |
| 0-9 | Select brush | |
| Ctrl+Up/Down | Reorder schedules | |
| Shift+Left/Right | Paint and move | |
| Shift+Home/End | Paint range | |

### Other Screen-Specific Keys

**Research TreeTab**: Up/Down/Left/Right for tree navigation, Return to queue research.

**Skills TreeTab**: Up/Down/Left/Right for tree navigation, Return to learn skill, +/- for boosters.

**Resource Browser**: Space to toggle pin, Shift+C to clear pins.

**Diagnostic Browser**: Space to toggle pin/criterion.

**Notification Menu**: Delete to dismiss notification.

**Codex ContentTab**: Alt+Left or Backspace for history back, Alt+Right for history forward.

**Consumables Table**: Return on column header toggles checkbox.

**Priority Table**: Shift+Up/Down to adjust priority value.

## Management Screen Hotkeys (base game, active from gameplay)

These open management screens. The mod doesn't override them.

| Key | Screen |
|-----|--------|
| L | Priorities |
| F | Consumables |
| V | Vitals |
| R | Research |
| . | Schedule |
| J | Skills |
| E | Report |
| U | Database (Codex) |
| Z | Starmap |

## Overwritten Game Functions

Keys where the mod suppresses the original game function. Only listed if the game function becomes inaccessible through that key while the mod is active.

| Key | Game Function Lost | Mod Function | Why Acceptable |
|-----|-------------------|--------------|----------------|
| W, A, S, D | Camera pan | S = colony status, W = world selector, D = diagnostics. A is globally blocked but unused by mod. | Camera panning not useful to blind players; tile cursor replaces it |
| H | CameraHome (jump to Printing Pod) | Jump to home bookmark | Printing Pod reachable via scanner |
| Tab | CycleSpeed | Action menu (tile cursor), tab navigation (menus) | Speed cycling moved to backtick |
| K | Clear (Sweep) tool activation | (no longer consumed — passes through to game) | N/A |
| I | Disinfect tool activation | Read tooltip / info | Tools activated via mod action menu |
| Shift+D | Disconnect tool activation | Open diagnostic browser | Tools activated via mod action menu |
| 1-0, -, =, Shift+-, Shift+= | Plan 1-14 (build category hotkeys) | Bookmarks (with Ctrl/Shift/Alt), build priority (in tool context) | Build menu navigated through mod's menu handler |
| Shift+0-9 | GotoUserNav (camera bookmarks) | Go to mod bookmark | Mod bookmarks replace camera bookmarks |
| Ctrl+F | Find (game search) | Scanner search | Mod scanner replaces game search |
| Space (build/tool only) | TogglePause | Place/confirm building or tool area | Backtick pauses; pausing mid-placement not useful |

**NOT overwritten** (game function passes through alongside mod):
- **Ctrl+0-9** (SetUserNav) -- both mod bookmark and game camera bookmark fire
- **N bare** -- not consumed; Capture (Wrangle) tool activation still fires
- **G, C, X, M, B, P, Y, T** -- tool activation keys pass through intentionally; mod detects tool changes via `OnActiveToolChanged`
