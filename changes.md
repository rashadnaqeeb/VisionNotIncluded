# Changelog

## Unreleased changes since 1.6.5

* The tile cursor now announces the facing direction for built bridges and suit/checkpoint buildings, so you can confirm their orientation after placement.

## 1.6.5

* Fixed the scanner search prompt so it accepts IME-composed text such as Korean.

* Fixed Alt+Up and Alt+Down reading a packed item's whole breakdown as one line. Tooltip-backed readouts like the Vitals Endurance or Morale lines now split into one line per field, so you can step through them.

## 1.6.4

* Added Alt+Up and Alt+Down to step through the item the cursor is on one line at a time, in menus, lists, tables, and detail screens. Useful for packed readouts like the Vitals morale column or a building's status panel, where a single cell or line crams in many values.

* Added a Verbose interface setting (off by default, in Settings under Interface). When on, menus and lists announce each item's control type (button, picker, slider, toggle, radio button, submenu) and its position, like "3 of 9"; tables announce the row count as you change rows and the column count as you change columns, and tag a sortable column header as a sort button; the table or grid name itself is announced, for example "Vitals, table" or "Schedules, grid"; and switching tabs ends with the tab's position, like "tab 2 of 4".

* The tool filter now says which option is currently selected, and the build material and facade pickers mark the current choice as selected.

* Fixed the area scan not counting under-construction pipes, wires, and conduit bridges. It only looked for build orders on regular building layers, so pending utility construction was left out of the order count.

## 1.6.3

* Fixed the scanner missing build orders for wires, pipes, conduit bridges, and logic wires; it only ever found build orders for regular buildings. It now also reports pipe, wire, and tile replacement tasks.

* Fixed the scanner counting a conduit bridge as two tiles in an order cluster; bridges now count once for build, deconstruct, and replace orders.

* The new-game Shuffle seed button now announces the applied world traits after rerolling, so players no longer need to open the destination info submenu just to hear the new traits.

* The pause menu now includes the world seed/coordinate copy button, so keyboard-only and screen reader users can copy the current colony coordinates from the ESC menu.

## 1.6.2

* Fixed the DLC list on the main menu's second tab going un-arrowable after the Aquatic Planet Pack update. The game now builds the DLC logos dynamically, so the mod discovers them live and will pick up future DLCs automatically.

## 1.6.1

* Fixed the load screen announcing itself at the main menu on startup. The game quietly activates that screen in the background to check for old saves, and the mod was treating it as if you had opened it.
* The save migration prompt is now readable and operable. When the game finds old-format saves loose in your save folder, the load screen offers to move them; you can now hear how many saves are affected and pick Migrate, Continue, More Info, or Open Saves, instead of an invisible panel blocking the screen.

## 1.6.0

* Updated the mod to support game update U59-736649.

Updated the game to support the new Aquatic Planet Pack DLC.

&#x20;An audio description of the DLC's YouTube trailer can be found here, curtesy of Scene Tell, a soon to be released product bringing AI powered audio descriptions to the wider community. for more info, contact Austin Hix. the link: https://github.com/rashadnaqeeb/VisionNotIncluded/blob/main/videos/Aquatic%20Intro%20Described.m4a

* Fixed the Geothermal victory loop video descriptions never being spoken: the game names that clip in lowercase, unlike every other video, and the lookup is case sensitive.
* The dig tool now has a filter menu (F) with the game's new checkbox filters. Unlike all other filters, these are not radio buttons. You can check and uncheck each of them independently. The plants filter is the same as using the uproot tool, just brought into the dig tool for convenience.
* Fixed scanner items for large regions (gas pockets, biomes, backwall sheets) sorting far down the list even when the cursor is inside them: distance now measures to the nearest tile of a region instead of its bottom-left corner.
* Fixed molten steel, salt, carbon, uranium, syngas, sucrose, nickel, and iridium sorting under Liquids, Misc in the scanner: they now join magma and the other molten elements in the Molten subcategory.
* The pathing description when examining a duplicant now includes their swimming abilities once they have the swim skill, and the new Slogo, Seaquine, and Glo Squid movement types are described when examining those critters.
* Fixed type-ahead in the codex categories tab forgetting previous letters: each keystroke started a new single-letter search instead of refining the match, and Up/Down could not walk the results.

## 1.5.5

* Custom scanner categories can now include search keywords. Add a keyword in a category's editor and it becomes a subcategory that gathers everything whose name matches, sorted ahead of the taxonomy subcategories.

## 1.5.4

* Fix being unable to set per-robot access on a door's Access Control: the Robots category can now be drilled into to set individual robot access, not just the category default.
* Fix being unable to pick a specific resource in a storage building's filter (storage bin, bottle emptier, etc.): you can now drill into a category like Liquids to choose Clean Water or Polluted Water, even on a newly placed building where nothing is selected yet.

## 1.5.3

* Alt+H now reports the direction and distance to your home location without moving the cursor, the same way Alt plus a number key orients toward a bookmark.
* Fix custom scanner categories not sorting alphabetically: they now order by name among themselves, in both the manager and the scan cycle, so renaming one moves it to its alphabetical place.

## 1.5.2

* Renamed the mod to Vision Not Included.

## 1.5.1

* Fix the Apply button being unreachable in graphics settings: it now appears in the list as soon as a setting change enables it, so you can apply your changes.

## 1.5.0

* Fix Codex Categories type-ahead landing on the first section of an article instead of the section you searched for.
* Duplicants now report the practical suit they are wearing (atmo, jet, lead, or oxygen mask) when you cycle to them or arrow over them, and while following a dupe with the camera you hear when they put one on or take it off.
* Added custom scanner categories: bundle the taxonomy filters you use most into your own named categories that appear first when cycling scanner categories. Create and edit them from "Custom scanner categories" in the scanner settings; they are saved globally and follow you to every colony.

## 1.4.7

* Updated Prism speech library to v0.16.1

## 1.4.6

* Updated Prism speech library to v0.15.0

## 1.4.5

* The Select Module side screen (rocket platform module construction) now reads the reasons a module is not buildable, matching the tooltip the game shows sighted players. Reasons are also announced when pressing Build while the button is disabled.

## 1.4.4

* Type-ahead search now accepts IME-composed input, so players using a Chinese, Japanese, or Korean IME can type CJK characters to filter lists.
* Fix a crash that could happen after using copy-building (PlanScreen's Copy hotkey) when the game's copy didn't actually enter build mode. The mod was previously pushing a build handler in this case, then crashing on the first rectangle selection because no build tool was active.

## 1.4.3

* Fix the game crashing when opening the material picker for a multi-ingredient building (e.g. Gas Filter) before each ingredient slot had a default material chosen.

## 1.4.2

* Shift+Home (scanner orient) now appends the current item's name after the direction, so you can confirm what you're getting the bearing for.

## 1.4.1

* Fix fast travel jump silently failing when the menu was opened from build or tool mode. The cursor now teleports to the selected point regardless of which handler is on top.
* Add a Relocate action to the fast travel menu (Shift+V), between Rename and Delete, that moves an existing point to the cursor's current location.

## 1.4.0

* Add a fast travel menu (Shift+V) for naming and jumping to cursor positions on the active world. Each entry stores a name and grid coordinates; right-arrow into an entry to rename or delete it. Bookmarks are scoped to the colony and the asteroid, and live in a YAML file alongside the save so the .sav itself stays unchanged.

## 1.3.9

* Fix sandbox parameter sliders (mass, temperature, etc.) so the value you set is actually applied. Adjustments now persist to the sandbox settings, so the brush paints with your chosen values instead of the defaults.
* The OS mouse pointer now follows the focused widget across menus and side screens, so a sighted observer can see where the blind player is focused. Widgets inside scroll containers are brought into view before the pointer is moved.

## 1.3.8

* Text edit fields now speak as you navigate: arrow keys announce the character to the right of the caret, Ctrl+arrows announce the next word, Home/End jump and announce, Up/Down re-read the full contents, and Backspace/Delete announce what was removed.
* Text edit fields now support selection: Shift+arrows, Ctrl+Shift+arrows, Shift+Home/End, and Ctrl+A announce what was selected or unselected. Ctrl+C copies the current selection (or the full text when nothing is selected).

## 1.3.7

* Activating a notification or a Vitals row now moves the cursor to the focused target, so reading the cursor announces the POI instead of your previous location.
* Type-ahead search now accepts space-delimited abbreviations: typing "ga pi" matches "Gas Pipe", "o n i" matches "Oxygen Not Included", etc. Each token must prefix a distinct word, in order. Existing matches are unchanged and always outrank abbreviation matches.
* Type-ahead search now ranks shorter names first across all match tiers, with match position as the tiebreaker (e.g. searching "wood" puts "Pinewood" before "Oakwood Shelf")
* Fix build rect mode so pressing Enter after setting one corner completes the rectangle at the cursor instead of speaking "no valid cells"
* Add Hijacked Headquarters "Choose Blueprint" catalog screen: browse printable critters and plants as a searchable tree, then flip to a details tab to hear description, cost, and trigger a Print.
* Trapped duplicants are now announced immediately when they become trapped, even if you're viewing a different world. A follow-up message confirms when they're no longer trapped.

## 1.3.6

* Fix valve side screen reading flow rate as a percentage instead of grams per second

## 1.3.5

* Add Spice Grinder recipe selection screen with dropdown cycling and description readout
* Fix unable to drill into categories on side screens like the pedestal
* Fix cursor unable to reach map edges at high zoom levels

## 1.3.4

* Updated Prism speech library to v0.11.3
* Fix Alt+Left/Right not navigating history in the Codex content tab
* Help screen now shows keys for the active tab instead of a fixed list on all tabbed screens (Codex, Research, Skills, Schedule, Starmap, Inventory, Outfits)
* Pressing Escape or Backspace while type-ahead search is active now clears the search first instead of closing the screen or navigating history

## 1.3.3

* Properties tab now shows sky visibility for telescopes, space scanners, and mission control stations -- lists which columns are blocked so you can find and dig out obstructions
* Oil Well Caps (and other attachable buildings) now appear in the tile cursor, scanner order detection, and area scanner -- previously invisible because they use a different object layer than normal buildings

## 1.3.2

* Type-ahead navigation in storage filter side screens (conveyor loader, storage bins, storage tile item picker, and element/liquid/gas/logic filters) no longer silently hides items from the list -- previously, the letters you typed to jump to an item would also land in a hidden search box, leaving rows filtered out until you reloaded the save
* Scanner now finds oil reservoirs under Geysers > Liquid
* Sandbox Reveal tool now works on unexplored tiles -- set corners and confirm over fog to clear it

## 1.3.1

* Tab/Shift+Tab in table screens (priorities, consumables, vitals) cycles through worlds to filter duplicants by colony
* Cursor skip now jumps to the map edge instead of stopping in place when no change is found
* Colony status (S) now starts with the world name
* Cycle status (Q) now says block before cycle
* Dig tool now warns when a tile requires a dig skill no duplicant has
* Updated Prism speech library from v0.9.0 to v0.11.1

## 1.3.0

* Read coordinates moved from K to Shift+K; bare K now activates the game's sweep tool
* Critter descriptions now appear on the Properties tab -- these describe what each critter looks like (shape, color, features, size)
* Duplicant details screen now includes a work reach explanation covering vertical and horizontal range, diagonal reach for digging, and storage reach
* Storage tile item selection side screen is now accessible -- browse and select which item to store
* Plant and seed names no longer say "(Original)" once mutations are unlocked
* Duplicant physical appearance descriptions now appear in colony start, printing pod, supply closet duplicant list, and the Bio tab -- these describe what each dupe looks like (skin, hair, clothing, expression, posture)
* Tools now preserve overlay context -- activating dig, sweep, or other tools on a temperature or conduit overlay keeps that overlay's information in speech
* Tile cursor now announces buildings queued for replacement (e.g. placing an insulated tile over a regular tile)
* Supply Closet: wardrobe now includes a "None" option to clear a duplicant's outfit back to default
* Supply Closet: overjoyed response designer is now accessible -- browse balloon artist facades, select and apply them
* Supply Closet: Duplicants screen is now accessible -- browse personalities, cycle outfit types, view outfit details, and open the editor or wardrobe

## 1.2.2

* Wardrobe and designer: clothing items now include their descriptions in speech output
* Wardrobe and designer: empty slots now say the default item name (e.g. "Default Gloves") instead of "empty"
* Wardrobe type filter now says "Type: Clothing" instead of the tooltip sentence
* Designer Save/Copy buttons now read their correct labels
* Supply Closet wardrobe: browse, edit, rename, and delete outfits across Clothing, Atmo Suit, and Jet Suit types
* Supply Closet outfit designer: create and modify outfits by selecting items for each slot
* Research screen search now matches TechItem names and descriptions, so typing "electrolyzer" or "breathable" finds the right tech
* Supply Closet item reveals now announce immediately when the server confirms, without waiting for animations to finish
* Plant branches (Arbor Tree, Bonbon Tree, etc.) now say "branch" on the tile cursor to distinguish them from the trunk

## 1.2.1

* Ctrl+Delete cancels all orders on the current map while the cancel tool is active, respecting the active filter

## 1.2.0

* Table screens (consumables, priorities, vitals) now start the cursor on the active world's duplicants
* Space POI details now show mass remaining and element composition percentages
* Starmap no longer announces, selects, or scans empty harvestable resource containers on hex cells
* Starmap scanner now finds non-empty harvestable resource containers under POIs
* Entity details now show harvestable resource container contents when items are present
* Entity details now show pathing behaviour for duplicants and critters, describing how they navigate the world
* Re-added Tolk override support: drop tolk\_override.dll and companion DLLs into the data folder to use Tolk instead of Prism
* Building details now show range coordinates for buildings like Auto-Sweeper, Robo-Miner, and Meteor Blaster
* Dupe navigator now announces work progress percentage when cycling dupes
* Rocket module selection now reads module descriptions and effects
* Material selection in the rocket module screen is now a navigable list instead of a slider

## 1.1.4

* Fix biome names showing English in non-English games
* Deconstruction orders now announce the building name, matching construction orders

## 1.1.3

ported the mod to the PRISM speech library, adding support for ZDSR, as well as cross-platform support for Mac and Linux.

* Fix crash when pressing Space or Enter in copy settings mode after opening the build menu
* Deconstruction orders on wires, pipes, and other conduits are now announced in their respective overlays and in the scanner

## 1.1.2

* Config settings and tolk\_override.dll now survive Steam Workshop updates (moved to the Klei save directory)

## 1.1.1

* Fix rotation direction for 2x2 and larger automation gates (AND, OR, XOR, Multiplexer, Demultiplexer) — now matches output direction like bridges
* Fix Shift+P port layout not working for automation gates (AND, OR, NOT, XOR, BUFFER, FILTER, Multiplexer, Demultiplexer)
* Fix some mod-authored labels (Difficulty, Interest, Colony name, and others) reading in English instead of the translated language
* Transit tube segments now announce their connection shape (vertical, corner, tee junction, etc.)
* Unconnected transit tube entrance and crossing connection points are announced when the cursor is on them

## 1.1

* Scanner direction earcons: optional tones indicate distance and direction when cycling scanner results (off by default)
* Fix config not persisting toggle options set to false
* Fix radbolt joint plate reporting wrong orientation
* Strip degree unit letter from temperature speech so screen readers no longer say "C" or "F" after every value
* Per-earcon volume sliders in the config menu
* Wires now speak circuit load percentage when flow direction readout is enabled
* Power overlay sonification pitch now rises above the safe-load threshold when a circuit is overloaded
* Flow direction readout now includes the element name (e.g. "water, 80% right 15% up")
* Fix wires, pipes, and backwall buildings not announced as "constructing" when under construction
* Temperature overlay now speaks building and debris temperature after the name, skipped when within 1 degree of the cell temperature
* Fix radbolt output port announced as "{0}" instead of direction when previewing building ports
* Footstep earcons: moving the cursor plays the game's footstep sound for the tile surface, enabled by default (toggle in config)
* Suppress UI hover sounds triggered by camera movement when footstep earcons are on
* Disable the game's pause audio snapshot so all sounds play clearly while paused

## 1.0.6

* Fix path check reporting inconsistent nearest reachable cells when moving the cursor
* Alt+arrows now skips coarsely, grouping all floor tiles, all ladders, all plants, all decorations, all liquids, and all natural solids as single zones
* Status and chore changes are now announced when using the game's Follow Cam on any entity (critters, buildings, etc.)
* Bot navigation now finds all five bot types including Sweepies and Remote Workers, with status announcements for each
* Fix tile cursor jumping to the printing pod during timelapse screenshots
* Fix crash when toggling the mod off and back on during duplicant selection or other screens
* Fix germ overlay skip and big cursor area scan not detecting germs in building storage or on conveyor rails

## 1.0.5

* Tools: Ctrl+G toggles single-cell selection mode (Space selects one cell at a time instead of rectangle corners)
* Tools: Shift+Space now removes a single cell from the selection instead of the entire rectangle
* Tile cursor germ readout now includes germs on items inside building storage (e.g. germy water in a reservoir) and items on conveyor rails

## 1.0.4

* Follow mode now plays a directional tone matching the dupe or bot's movement direction. Togglable in F12 settings
* Scanner announcements now include mass: total kg for solids/liquids, average kg per tile for gases. Togglable in F12 settings
* Priority screen: Ctrl+R resets all priorities from anywhere in the table
* Copy settings now announces "settings not applied" when copying plant settings to a farm tile that can't accept them (e.g. already has a different plant, or placement is blocked)
* Tile cursor coordinate origin (0,0) is now the ground tile below the Printing Pod, not the building itself
* Fix "MISSING" being spoken after story trait popup text and buttons
* Tile cursor now announces buried objects inside solid tiles
* Fix boundary sound not playing reliably at map edges, especially on rockets and side edges
* Fix crash in disinfect threshold settings when the timelapse screenshot fires while the panel is open
* Earcons no longer play on unexplored tiles; moving into fog of war silences all earcons and resets temperature tracking

## 1.0.3

* Support for tolk\_override.dll: drop in a custom Tolk replacement to use additional screen reader drivers
* Codex articles collapse sub-bullets onto their parent line, skip markup-only fragments, and improve content tab readability
* Shape earcons now include bridge connections; fixed down-corner segment order; reduced flow sonifier volume
* Recipe queue screen now includes a continuous toggle to set or clear infinite production
* Direction toggle buttons (e.g. wash basin) now announce the current direction before the toggle action

## 1.0.2

* Bot navigator: Shift+\[ / Shift+] cycles through all autonomous bots (Sweepy, Flydo, Rover, Biobot, Remote Worker) on the current world. Jump, follow, and pathability check (, Ctrl+, Shift+) work on whichever entity type was last cycled to
* Codex effects for multi-converter buildings (e.g. Desalinator) now group each converter's outputs with its input instead of listing duplicates
* Build tool now reads existing conduits when placing bridges (liquid, gas, solid, wire, and automation)
* Scanner now finds POI and story buildings: Thermo-Nullifier, Teleportal Pad, Hijacked Headquarters, warp conduits, fossil dig sites, and other Gravitas structures
* Audio descriptions for tutorial videos: timed narration of visual content queues to the screen reader as the video plays (digging tutorial)
* Temperature band earcons: plays a rising or falling tone when the cursor crosses a temperature band boundary. Toggle in F12 settings (off by default)
* Flow sonification now works on wires in the power overlay — pitch tracks circuit load
* Shape earcons: when enabled, plays tonal sequences to convey pipe, wire, and rail connection shapes in overlay views instead of speaking them. Toggle in F12 settings (off by default)
* Flow sonification: fix buzzing artifacts from loop seam and abrupt volume changes; reduce volume
* Flow sonification: a continuous tone plays when the cursor is on a pipe in the liquid or gas overlay. Pitch maps fill level (C4 empty to C5 full), volume reflects how often fluid is present. Toggle in F12 settings (off by default)
* Pixel pack announces the signal state and color of each pixel at the cursor
* Duplicants no longer appear in scanner snapshots (still findable via Ctrl+F search)
* Enter now completes the rectangle when a first corner is set, instead of canceling

## 1.0.1

* Recipe screen ingredient slots now show how many other material options are available
* Cluster selector on the new game screen now explains left/right and enter controls
* Fix assigned amenities list showing absolute world coordinates instead of coordinates relative to the printing pod
* Fix germ skip skipping past contaminated cells when germs were on buildings, items, or pipe contents rather than the tile surface
* Codex articles split multi-line text blocks into separate cursor items so each property gets its own line
* Codex articles no longer repeat redundant names in conversion and recipe panels
* Pending orders now announce blockers: "needs skill" for dig orders requiring an unskilled colony, "can't store" for sweep orders with no accepting storage
* Audio earcons play when moving the tile cursor: impassable tiles get a distinct sound, and wires, pipes, and rails each have unique tones in the default overlay (toggle each set in F12 settings)
* Press A on any tile to hear temperature, room type, germs, radiation, light, decor, and biome at once
* Biome name announced when crossing into a different biome during tile-by-tile movement (toggle in F12 settings)
* Radbolt output ports now announce their direction (e.g. "radbolt output, right") in the radiation overlay
* Fix: wire bridges, joint plates, and power transformers no longer falsely announce "placed" when placement is invalid
* Automation wires now announce their signal color (green/red) after connection directions; ribbons announce all 4 bits
* F12 settings: "Lock zoom level" option lets you keep your current zoom when moving the tile cursor (on by default)
* F12 opens a settings screen where you can view and adjust all config options (coordinate mode, auto-move cursor) in one place

## Pre-release

* Fix: wire bridge and joint plate connection points now announce "connection port" in the power overlay, so players can find where to attach wires
* Fix: wire shapes next to joint plates now correctly include the joint plate connection direction
* Fix: build tool now allows placing replacement tiles (e.g. sandstone ladder over wood ladder) instead of rejecting as obstructed
* Build tool: port layout hotkey changed from P to Shift+P, freeing bare P for the game's Prioritize tool
* Story trait popups (discovery and completion) and gameplay event popups (meteor showers, food fights, etc.) are now spoken with title, description, and action buttons
* Ctrl+\\ follows the current duplicant with the camera, announcing status and chore changes in real time; \[ / ] switches the follow target; any cursor movement stops following
* Dupe navigator (\[ / ]) now announces position relative to cursor (e.g. "3 up 2 left") and speaks statuses before the current chore
* Database: lore categories (emails, journals, research notes, personal logs, investigations, notices) now appear in the categories tab alongside game-data categories
* Fix: type-ahead search in the Database no longer causes a crash
* Database: critter morphs and other sub-entries are now navigable via Right arrow from the parent entry, and appear in type-ahead search
* Shift+G opens disinfect threshold settings when germ overlay is active (toggle auto-disinfect, adjust threshold via slider or direct number entry)
* Cycling dupes with \[ / ] now announces "trapped" after the dupe's name when they can't pathfind to other dupes, the telepad, or their bed
* Farm tiles now announce plant extent and blocked status (e.g. "extends 2 up, blocked") when selecting a multi-cell plant
* Pipe bridges, logic gates, and wire bridges now place the cursor on the input end and announce the output direction (e.g. "right" means flow goes right)
* Fix: logic gate ports (NOT, AND, OR, XOR, buffer, filter, multiplexer, demultiplexer) now announce port names in the automation overlay
* Conduits and wires announce shapes instead of raw directions (e.g. "vertical", "up right corner", "right tee junction" instead of "connects up, down")
* Details screen navigation is now stable when the game reorders widgets between keypresses (errands, storage, and other tabs no longer jump to unexpected items)
* Material picker now announces material effects (overheat temperature modifier, thermal conductivity, etc.) alongside name and quantity
* Fix: audio rulers no longer disappear when opening and closing menus
* Translation: replaced concatenation patterns with format strings so translators can reorder words (biome names, bottled/loose labels, colony inventory, starmap research, diet restrictions, material alternatives)
* Fix: heavy-watt joint plates now announce "horizontal" and "vertical" when rotating instead of meaningless direction names
* Sandbox mode: all 12 sandbox tools are accessible. Toggle sandbox with Shift+S, pick tools from the Sandbox Tools category in the action menu (B key). Rectangle selection for brush tools (Space for corners, Enter to apply), single-cell apply for flood/spawner/story tools, Ctrl+Space to sample, F to open the parameter menu with sliders and selectors
* Fix: bare G key was blocked during build placement even after the rectangle mode toggle moved to Ctrl+G
* Rectangle build mode: press Ctrl+G while placing a 1x1 building (tile, drywall, etc.) to define rectangular areas with Space, then Enter to fill them all at once
* Alt+Arrow skips by building, tile type, or element regardless of the active overlay
* Fix: vacuum and void tiles no longer report "0 g" mass
* Fix: cluster map cursor no longer resets to the active world when returning from a details screen or entity picker
* Cluster map: H key jumps cursor to the active world's hex location
* Fix: rocket module side screen now speaks the module name and description instead of silence
* Module flight utility side screen: duplicant dropdown is now cyclable with left/right arrows
* Module flight utility side screen: duplicate module names are numbered (e.g. "Cargo Bay 1", "Cargo Bay 2")
* Rocket restriction side screen now speaks the automation tooltip when restrictions are controlled by a logic wire
* Pilot and crew side screen now announces when a robo-pilot copilot is active alongside a dupe pilot
* Fix: rocket landing pad dropdown no longer speaks the pad name twice, removes redundant label, and excludes invalid pads with reasons
* Side screen dropdown options (e.g. door access, rocket restrictions) now speak their tooltip when selected
* Disabled buttons and toggles are now navigable and announce "unavailable". Pressing Enter on them plays a negative sound instead of silently failing
* Announce "Saved" after the game finishes saving (autosave or manual)
* H key now jumps to the Rocket Control Station when inside a rocket interior. Coordinates are relative to the control station (0,0)
* Build tool cursor now represents the bottom-left corner of the building. Extent announcements use "right" and "up" only (e.g. "extends 2 right, 1 up"), and port offsets are relative to the corner you're standing on
* P key in build tool announces all ports with their offset from the cursor (e.g. "liquid input, here, liquid output, 1 up. power input, 1 right."). Offsets update when the building is rotated
* Shift+R in build tool rotates the building backward (counterclockwise for 4-way rotation)
* Geysers are now a top-level scanner category (after Zones) with subcategories: Gas, Liquid, Molten, Geothermal
* Buried geysers no longer appear in the tile cursor or scanner until exposed by digging
* Backspace returns to pre-jump position after both \\ dupe jump and Home scanner teleport
* Shift+\\ checks if the current dupe can reach the cursor tile, reporting cost if reachable or the nearest reachable tile if not
* Demolior (DLC4 large impactor): spoken announcements for discovery, impact, destruction, and damage milestones; S key reports Demolior health and time remaining; activating the Demolior notification focuses the camera on the impact zone
* Fix: spurious diagnostic, resource, and world announcements during save load are now suppressed
* Fix: notification submenu now shows message titles instead of internal UI object names and coordinates
* Activating an achievement notification announces "opening achievements" and each unlocked achievement name
* Fix: deconstruct tool now works on Gravitas buildings and other demolishable structures (Gene Shuffler, Cryo Tank, etc.)
* Gravitas ruins (doors, walls, desks, and other props) now appear in the scanner under Buildings > Gravitas
* Tile cursor now reports deconstruction orders on backwalls
* Fix: navigating recipes or side screen items no longer crashes when the game refreshes widgets between actions
* Fix: foundation tiles no longer double-announce (element was spoken on top of the building)
* Cluster map scanner now supports Ctrl+F search to filter entities by name
* Cluster map: hidden and peeked hexes now announce "unexplored" or "unseen" instead of verbose tooltip text
* Cluster map (Spaced Out DLC starmap): full keyboard navigation with hex cursor, entity selection, scanner, pathfinder, and coordinate reading
* W opens a navigable world list to switch between asteroids and rockets (Spaced Out DLC)
* Switching worlds now announces the destination world name, whether via the world list, number hotkeys, or starmap
* Off-screen world diagnostic degradation is announced with the world name and status
* Newly discovered worlds are announced by name
* Fixed rotation direction for horizontal-flow buildings (gas filters, conduit bridges, logic gates, etc.): "facing left" now correctly indicates the input side at Neutral orientation
* Fix: placing a building, canceling it with Shift+Space, then placing again on the same tile now works correctly
* Backspace returns to your previous position after a scanner teleport (Home key)
* Shift+Home announces distance to the current scanner item; auto-move toggle moved to Shift+End
* Shift+Up/Down adjusts a single cell's priority in the priorities table, complementing Ctrl+Up/Down which adjusts the entire column
* Numpad number keys now work everywhere the main keyboard number keys do: priority setting, schedule brush selection, and cursor bookmarks
* D reads current diagnostic alerts (breathability, food, stress, etc.) sorted by severity
* Shift+D opens a full diagnostics browser with pin state cycling and criteria toggles
* Diagnostic alerts are now automatically announced when conditions worsen
* Liquids pooling on buildings are now spoken instead of being suppressed with the gas element
* Type-ahead search now supports spaces for multi-word queries (e.g., "blue c" to find "Blue Cheese"). Space only enters the search buffer after typing at least one letter, so handler actions like pin toggle still work normally
* Encyclopedia type-ahead now includes top-level categories in search results, listed after article matches
* Type-ahead search now sorts matches by position within each tier, so "washroom" ranks before "fried mushroom" when searching "room"
* Materials overlay area scan now reports only element percentages, without median mass
* Ctrl+Shift+Down resets big cursor to 1x1 instantly
* Fixed cursor skip stopping on random unexplored tiles instead of treating the entire fog-of-war region as one block
* Fixed opening the starmap (Z) or other non-tool screens while in build or tool mode causing errors
* Big cursor: Shift+Up/Down cycles cursor size (1x1, 3x3, 5x5, 9x9, 21x21). Arrow keys move by the full cursor width, and landing speaks an area scan summary instead of a single tile
* Area scan adapts to the active overlay: element breakdown for Materials, average temperature/lux/decor/rads for their overlays, O2/PO2 percentages for Oxygen, germ averages for Disease, room types for Rooms, plant growth for Crops
* With a big cursor active, Space in tool mode sets both corners at once, creating a rectangle the size of the cursor area
* Clear rectangle in tool mode changed from Delete to Shift+Space, matching build tool mode
* Help key changed from F12 to ? (Shift+/) to avoid conflict with Steam Screenshot
* Extension cells below Pitcher Pump and Water Trap now announce as "intake pipe" or "lure" instead of repeating the building name
* Utility ports now use descriptive labels: filters say "filtered gas output" instead of "gas output 1", overflow valves say "overflow output", preferential flow valves say "priority input"
* Gas, liquid, and solid filter side screens now present a flat alphabetical list of elements instead of confusing folded categories
* Door access control now says "passing from right to left" instead of just "left" to clarify the direction of travel
* Action buttons now include their tooltip in speech (e.g. wash basin direction buttons explain the travel direction)
* Pressing backslash on the details screen activates copy settings for the selected building, with an error message if the entity has no copyable settings
* Fixed switching tools (e.g. pressing X for deconstruct while in build mode) not activating the new tool's handler
* Tile cursor help (F12) now lists the 10 base game management screen hotkeys (Priorities, Consumables, Vitals, etc.)
* Build menu now announces material costs inline (e.g. "copper 400 kg") so you can compare buildings without opening the info screen
* Fixed dig tool announcing gas or liquid element behind buildings
* Fixed dig tool announcing "N/A" hardness for gas and liquid elements
* Fixed details screen speaking extraneous periods in Category line (e.g., "Category:. . Toilet" now reads "Category:. Toilet")
* Tile cursor now announces "unreachable" for dig, mop, and sweep orders that no duplicant can reach
* Fixed utility placement (wires, pipes) rejecting cells with existing utilities, so you can now start drags on existing lines and drag over them to reconnect
* Fixed copy building key (B) announcing "tool tool" instead of the building name
* Place tool (cargo lander placement) is now accessible: navigate with tile cursor, Space/Enter to confirm, Escape to cancel
* Copy settings tool is now accessible: Space to apply, Enter to apply and exit
* README now lists base game tool hotkeys (noting I and K are overwritten) and management screen hotkeys

