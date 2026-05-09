# OniAccess Reference

OniAccess is an accessibility mod that makes Oxygen Not Included playable for blind users through screen reader speech output. This is the full feature reference. For install instructions and getting started, see [getting-started.md](getting-started.md). For recent changes, see [changes.md](changes.md).

## Context help and mod toggle

**?** (Shift+/) opens an interactive help list tailored to whatever screen you're on. The list changes depending on context -- the help you see in the colony view is different from the help inside a details screen or while building a building. The list supports type-ahead search, so you can type part of a key name to jump to it.

**Ctrl+Shift+F12** toggles the entire mod off. All speech stops and every key passes through to the game as if the mod weren't installed. Press it again to re-enable. This is the only key that works while the mod is disabled.

## Tile cursor

The tile cursor is your primary way of exploring the map. Arrow keys move one tile at a time. Each tile announces any building, its element (with mass), entities, active orders, and debris -- in that order. Gases are suppressed when a building or foundation covers the cell.

Most overlays prepend one extra reading before the standard tile information. Temperature adds the temperature (with a warning near phase transitions), Light adds lux, Radiation adds rads, Decor adds the value with sign, and Disease adds germ counts by type or "clean." The utility overlays (Power, Plumbing, Ventilation, Conveyor) add network/conduit data, and Automation adds signal state. Rooms prepends the room name, announced once per room rather than every tile.

### Skip

**Ctrl+Arrow** skips in a direction until something changes, then announces how many tiles were crossed. What counts as a "change" depends on the active overlay:

- **Default view**: different building, tile type, or element
- **Temperature**: different temperature band (8 bands from below freezing to above 1800 C)
- **Power / Plumbing / Ventilation / Conveyor**: follows a pipe/wire, stops at junctions or at the end of the pipe/wire. Doesn't jump between networks.
- **Rooms**: different room
- **Disease**: transition between clean and infected
- **Light / Radiation / Decor**: different value band, set by the game

Skip also stops at the alignment ruler if one is placed, and at world boundaries.

**Alt+Arrow** skips using the default overlay regardless of which overlay is active, but with coarser grouping: all floor tiles count as one zone, all ladders as one zone, all plants, all decorations, all liquids, and all natural solids likewise. This is useful for jumping between major areas without stopping at every material or building change.

### Big cursor

**Shift+Up/Down** cycles the cursor size: 1x1 (default), 3x3, 5x5, 9x9, 21x21. **Ctrl+Shift+Down** resets to 1x1. The size also resets on world load.

When the cursor is larger than 1x1, arrow keys move by the full cursor width (e.g., 5 tiles at 5x5), tiling areas edge-to-edge. The cursor stops where the full area fits inside the world. Ctrl+Arrow skip is unaffected by cursor size.

Landing on a tile speaks an area scan summary instead of the single-tile glance. The scan adapts to the active overlay:

- **Default / utility overlays**: solid/liquid/gas/vacuum percentages, buildings by type, dupes, critters, pending orders by type
- **Materials**: element breakdown by percentage
- **Oxygen**: O2 and polluted O2 percentages with median mass
- **Temperature / Light / Decor / Radiation**: area average
- **Disease**: average germ count per type
- **Rooms**: room types intersecting the area
- **Crops**: plant count by type with average growth percentage

All scans report unexplored percentage first if any tiles in the area haven't been revealed. Coordinate reading (Shift+K) always reports the center cell.

### Coordinates

**Shift+K** reads the cursor's X,Y position relative to the Printing Pod. The coordinate display mode (Off, Append, or Prepend) is set in Settings (F12) — in Append or Prepend mode, coordinates are included in every tile announcement automatically. The setting persists across sessions.

If your Printing Pod was somehow destroyed, 0, 0 becomes the center of the map.

### Tooltip

**I** reads the most relevant tooltip block for the current tile. The mod picks which block based on the overlay -- in utility overlays it prioritizes conduit data, in others it prioritizes the building.

### Entity picker

If a tile has multiple selectable objects, **Enter** opens a picker listing them all. Single-entity tiles select directly. When available, an item's full tooltip is also read in this menu, falling back to just the name if no tooltip is available.

## Scanner

The scanner catalogs everything on the current asteroid into a four-level hierarchy: category, subcategory, item, instance.

- **End** -- perform a full scan. Results are organized into categories (Solids, Liquids, Gases, Buildings, Networks, Automation, Debris, Zones, Life), each with subcategories and an "all" subcategory. Each item announces its name, distance from you, and list position, sorted by distance from the cursor. Destroyed entities are silently pruned.
- **Ctrl+PageUp/Down** -- cycle categories
- **Shift+PageUp/Down** -- cycle subcategories
- **PageUp/Down** -- cycle items
- **Alt+PageUp/Down** -- cycle instances of the current item
- **Home** -- teleport the cursor to the current instance
- **Backspace** -- return to your position before the last teleport (one position saved)
- **Shift+Home** -- announce distance from the cursor to the current item
- **Shift+End** -- toggle auto-move. When on, the cursor teleports automatically as you cycle, and distances are measured from where you scanned rather than the cursor's current position

### Clustering

Most scanner results are spatially clustered: adjacent cells of the same type merge into a single entry showing the cell count (e.g., "47 Granite"). This applies to elements, constructed tiles, biome zones, and orders like dig or mop. Two pools of the same liquid that aren't touching appear as separate entries.

Utility networks (Power, Plumbing, Ventilation, Conveyor, Automation) cluster differently. Segments are grouped by the game's network ID and segment type, so all regular wire on the same electrical network is one entry, but insulated wire on that same network is a separate entry. Bridge buildings (connectors, transformers, etc.) are listed individually rather than clustered.

### Scanner search

**Ctrl+F** opens a text input. Type a query and press Enter. Results are grouped in an all category and under their original categories as subcategories, sorted by match quality (prefix matches rank highest). Escape cancels the search.

The scanner clears automatically when you switch asteroids.

## Tools

Selecting a tool (Dig, Deconstruct, Mop, etc.) from the action menu (Tab) enters tool mode. The cursor still moves normally, but you now place orders instead of inspecting.

### Rectangle selection

- **Space** -- set the first corner. Move to the opposite corner and press Space again to complete the rectangle. A drag sound plays whose pitch reflects the selection size. You can place multiple rectangles before confirming
- **Enter** -- confirm all pending rectangles. If no rectangle is set, Enter confirms a single cell under the cursor
- **Ctrl+G** -- toggle single-cell selection mode. In single mode, Space selects one cell at a time instead of setting rectangle corners
- **Shift+Space** -- remove the cell under the cursor from the selection

With a big cursor active, Space sets both corners at once, creating a rectangle the size of the cursor area. Enter also applies the full cursor rectangle but confirms immediately, closing the tool. The disconnect tool (for splitting pipe/wire segments) always uses single-cell selection regardless of cursor size.

### Priority

**0-9** sets the priority for future placements. 0 is emergency (top priority/yellow alert), 1-9 are normal priorities. The priority is announced when you activate a tool and when you change it.

### Filters

**F** opens a filter picker for the active tool, showing available filter layers. Changing the filter clears any pending selection.

Switching overlays while a tool is active automatically changes the tool's filter to match. For example, switching to the plumbing overlay while deconstructing changes the filter to target pipes.

### Sandbox

Sandbox mode must be enabled in the game settings when starting a new colony. Once enabled, **Shift+S** toggles it on and off. Sandbox tools are listed under the Sandbox Tools category in the action menu (Tab). Rectangle-mode tools (Brush, Sprinkle, Heat, Stress, Clear Floor, Destroyer, Fog of War, Critter) use the same two-press corner selection as regular tools. Single-cell tools (Flood, Spawner, Story Trait) apply at the cursor with Space or Enter.

- **Space** -- set corners (rectangle tools) or apply (single-cell tools)
- **Enter** -- confirm and exit the tool
- **F** -- open the parameter menu (element, entity, mass, temperature, etc.). Sliders adjust with Left/Right, selectors open a nested picker with Enter
- **Ctrl+Space** -- sample the tile under the cursor into the parameter menu
- **Shift+Space** -- clear the rectangle under the cursor

Big cursor works with sandbox tools the same way it does with regular tools: Space sets both corners at once.

## Building

**Tab** from the colony view opens the action menu. It has three levels: categories (Tools, Housing, Food, Power, etc.), subcategories, and individual buildings.

At the building level, navigation wraps across subcategories and categories -- you can scroll continuously through every building without backing out. **Ctrl+Up/Down** jumps between subcategory boundaries. Type-ahead search works across all buildings and tools regardless of category. All nested menus in the mod work like this.

Selecting a building enters placement mode:

- **Space** -- place one copy at the cursor
- **Enter** -- place and return to the map immediately
- **R** -- rotate (announces new orientation and extent for multi-tile buildings). Not all buildings are rotatable. Some have only 2 orientations
- **Tab** -- return to the building list at the same position
- **I** -- read building description, effects, and material requirements. Material and facade can be changed from here
- **Shift+P** -- announce port layout (input/output positions for pipes, wires, automation, etc.)
- **Ctrl+G** -- toggle rectangle mode (single-cell buildings only). Space sets corners, Enter fills the rectangle
- **0-9** -- set construction priority
- **Shift+Space** -- cancel existing construction at the cursor

The cursor is always at the bottom-left corner of the building footprint. Multi-tile buildings extend up and to the right from the cursor. Rotating changes which corner faces down-left. Utility bridges are the exception: the cursor is always at the input end, and the rotation direction points toward the output. For example, a bridge rotated down has the cursor at the top (input) and the output below.

Utility buildings (pipes and wires) use line placement: Space sets the start, then move in a straight line and Space again to complete the run. If any tiles along the line are invalid, the placement will fail.

## Colony status

These readouts are available from the colony view:

- **Q** -- cycle number, current schedule block, and alert state (red/yellow alert if active)
- **Shift+Q** -- total hours played
- **S** -- colony summary: duplicant count (local and cluster-wide if Spaced Out), sick count (if any), rations with trend, max stress with trend, and electrobank energy with trend (only if bionic duplicants are present). Trends are "rising" or "falling" based on 10-minute history
- **Shift+P** -- all pinned resource amounts
- **Backtick (`)** -- cycle game speed (1x, 2x, 3x)
- **D** -- diagnostic alerts sorted by severity (worst first). Only diagnostics pinned to the sidebar are included. Each announces name, status, and current value
- **Shift+D** -- open the diagnostics browser (see below)
- **Ctrl+R** -- toggle red alert
- **W** -- open the world selector (Spaced Out DLC). Lists all discovered asteroids with type-ahead search. Switching worlds moves the cursor to the new asteroid

The mod also announces automatically without input: pause/unpause (with speed on unpause), speed changes, new cycles, and red/yellow alert transitions. During initial game load, notifications are suppressed until you first unpause.

## Duplicant tracking

- **[** and **]** -- cycle through all living duplicants on the current asteroid. Each announcement includes the dupe's name, critical statuses, position relative to the cursor (e.g. "3 up 2 left"), and current task with target building. Dupes who can't pathfind to other dupes, the telepad, or their bed are announced as "trapped"
- **Backslash** -- jump the cursor to the current dupe's location. Press again when already on their tile to select them and open their details screen
- **Shift+Backslash** -- check if the current dupe can reach the cursor tile, reporting path cost if reachable or the nearest reachable tile if not
- **Ctrl+Backslash** -- follow the current dupe with the camera, announcing status and chore changes in real time. When follow movement earcons are enabled (F12 settings), a directional tone plays on each tile moved. [ and ] switch the follow target. Any cursor movement stops following
- **Shift+[** and **Shift+]** -- cycle through autonomous bots (Sweepy, Flydo, Rover, Biobot, Remote Worker) the same way. Backslash, Shift+Backslash, and Ctrl+Backslash work on whichever entity type was last cycled to

The status checks cover: incapacitated, critical health, injured, severe wounds, suffocating, holding breath, nervous breakdown, stressed, scalding, overheating, hypothermia, sick, starving, entombed, fleeing, and bionic battery states.

## Notifications

New notifications are batched over a short window and collapsed by title (e.g., "Stress Alert x3"). During game load, all notifications are held until you first unpause.

- **Shift+N** -- open the notification menu. Groups are listed by title with count
- **Enter** -- activate a notification (focuses the camera on the source, selects the entity, or opens a message dialog). Groups with multiple members drill into a submenu listing individual notifications with their source name and location
- **Delete** -- dismiss a group

## Spatial tools

### Ruler

- **Ctrl+B** -- place an alignment ruler at the cursor. The ruler provides audio feedback in three zones as you move: a click at the exact crosshair (same row and column), a higher-pitched tone on the same row or column, and a lower-pitched tone one tile away from the line. Skip movement stops at ruler lines
- **Ctrl+Shift+B** -- clear the ruler

### Bookmarks and jump home

- **Ctrl+1-0** -- save the current position as a bookmark (uses the game's native bookmark system)
- **Shift+1-0** -- jump to a saved bookmark
- **Alt+1-0** -- report direction and distance to a bookmark without moving
- **Shift+V** -- open the fast travel menu, a named-bookmark list scoped to the current colony and asteroid. Enter on an entry jumps the cursor; right-arrow into an entry to rename or delete it; the last row creates a new bookmark at the cursor
- **H** -- teleport the cursor to the Printing Pod

## Details screen

The details screen has three sections: main tabs, side screens, and action buttons. **Tab/Shift+Tab** cycles within the current section. **Ctrl+Tab/Ctrl+Shift+Tab** jumps between sections.

Main tabs vary by entity but include: Status (vitals, storage, process conditions, status items), Personality (bio, traits, attributes, resume, equipment), Chores (pending errands with assigned dupes), and Properties (germs, immune system, power generators/consumers/batteries). Side screens hold building-specific config panels -- sliders, toggles, dropdowns, and material selection. The action buttons section has context actions (e.g., cancel building, empty storage), priority adjustment, and links to the codex entry or rename.

### Sliders

Left/Right adjusts values. Modifier keys control step size:

- **Plain** -- 1 step (or 1% of range for fractional sliders)
- **Shift** -- 10 (or 10%)
- **Ctrl** -- 100 (or 25%)
- **Ctrl+Shift** -- 1000 (or 50%)

A boundary sound plays at minimum and maximum values.

### Dropdowns and radio groups

Left/Right cycles through options directly, announcing the new value.

### Move-to-location

From an entity's details, the move-to command enters a cursor mode. Navigate to the destination and press Space or Enter to confirm.

### Recipe queue

When a fabricator has a recipe queue side screen, it shows the recipe info, material slots, and queue. Tab/Shift+Tab cycles recipes. Left/Right adjusts the queue count using the same step sizes as sliders.

## Priority screen

The priority screen is a 2D grid with duplicants as rows and chore groups as columns. Each cell shows the priority level and the dupe's skill for that chore. Trait-disabled chores are announced as such.

- **0-5** -- set priority (0=disabled, 1=very low through 5=very high)
- **Shift+0-5** -- set the entire column (all dupes for that chore)
- **Shift+Up/Down** -- adjust priority of the current cell by 1
- **Ctrl+Left/Right** -- adjust all priorities in the current row by 1
- **Ctrl+Up/Down** -- adjust all priorities in the current column by 1

A toolbar row at the top provides Reset Settings and an Advanced (proximity) Mode toggle.

## Schedule screen

The schedule screen has two tabs: Schedules and Duplicants.

### Schedules tab

A 2D grid of schedules and 24 hour blocks. Each cell is a block type: Work, Hygiene, Recreation, or Sleep.

- **1/2/3/4** -- select a brush (Work, Hygiene, Recreation, Sleep)
- **Space** -- paint the current cell with the selected brush
- **Shift+Left/Right** -- paint while moving
- **Shift+Home/End** -- paint from cursor to start or end of the row
- **Ctrl+Left/Right** -- rotate hour blocks within the row (i.e., current hour 1 cell becomes hour 2)
- **Ctrl+Up/Down** -- reorder schedules
- **Enter** -- open options (Rename, Alarm, Duplicate, Delete, Add/Delete Row)

Schedules can have multiple timetable rows. Shift+Up/Down moves rows within a schedule.

### Duplicants tab

A flat list of duplicants showing their name, schedule trait (Early Bird, Night Owl), and assigned schedule. Left/Right changes the assignment.

## Research screen

The research screen has three tabs: Browse, Queue, and Tree.

### Browse tab

Techs grouped into three buckets: Available, Locked, and Completed. Each tech announces its name, state, research cost (or progress if partially complete), what it unlocks, and prerequisites if locked. Enter selects a tech for research. Space jumps to it in the Tree tab.

### Queue tab

Shows banked research points at the top, then queued techs in order. The queue allows queuing a tech whose prerequisites aren't met. Cancelling any queued tech cancels all queued techs.

### Tree tab

Navigates the tech tree as a graph. Up moves to a prerequisite, Down to a dependent, Left/Right cycles siblings. Enter selects a tech. Announces "root node" or "dead end" at boundaries.

All three tabs support type-ahead search across the full tech database.

## Resource and diagnostics browsers

Both browsers use the same nested navigation: Up/Down moves within a level, Enter drills in, Backspace goes back, Ctrl+Up/Down jumps between groups, and type-ahead search filters items at the current level.

**Shift+I** opens the resource browser. Categories are listed at the top level, with a synthetic "Pinned" category if any resources are pinned. Each category shows its total amount and trend. Drill into a category to see individual resources with total, reserved, available (or overdrawn), and trend. **Space** toggles pin status. **Shift+C** clears all pins. **Enter** on a resource lists all world instances with amount and location; Enter on an instance jumps the cursor there.

**Shift+D** opens the diagnostics browser, listing all colony diagnostics sorted alphabetically. Each diagnostic announces its name, status (normal, warning, bad, etc.), value, and pin state. Enter drills into individual criteria. **Space** at the top level cycles pin state (Always, Never, Alert Only). **Space** on a criterion toggles it on or off. Diagnostic conditions that worsen are also announced automatically without needing the browser open.

## Cluster map (Spaced Out DLC)

**Z** opens the starmap, which is a hex grid of asteroids, rockets, POIs, and other space entities. The cursor starts on the asteroid you opened from.

Six keys map to hex directions matching the keyboard layout: **U** (northwest), **O** (northeast), **J** (west), **L** (east), **N** (southwest), **.** (southeast). Arrow keys also work: Left/Right are always west/east, Up/Down alternate diagonals based on row parity so they zigzag north or south. Each hex announces its entities and fog-of-war state (unexplored, unknown object detected, or entity names).

**K** reads hex distance and compass direction from the starting asteroid. **I** reads detailed entity status items, only available on rockets and showers.

**Enter** selects an entity (or opens a picker if multiple share the hex). **Ctrl+Enter** switches your active world to the asteroid at the cursor. When the map is in destination-selection mode (from a rocket's "Select Destination" side screen), Enter confirms the destination and Escape cancels.

### Pathfinder

**Space** sets a start point. **D** calculates the shortest path from start to cursor and announces the hex count. If a shorter path exists through fog, both distances are reported.

### Scanner

The cluster map has its own scanner with the same keys as the tile scanner (End, PageUp/Down, etc.) but a simpler three-level hierarchy: category (Asteroids, Rockets, POIs, Meteor Showers, Unknown), item, instance. Peeked entities that the game deliberately hides are grouped under "Unknown." Ctrl+F searches across all entities.

## Type-ahead search

Most menu screens support type-ahead: start typing to filter. Matches are ranked in five tiers from start-of-string exact matches down to substring matches. Typing a single letter repeatedly cycles through items starting with that letter. Backspace edits the query. Escape clears the search.

## Settings (F12)

**F12** opens the settings screen. All settings persist across sessions. Navigate with Up/Down, toggle with Enter or Left/Right. Type-ahead search works here too. Settings are organized into four sections.

### Tile cursor settings

- **Tile cursor coordinate mode** (Off / Append / Prepend) -- controls whether X,Y coordinates are included in every tile announcement. Off by default
- **Lock zoom level when moving tile cursor** -- locks the camera zoom to level 10, which is the best level for audio. On by default
- **Announce biome changes** -- speaks the biome name when the tile cursor crosses into a different biome. On by default
- **Passability earcons** -- plays a warning sound on tiles duplicants can't walk through. See Earcons below. Off by default
- **Passability volume** -- volume for passability earcons (0-200%)
- **Footstep earcons** -- plays the game's footstep sound for the tile surface when moving the cursor. See Earcons below. On by default
- **Footstep volume** -- volume for footstep earcons (0-200%)
- **Temperature band earcons** -- plays a rising or falling tone when the cursor crosses a temperature threshold. See Earcons below. Off by default
- **Temperature band volume** -- volume for temperature band earcons (0-200%)

### Scanner settings

- **Auto-move cursor when cycling scanner entries** -- when on, the cursor teleports as you cycle scanner instances, and distances are measured from where you scanned. Off by default
- **Scanner mass readout** -- includes mass in scanner announcements: total kg for solids and liquids, average kg per tile for gases. On by default
- **Scanner direction earcons** -- plays a directional tone when cycling scanner entries, indicating the direction and distance to the target. See Earcons below. Off by default
- **Scanner direction volume** -- volume for scanner direction earcons (0-200%)

### Utility readouts

- **Utility presence earcons** -- plays a sound when the cursor lands on hidden infrastructure in the default overlay. See Earcons below. Off by default
- **Utility presence volume** -- volume for utility presence earcons (0-200%)
- **Pipe shape earcons** -- plays directional tones showing how pipes and wires connect at each tile. See Earcons below. Off by default
- **Pipe shape volume** -- volume for pipe shape earcons (0-200%)
- **Flow sonification** -- continuous tone reflecting conduit fill or circuit load. See Earcons below. Off by default
- **Flow sonification volume** -- volume for flow sonification (0-200%)
- **Flow direction readout** -- appends flow direction and wire load to conduit and power overlay readings. In conduit overlays, announces which direction contents are flowing (e.g., "Water 80% right"). In the power overlay, announces circuit load as a percentage of safe wattage. On by default

### Miscellaneous

- **Follow movement earcons** -- plays a directional tone matching the dupe or bot's movement direction during follow mode. See Earcons below. Off by default
- **Follow movement volume** -- volume for follow movement earcons (0-200%)

## Earcons

Earcons are short non-speech audio cues that convey spatial information faster than speech alone. Most are off by default (footstep earcons are the exception). All can be toggled in Settings (F12), and each has an adjustable volume slider. Multiple earcon types can be active at once; they play in sequence with short gaps between them.

### Footstep earcons

Active in all overlays. Plays the game's own footstep sound for whatever surface the cursor lands on -- metal tiles sound different from natural rock, plastic ladders from metal ones, and liquids from solids. Tiles with no walkable surface (gas, vacuum without a ladder) are silent. This gives a sense of terrain as you move without waiting for speech.

### Utility presence earcons

Active in the default overlay only. When the cursor lands on a tile that has pipes or wires running through it, a sound identifies what's there: one sound for power wire, a different sound for automation wire, and a combined sound when both are present. Pipes work the same way -- distinct sounds for liquid, gas, both, and conveyor rail. Wire and pipe sounds can play together on the same tile. This lets you find hidden infrastructure without switching overlays. 

### Pipe shape earcons

Active in utility overlays (Power, Plumbing, Ventilation, Conveyor, Automation). Plays a rapid sequence of synthesized tones that encode which directions the pipe or wire connects to. Each connection plays as a short tone: high pitch for up, low for down, mid for horizontal. Horizontal tones pan left or right in stereo to match the connection side. A straight vertical pipe plays high-then-low; a corner plays the vertical direction then the horizontal one.  Tiles with no connections are silent.

### Passability earcons

Active in all overlays. Plays a warning sound on tiles that duplicants cannot walk through -- solid tiles, walls, and other impassable obstacles. Passable tiles are silent. 

### Temperature band earcons

Active in all overlays. Plays a rising tone when the cursor moves into a warmer temperature band, and a falling tone for a cooler one. Silent when staying in the same band or moving into vacuum. The bands match the temperature skip thresholds, so you hear a tone at the same boundaries where Ctrl+Arrow would stop.

### Flow sonification

Active in the Power, Plumbing, and Ventilation overlays. Plays a continuous tone whose pitch reflects how full the conduit is or how much power the circuit is drawing. Pitch ranges from C4 (empty pipe or idle circuit) up one octave to C5 (full pipe or max safe wattage). The volume rises when the conduit is actively carrying contents and fades when empty. Moving the cursor to a tile without a conduit or wire silences the tone. This gives real-time feedback about system activity without needing to mash the I key.

### Scanner direction earcons

Active when cycling scanner entries (PageUp/Down, Alt+PageUp/Down, Home). Plays a short tone indicating the direction from the cursor to the scanner target. Uses the same pitch and pan scheme as pipe shape earcons: high pitch for up, low for down, mid for horizontal with left/right panning. Diagonal directions play a two-tone sequence -- vertical pitch first, then horizontal. Volume decreases with distance, giving a sense of how far away the target is. When the target is on the same cell, a centered horizontal tone plays.

### Follow movement earcons

Active during follow mode (Ctrl+Backslash). Plays a short tone on each tile the dupe or bot moves through. The tone encodes the movement direction using the same pitch and pan scheme as pipe shape earcons: high pitch for up, low for down, mid for horizontal. Horizontal movement pans left or right to match direction. Diagonal movement combines the vertical pitch with horizontal panning -- for example, moving up-right plays a high tone panned right. The tone plays on every movement, not just direction changes, giving continuous feedback about where the entity is heading.

## Base game hotkeys

### Management screens

These are base game hotkeys that open management screens from the colony view. They are not mod keys, so the game allows remapping them.

- **L** -- Priorities
- **F** -- Consumables
- **V** -- Vitals
- **R** -- Research
- **.** -- Schedule
- **J** -- Skills
- **E** -- Colony report
- **U** -- Database (Codex)
- **Z** -- Starmap

### Tool hotkeys

The game assigns letter keys to activate tools directly from the colony view. Since the mod activates tools through its own action menu, these hotkeys are extra but still work. **I** is overwritten by the mod (tooltip). All of these can be remapped from the game's Input Bindings options menu -- the number row is a good alternative if you want them back.

- **G** -- Dig
- **C** -- Cancel construction
- **X** -- Deconstruct
- **P** -- Prioritize
- **M** -- Mop
- **K** -- Sweep
- **I** -- Disinfect (overwritten by mod -- tooltip)
- **T** -- Attack
- **N** -- Capture / Wrangle
- **Y** -- Harvest
- **B** -- Copy building

### Overlay hotkeys

These are base game hotkeys that toggle information overlays. Each overlay changes what the tile cursor and big cursor area scan report. Pressing the same overlay key again returns to the default view. These keys can be remapped from the game's Input Bindings menu.

- **F1** -- Oxygen
- **F2** -- Power
- **F3** -- Temperature
- **F4** -- Materials
- **F5** -- Light
- **F6** -- Liquid Plumbing
- **F7** -- Gas Plumbing
- **F8** -- Decor
- **F9** -- Disease
- **F10** -- Crops
- **F11** -- Rooms
- **Shift+F1** -- Exosuit (requires Exosuit Engineering research)
- **Shift+F2** -- Automation (requires Smart Batteries research)
- **Shift+F3** -- Conveyor (requires Solid Transport research)
- **Shift+F4** -- Radiation (Spaced Out DLC only)

## Troubleshooting

If something isn't working, the player log usually has the answer. It's at `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log` on Windows or `~/Library/Logs/Klei/Oxygen Not Included/Player.log` on Mac. When reporting a bug, please include this file.

## Credits

- **aaronr7734** — His work on Rimworld Access directly inspired many of the mod's complex UI adaptations, including the schedule screen, type-ahead search, and scanner search.
- **Alexandr Epaneshnikov (alex19EP)** — Started the OniAccess project and provided much of the initial research.
- **Austin Hicks (ahicks)** — His general advice and work on Factorio Access inspired much of the mod's infrastructure. Many of the mod's core systems are lifted directly from Factorio Access.
- **Brad Renshaw (chaosbringer216)** — Helped me keep my code organized, teaching me OOP along the way. His Slay the Spire mod inspired the graph/node tree approach.
- **Keltosh_** — Provided the earcon sounds used for pipe and wire presence detection.
