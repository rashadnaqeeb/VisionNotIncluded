# OniAccess

An accessibility mod for Oxygen Not Included that makes the game playable for blind users. All game information is delivered through speech output via your screen reader (using the Prism library). The mod doesn't change game behavior, only adds speech.

## System requirements

- Windows
- Oxygen Not Included on Steam
- A screen reader such as NVDA or JAWS. If none is running, the mod falls back to SAPI (the Windows built-in speech engine).

## Install (Steam Workshop)

1. Subscribe to the mod on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3683507975).
2. Disable two Steam hotkeys that interfere with the mod:
   - Open Steam > Settings > In-Game.
   - Uncheck "Enable the Steam Overlay while in-game".
   - Clear or change the "Screenshot shortcut key".
3. Launch the game once, then close it. This lets the game discover the mod.
4. Download and run [EnableMod.exe](https://github.com/rashadnaqeeb/OniAccess/raw/main/EnableMod.exe). It enables the mod in the game's config so you don't have to navigate the mod manager, and offers to launch the game. If the mod ever crashes and the game disables it, run it again to re-enable.
5. Launch the game. The mod will announce when it's ready.

Updates are delivered automatically through the Steam Workshop.

## Install (manual, without Steam Workshop)

Use the Steam Workshop install above if at all possible. It handles updates automatically and is the primary distribution method. This manual method is only for players in regions where the Steam Workshop is unavailable.

1. Download the latest release zip from the [GitHub releases page](https://github.com/rashadnaqeeb/OniAccess/releases).
2. Extract the zip into your local mods folder: `Documents\Klei\OxygenNotIncluded\mods\local\OniAccess\`. Create the folder if it doesn't exist. The folder should contain `OniAccess.dll`, `mod.yaml`, `mod_info.yaml`, and the `native`, `translations`, and `audio` folders directly -- not nested inside an extra subfolder.
3. Disable two Steam hotkeys that interfere with the mod:
   - Open Steam > Settings > In-Game.
   - Uncheck "Enable the Steam Overlay while in-game".
   - Clear or change the "Screenshot shortcut key".
4. Download and run [EnableMod.exe](https://github.com/rashadnaqeeb/OniAccess/raw/main/EnableMod.exe). It enables the mod in the game's config so you don't have to navigate the mod manager, and offers to launch the game. If the mod ever crashes and the game disables it, run it again to re-enable.
5. Launch the game. The mod will announce when it's ready.

To update, download the new release zip and extract it into the same folder, overwriting the existing files.

## About the game

Oxygen Not Included is a colony survival sim. You manage a group of duplicants (dupes) stranded inside an asteroid. They need oxygen, food, water, and a place to sleep. You don't control them directly -- you build infrastructure and set priorities, and they carry out the work autonomously. If you stop building, they stop progressing. If you forget oxygen or food, they die.

The world is a 2D side-view grid. This means that you move up, down, right, and left, rather than in cardinal directions. Each tile contains an element (granite, dirt, oxygen, water, etc.) and may have buildings, creatures, or debris on it.

There is a [Getting Started guide on Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=1359110726) written for sighted players. It contains a lot of screenshots, but the text is detailed enough to be reasonably followable.

The game has a built-in Database (U) covering every building, creature, element, geyser, disease, and game mechanic. It also contains tutorials that explain core concepts like power, plumbing, and morale. Some tutorials link to short audio described videos. New tutorials unlock as you play and show up in your notifications (Shift+N).

## Getting started

**?** (Shift+/) is context-sensitive help. Press it on any screen to see every key available in that context. This is the fastest way to learn the mod.

### Navigation

Arrow keys move the tile cursor one tile at a time. Each tile announces what's there: buildings, element and mass, entities, orders, debris. Enter opens a menu with all items present on a tile. Enter again on any of them opens a details screen for that item. This screen is very information dense, and is split into three sections. Navigate between sections with Ctrl+Tab, and within sections with Tab.

### Building

Tab opens the action menu. Browse categories of buildings with Up/Down, select a building, then use the tile cursor to decide where it should go. When ready to place, hit Space. R rotates the building. Enter places and immediately exits, saving a keypress if you're only placing a single building.

### Tools

Tools like Dig, Deconstruct, and Mop let you give orders on the map. You'll find them in the action menu under the Tools category. With a tool active, move the cursor to one corner of the area you want to affect, press Space, move to the opposite corner, and press Space again to mark the rectangle. Enter confirms the order. You can also just press Enter without setting corners to apply the tool to a single tile. Escape cancels the tool and returns to the map.

### Overlays

The game has overlays -- filtered views that focus on one system. F2 shows power networks, F3 shows temperature, F6 shows plumbing, and so on. For sighted players, this highlights particular pieces of information that are otherwise not visible or difficult to see in the default view. The mod works with this system to avoid overwhelming you with information. When you switch overlays, the mod changes what it tells you about each tile. For example, in the temperature overlay, every tile announces its temperature. In the power overlay, wires and their connection points are read out. Pressing escape  returns to the default view.

### Colony status

While on the map, S reads a summary of your colony: how many dupes you have, food supply, stress levels. Q reads the current cycle (day) number. D reads diagnostic alerts if anything is going wrong. The diagnostics can be customised with Shift+D, where you can decide what's worth your attention.

### Scanner

End scans your entire asteroid and catalogs everything: elements, buildings, creatures, debris. Results are organized into a four-level hierarchy. To find something, say the second patch of Iron Ore:

1. Ctrl+PageUp/Down to reach the Solids category
2. Shift+PageUp/Down to reach the Metals subcategory
3. PageUp/Down to reach Iron Ore
4. Alt+PageUp/Down to cycle through individual patches, sorted by distance

Home teleports the cursor to whichever patch you've selected. Ctrl+F searches by name if you already know what you're looking for. The scan is not live-updated -- press End again to refresh.

### Duplicants

[ and ] cycle through your dupes, announcing their name, what they're doing, and any critical problems. Backslash jumps the cursor to the current dupe's location.

### Notifications

The mod announces new alerts automatically. Shift+N opens the notification menu where you can review and act on them. This doesn't include diagnostic alerts, which are things like you're running out of food etc. For those, see D and Shift+D.

### The ? key

Every screen has different controls. The action menu, details screens, research screen, schedule screen -- they all have their own keys. Rather than memorizing everything up front, press ? whenever you're on a new screen. It will list every key that works in that context.

The game has dedicated screens for managing things like research, duplicant priorities, schedules, and skills. Each has a single-key hotkey from the colony view (e.g. R for Research, L for Priorities). These hotkeys and the full feature reference are in the [README](README.md).
