namespace STRINGS {
	// Translation notes:
	// - These strings are spoken by a screen reader, not displayed visually.
	// - Game terms (Duplicant, Cycle, Block, Morale, Decor, Errand, Chore)
	//   must match the base game's official translation for the target language.
	// - Consult the game's .po translation files for canonical term translations.
	public class ONIACCESS {
		// Spoken descriptions of sprite/icon overlays
		public class SPRITES {
			// Replacement text for warning icon sprites in game text. Followed by the warning detail
			public static LocString WARNING = "warning:";
			// Automation wire signal states
			public static LocString LOGIC_GREEN = "green signal";
			public static LocString LOGIC_RED = "red signal";
		}

		// System-level speech announcements
		public class SPEECH {
			// {0} = mod version string (e.g. "1.2.3")
			public static LocString MOD_LOADED = "Oni-Access version {0} loaded";
			public static LocString MOD_ON = "Oni-Access on";
			public static LocString MOD_OFF = "Oni-Access off";
			public static LocString NO_COMMANDS = "No commands available in this context";
			// {0} = screen handler display name (e.g. "Key Bindings", "Minion Select")
			public static LocString HANDLER_FAILED = "Error, {0} failed";
		}

		// Type-ahead search feedback
		public class SEARCH {
			public static LocString CLEARED = "Search cleared";
			// {0} = the user's search query text
			public static LocString NO_MATCH = "No match for {0}";
		}

		// Hotkey action descriptions shown in key binding UI
		public class HOTKEYS {
			public static LocString TOGGLE_MOD = "Toggle Oni-Access on/off";
		}

		// Screen handler display names, announced when a screen opens
		public class HANDLERS {
			public static LocString LOADING = "Loading";
			public static LocString HELP = "Help";
			public static LocString MAIN_MENU = "Main menu";
			public static LocString PAUSE_MENU = "Pause menu";
			public static LocString AUDIO_OPTIONS = "Audio options";
			public static LocString GRAPHICS_OPTIONS = "Graphics options";
			public static LocString GAME_OPTIONS = "Game options";
			public static LocString COLONY_SUMMARY = "Colony summary";
			public static LocString WORLD_GEN = "Generating world";
			public static LocString MINION_SELECT = "Select duplicants";
			public static LocString SAVE_LOAD = "Save and load";
			public static LocString MODS = "Mods";
			public static LocString TRANSLATIONS = "Translations";
			public static LocString DATA_OPTIONS = "Data options";
			public static LocString FEEDBACK = "Feedback";
			public static LocString KEY_BINDINGS = "Key bindings";
			public static LocString SUPPLY_CLOSET = "Supply closet";
			public static LocString INVENTORY = "Inventory";
			public static LocString WARDROBE = "Wardrobe";
			public static LocString OUTFIT_DESIGNER = "Outfit designer";
			public static LocString ITEM_DROP = "Claim blueprints";
			public static LocString WELCOME_MESSAGE = "Welcome message";
			public static LocString STORY_MESSAGE = "Story message";
			public static LocString EVENT_INFO = "Event";
			public static LocString VIDEO = "Video";
			public static LocString COLONY_VIEW = "Colony View";
			public static LocString ENTITY_PICKER = "object selection";
			public static LocString DETAILS_SCREEN = "Entity details";
			public static LocString PRINTING_POD = "Printing pod";
			public static LocString ERROR_SCREEN = "Error";
			public static LocString DIAGNOSTICS = "Diagnostics";
			public static LocString WORLD_SELECTOR = "World list";
			public static LocString DISINFECT_SETTINGS = "disinfect settings";
			public static LocString CONFIG = "Settings";
		}

		// Config screen option labels
		public class CONFIG {
		public static LocString SECTION_TILE_CURSOR = "Tile Cursor Settings";
		public static LocString SECTION_SCANNER = "Scanner Settings";
		public static LocString SECTION_UTILITY = "Utility Readouts";
		public static LocString SECTION_MISC = "Miscellaneous";
			public static LocString COORDINATE_MODE = "Tile cursor coordinate mode";
			public static LocString AUTO_MOVE_CURSOR = "Auto-move cursor when cycling scanner entries";
			public static LocString SCANNER_MASS_READOUT = "Scanner mass readout";
			public static LocString LOCK_ZOOM = "Lock zoom level when moving tile cursor";
			public static LocString UTILITY_PRESENCE_EARCONS = "Utility presence earcons";
			public static LocString PIPE_SHAPE_EARCONS = "Pipe shape earcons";
			public static LocString PASSABILITY_EARCONS = "Passability earcons";
			public static LocString ANNOUNCE_BIOME_CHANGES = "Announce biome changes";
			public static LocString FLOW_SONIFICATION = "Flow sonification";
			public static LocString TEMPERATURE_BAND_EARCONS = "Temperature band earcons";
			public static LocString FOLLOW_MOVEMENT_EARCONS = "Follow movement earcons";
			public static LocString FOOTSTEP_EARCONS = "Footstep earcons";
			public static LocString UTILITY_PRESENCE_VOLUME = "Utility presence volume";
			public static LocString PIPE_SHAPE_VOLUME = "Pipe shape volume";
			public static LocString PASSABILITY_VOLUME = "Passability volume";
			public static LocString TEMPERATURE_BAND_VOLUME = "Temperature band volume";
			public static LocString FLOW_SONIFICATION_VOLUME = "Flow sonification volume";
			public static LocString FOLLOW_MOVEMENT_VOLUME = "Follow movement volume";
			public static LocString FOOTSTEP_VOLUME = "Footstep volume";
			public static LocString SCANNER_DIRECTION_EARCONS = "Scanner direction earcons";
			public static LocString SCANNER_DIRECTION_VOLUME = "Scanner direction volume";
			public static LocString FLOW_DIRECTION_READOUT = "Flow direction readout";
		}

		// Supply closet (Klei rewards) screen messages
		public class SUPPLY_CLOSET {
			public static LocString NO_ITEMS = "No items to claim";
			public static LocString OFFLINE = "Not connected to server";
		}

		// Inventory (blueprint gallery) screen
		public class INVENTORY {
			public static LocString GALLERY_TAB = "Gallery";
			public static LocString DETAIL_TAB = "Details";
			public static LocString OWNED = "owned {0}";
			public static LocString UNOWNED = "unowned";
			public static LocString NOT_FOR_SALE = "not for sale";
			public static LocString NOT_FOR_SALE_YET = "not yet available";
			public static LocString TOO_EXPENSIVE = "too expensive, {0} filaments";
			public static LocString ALREADY_OWNED = "already owned";
			public static LocString BUY = "Buy for {0} filaments";
			public static LocString SELL = "Sell for {0} filaments";
			public static LocString SELL_NONE = "Sell, none owned";
			public static LocString FILAMENTS = "Filaments: {0}";
			public static LocString FACADE_FOR = "Blueprint for {0}";
			public static LocString FILTER_OWNERSHIP_ALL = "all";
			public static LocString FILTER_OWNERSHIP_OWNED = "owned only";
			public static LocString FILTER_OWNERSHIP_DOUBLES = "doubles only";
			public static LocString FILTER_DLC_ALL = "all";
			public static LocString OWNERSHIP_FILTER = "Ownership: {0}";
			public static LocString DLC_FILTER = "Collection: {0}";
			public static LocString CONFIRM_BUY = "Buy {0} for {1} filaments?";
			public static LocString CONFIRM_SELL = "Sell {0} for {1} filaments?";
			public static LocString TRANSACTION_LOADING = "Processing";
			public static LocString TRANSACTION_SUCCESS = "Transaction complete";
			public static LocString TRANSACTION_FAILED = "Transaction failed";
		}

		// Wardrobe (outfit browser) screen
		public class WARDROBE {
			public static LocString GALLERY_TAB = "Outfits";
			public static LocString DETAIL_TAB = "Details";
			// {0} = slot display name, {1} = item name or "None"
			public static LocString SLOT_ITEM = "{0}: {1}";
			// {0} = type name (Clothing, Atmo Suit, Jet Suit)
			public static LocString OUTFIT_TYPE = "Type: {0}";
			public static LocString TYPE_CLOTHING = "Clothing";
			public static LocString TYPE_ATMO_SUIT = "Atmo Suit";
			public static LocString TYPE_JET_SUIT = "Jet Suit";
			public static LocString CONTAINS_LOCKED = "contains unowned items";
			public static LocString NO_OUTFITS = "No outfits";
		}

		// Outfit designer screen
		public class OUTFIT_DESIGNER {
			public static LocString SELECTED = "selected";
		}

		// Help overlay key descriptions
		public class HELP {
			public static LocString NAVIGATE = "Step through help entries";
			public static LocString CLOSE = "Close";
			public static LocString NAVIGATE_ITEMS = "Navigate items";
			public static LocString JUMP_FIRST_LAST = "Jump to first or last";
			public static LocString SELECT_ITEM = "Activate selected item";
			public static LocString ADJUST_VALUE = "Adjust value by 1 or 1 percent";
			public static LocString ADJUST_VALUE_LARGE = "Adjust value by 10 or 10 percent";
			public static LocString ADJUST_VALUE_LARGER = "Adjust value by 100 or 25 percent";
			public static LocString ADJUST_VALUE_LARGEST = "Adjust value by 1000 or 50 percent";
			public static LocString TYPE_SEARCH = "Type-ahead search";
			public static LocString SWITCH_PANEL = "Switch panel";
			public static LocString SWITCH_SECTION = "Switch section";
			public static LocString COPY_SETTINGS = "Copy settings";
			public static LocString SWITCH_DUPE_SLOT = "Switch duplicant slot";
			public static LocString SWITCH_OPTION = "Switch option";
			public static LocString MOVE_CURSOR = "Move tile cursor";
			public static LocString READ_COORDS = "Read coordinates";
			public static LocString CYCLE_COORD_MODE = "Cycle coordinate display";
			public static LocString READ_TOOLTIP_SUMMARY = "Read tooltip summary at cursor";
			public static LocString READ_TILE_DETAILS = "Read tile environment details";
			public static LocString CYCLE_GAME_SPEED = "Cycle game speed";
			public static LocString SELECT_ENTITY = "Select object at cursor";
			public static LocString OPEN_GROUP = "Open group";
			public static LocString GO_BACK = "Go back";
			public static LocString JUMP_GROUP = "Jump to next or previous group";
			public static LocString CYCLE_RECIPE = "Cycle recipe";
			public static LocString TOGGLE_OPTION = "Toggle option";
			public static LocString OPEN_CONFIG = "Open settings";
			public static LocString OPEN_FILTER = "Open filter";

			// Help entries for tool-specific keys
			public class TOOLS_HELP {

				public static LocString SET_CORNER = "Set rectangle corner";
				public static LocString CLEAR_RECT = "Clear rectangle at cursor";
				public static LocString CLEAR_CELL = "Clear cell at cursor";
				public static LocString SELECT_CELL = "Select cell";
				public static LocString TOGGLE_MODE = "Toggle selection mode";
				public static LocString CONFIRM_TOOL = "Apply tool at cursor and dismiss";
				public static LocString APPLY_SETTINGS = "Apply settings";
				public static LocString APPLY_AND_EXIT = "Apply settings and exit";
				public static LocString CANCEL_TOOL = "Cancel tool";
				public static LocString SET_PRIORITY = "Set priority";
				public static LocString OPEN_FILTER = "Change filter";
				public static LocString CANCEL_ALL = "Cancel all on map";
			}
		}

		// Crew assignment screen labels
		public class CREW_SCREEN {
			public static LocString AVAILABLE = "Available";
			// {0} = total assigned count after toggle (int)
			public static LocString TOTAL_FORMAT = "{0} total";
		}

		// Recipe queue screen labels
		public class RECIPE {
			// {0} = queue count number or "Forever"
			public static LocString QUEUE_COUNT = "Queue: {0}";
			// {0} = number of other material options
			public static LocString OTHER_OPTIONS = "{0} other options";
		}

		// Select module side screen section headers
		public class MODULE_SCREEN {
			public static LocString MODULES = "Select module";
			public static LocString MATERIALS = "Select material";
			public static LocString FACADE = "Select skin";
		}

		// Colony setup / destination select screen
		public class COLONY_SETUP {
			public static LocString CLUSTER_SELECTOR_HINT = "left and right to switch, enter for details";
		}

		// Labels for colony summary statistics
		public class COLONY_STATS {
			// Label preceding the most recent cycle's value (e.g. "Population, last cycle 12")
			public static LocString LAST_CYCLE = "last cycle";
			// Label preceding the all-time high value (e.g. "peak 15")
			public static LocString PEAK = "peak";
		}

		// Panel/tab names announced when switching UI sections
		public class PANELS {
			public static LocString SEED = "World seed";
			public static LocString ACHIEVEMENTS = "Achievements";
			public static LocString VICTORY_CONDITIONS = "Victory conditions";
			public static LocString STATS = "Stats";
			public static LocString BUTTONS = "Buttons";
			public static LocString PLANETOIDS = "Planetoids";
			public static LocString DLC = "DLC";
			public static LocString NEWS = "News";
			public static LocString NO_NEWS = "No news available";
			public static LocString COLONY_NAME = "Colony name";
			public static LocString RENAME = "Rename";
			public static LocString SHUFFLE_NAME = "Shuffle name";
		}

		// DLC ownership status labels
		public class DLC {
			public static LocString ACTIVE = "Active";
			public static LocString OWNED_NOT_ACTIVE = "Owned, not active";
			public static LocString NOT_OWNED = "Not owned";
		}

		// Text input field feedback
		public class TEXT_EDIT {
			// Announced when entering text edit mode
			public static LocString EDITING = "Editing";
			public static LocString CANCELLED = "Cancelled";
			public static LocString COPIED = "Copied";
			public static LocString PASTED = "Pasted";
		}

		// Standalone state labels appended to item names.
		// These are single words/phrases spoken after an item name
		// to describe its current state (e.g. "Algae Distiller, selected").
		public class STATES {
			// Spoken after an item to indicate it is the current selection (tools, buildings, etc.)
			public static LocString SELECTED = "selected";
			// World trait is guaranteed to appear on the asteroid
			public static LocString GUARANTEED = "present";
			// World trait is excluded from the asteroid
			public static LocString FORBIDDEN = "not present";
			public static LocString ON = "on";
			public static LocString OFF = "off";
			// Checkbox with mixed state (some children on, some off)
			public static LocString MIXED = "mixed";
			// Toggle state: feature/criterion is active (diagnostics, settings, etc.)
			public static LocString ENABLED = "enabled";
			public static LocString DISABLED = "disabled";
			// Filter dropdown: no filter applied
			public static LocString ANY = "Any";
			// Filter dropdown: nothing selected
			public static LocString NONE = "None";
			// Victory/achievement condition status
			public static LocString CONDITION_MET = "met";
			public static LocString CONDITION_MET_OTHER = "met by past colony";
			public static LocString CONDITION_NOT_MET = "not met";
			public static LocString CONDITION_FAILED = "failed";
			// Widget type labels announced for focus context
			public static LocString INPUT_FIELD = "input field";
			public static LocString SLIDER = "slider";
			// Item availability (e.g. blueprint available to claim)
			public static LocString AVAILABLE = "available";
			public static LocString LOCKED = "locked";
			public static LocString ASSIGNED = "assigned";
			public static LocString UNASSIGNED = "unassigned";
			public static LocString QUEUED = "queued";
		}

		// Receptacle (e.g. Display Shelf, Flower Pot) side screen
		public class RECEPTACLE {
			// {0} = number of depositable items (int)
			public static LocString ITEM_COUNT = "{0} items";
			// {0} = comma-separated extent directions from BUILD_MENU.EXTENT_* (e.g. "2 up, 1 right")
			// Spoken when selecting a multi-cell plant in a farm tile
			public static LocString EXTENT_CLEAR = "extends {0}, clear";
			public static LocString EXTENT_BLOCKED = "extends {0}, blocked";
		}

		// Fabricator (e.g. Rock Crusher, Kiln) side screen
		public class FABRICATOR {
			// {0} = queue count from the fabricator's UI label (int)
			public static LocString QUEUED = "{0} queued";
			public static LocString CONTINUOUS = "continuous";
			public static LocString NOT_QUEUED = "not queued";
			// Recipe cannot be fabricated (missing materials or research)
			public static LocString UNAVAILABLE = "unavailable";
		}

		public class GEOTUNER {
			// {0} = number of geotuners targeting this geyser
			public static LocString TUNER_COUNT = "{0} tuners";
		}

		// Button labels for actions not covered by game strings
		public class BUTTONS {
			public static LocString ACCEPT = "Accept";
			public static LocString MANAGE = "Manage";
			public static LocString VIEW_OTHER_COLONIES = "View other colonies";
			public static LocString TOGGLE_ALL = "Toggle all";
			public static LocString MOVE_UP = "Move up";
			public static LocString MOVE_DOWN = "Move down";
		}

		// World generation progress screen
		public class WORLD_GEN {
			public static LocString COMPLETE = "World generation complete";
			// {0} = percentage complete (int, 0-100)
			public static LocString PERCENT = "{0} percent";
		}

		// Labels used in the duplicant selection and game setup screens
		public class INFO {
			public static LocString DIFFICULTY = "Difficulty";
			public static LocString STORY_TRAITS = "Story traits";
			// Fallback noun label for a game difficulty setting when its name is unavailable
			public static LocString SETTING = "setting";
			// Duplicant skill interest label
			public static LocString INTEREST = "Interest";
			public static LocString INTEREST_FILTER = "Interest filter";
			// Duplicant trait type labels
			public static LocString TRAIT = "Trait";
			public static LocString POSITIVE_TRAIT = "Positive trait";
			public static LocString NEGATIVE_TRAIT = "Negative trait";
			public static LocString BIONIC_UPGRADE = "Bionic upgrade";
			public static LocString BIONIC_BUG = "Bionic bug";
			// {0} = duplicant selection slot number (1-indexed int)
			public static LocString SLOT = "Slot {0}";
			// {0} = amount the colony already has (e.g. "200 kg")
			public static LocString COLONY_HAS = "Colony has: {0}";
		}

		// Save/load screen labels
		public class SAVE_LOAD {
			public static LocString SAVE_INFO = "Save info";
			public static LocString CONVERT_ALL_TO_CLOUD = "Convert all to cloud";
			public static LocString CONVERT_ALL_TO_LOCAL = "Convert all to local";
			public static LocString DELETE = "Delete";
			// Tag appended to the most recent save file
			public static LocString NEWEST = "newest";
			// Tag appended to auto-save files
			public static LocString AUTO_SAVE = "auto-save";
		}

		// Key bindings screen
		public class KEY_BINDINGS {
			// Announced when a key action has no binding
			public static LocString UNBOUND = "Unbound";
			// {0} = action name being rebound (e.g. "Move cursor left")
			public static LocString PRESS_KEY_FOR = "Press a key for {0}";
			public static LocString RESET_ALL = "Reset all to defaults";
			public static LocString BINDINGS_RESET = "All bindings reset to defaults";
		}

		// Big cursor area survey
		public class BIG_CURSOR {
			public static LocString HELP_CYCLE_SIZE = "Increase or decrease cursor size";
			public static LocString HELP_RESET_SIZE = "Reset cursor to 1x1";
			// {0} = dimension (int), e.g. "3x3"
			public static LocString SIZE_FORMAT = "{0}x{0}";
			// {0} = percent (int)
			public static LocString UNEXPLORED_PCT = "{0}% unexplored";
			// {0} = element name (string), {1} = percent (int)
			public static LocString ELEMENT_PCT = "{0} {1}%";
			// {0} = element name (string), {1} = percent (int), {2} = formatted mass (string)
			public static LocString ELEMENT_MASS_PCT = "{0} {1}%: {2}";
			// {0} = count (int), {1} = building name (string)
			public static LocString BUILDING_COUNT = "{0} {1}";
			// {0} = count (int)
			public static LocString DUPE_SINGULAR = "{0} dupe";
			// {0} = count (int). Used in area surveys and colony status
			public static LocString DUPE_PLURAL = "{0} dupes";
			// {0} = count (int)
			public static LocString CRITTER_SINGULAR = "{0} critter";
			// {0} = count (int)
			public static LocString CRITTER_PLURAL = "{0} critters";
			// {0} = count (int), {1} = order type (string)
			public static LocString ORDER_COUNT = "{0} {1}";
			// {0} = formatted temperature (string)
			public static LocString AVG_TEMPERATURE = "average {0}";
			// {0} = formatted lux (string)
			public static LocString AVG_LUX = "average {0}";
			// {0} = sign+value (string, e.g. "+12" or "-5")
			public static LocString AVG_DECOR = "average {0} decor";
			// {0} = formatted rads (string)
			public static LocString AVG_RADS = "average {0}";
			// {0} = germ type name (string), {1} = formatted germ count (string)
			public static LocString AVG_DISEASE = "{0} {1}";
			public static LocString DISEASE_CLEAR = "no germs";
			// {0} = count (int), {1} = plant name (string), {2} = avg growth percent (int)
			public static LocString PLANT_ENTRY = "{0} {1}, {2}% grown";
			public static LocString NO_PLANTS = "no plants";
			// {0} = count (int)
			public static LocString UNCATEGORIZED_ROOMS = "{0} uncategorized";
			public static LocString NO_ROOMS = "no rooms";
			public static LocString EMPTY = "empty";
			public static LocString SCAN_ERROR = "scan error";
			public static LocString SOLID = "solid";
			public static LocString LIQUID = "liquid";
			public static LocString GAS = "gas";
			public static LocString VACUUM = "vacuum";
		}

		// Tile cursor navigation and coordinate display
		public class TILE_CURSOR {
			// Announced when cursor enters an unexplored tile or hex (world map and starmap)
			public static LocString UNEXPLORED = "unexplored";
			// {0} = X coordinate (int), {1} = Y coordinate (int)
			public static LocString COORDS = "{0}, {1}";
			// Coordinate display mode labels
			public static LocString COORD_OFF = "coordinates off";
			public static LocString COORD_APPEND = "coordinates append";
			public static LocString COORD_PREPEND = "coordinates prepend";
			public static LocString OVERLAY_NONE = "default view";
			// Announced when cursor tile is not inside any defined room
			public static LocString NO_ROOM = "no room";
			// Announced when cursor has no selectable object (world map and starmap)
			public static LocString NOTHING_TO_SELECT = "nothing to select";
			// Prompt when multiple objects occupy the cursor tile
			public static LocString SELECT_OBJECT = "select an object";

			// Spelled-out name of the period/full-stop key (the punctuation mark).
			// Localizers: this is the keyboard key ".", not an abbreviation.
			public static LocString KEY_PERIOD = "period";

			// Help descriptions for base game management screen hotkeys.
			// These are not mod keys — listed here so blind players can discover them.
			public class MANAGEMENT_HELP {
				public static LocString PRIORITIES = "Priorities";
				public static LocString CONSUMABLES = "Consumables";
				public static LocString VITALS = "Vitals";
				public static LocString RESEARCH = "Research";
				public static LocString SCHEDULE = "Schedule";
				public static LocString SKILLS = "Skills";
				public static LocString COLONY_REPORT = "Colony report";
				public static LocString DATABASE = "Database";
				public static LocString STARMAP = "Starmap";
			}
		}

		public class VIDEO {
			public static LocString PLAYING = "Video playing";

			/// Audio descriptions for tutorial and cinematic videos.
			/// Translators: these describe visual content in short videos that play
			/// during in-game tutorials and victory sequences. Each string is spoken
			/// at a specific timestamp. Comments note what is happening on screen.
			public class DESCRIPTIONS {
				/// Artifact collection victory cinematic (14s). A dupe uncovers
				/// the final artifact and completes the museum collection.
				public class ARTIFACT {
					// [00:00] Dupe operates analysis station
					public static LocString STATION = "A duplicant operates an Artifact Analysis Station. The machine whirs and clanks, stripping away a layer of neutronium to reveal a pristine Old Teapot artifact. The cheerful dupe eagerly snatches up their newly uncovered treasure.";
					// [00:03] Carrying teapot through museum
					public static LocString CARRYING = "The duplicant hurries through a museum room carrying the teapot. They scurry past several Pedestals, which are already proudly displaying a variety of other curious space artifacts.";
					// [00:05] Placing teapot on pedestal
					public static LocString DISPLAY = "They carefully place the Old Teapot onto an empty Pedestal and strike a triumphant pose. A fellow duplicant, looking official with a clipboard and a snazzy hat, nods in approval at the new addition.";
					// [00:08] Clipboard checklist
					public static LocString CHECKLIST = "The view switches to the official looking clipboard, which holds a checklist of various artifact icons. A hand reaches in with a thick red marker and eagerly checks off the final artifact on the grid.";
					// [00:12] Celebration
					public static LocString CELEBRATE = "With the collection finally complete, the two duplicants celebrate their grand success. They throw their hands in the air and jump for joy among their completed museum of shiny space junk.";
				}

				/// Artifact collection victory loop (12s). A quiet museum
				/// displaying recovered space artifacts on pedestals.
				public class ARTIFACT_LOOP {
					// [00:00] Museum overview
					public static LocString DISPLAY = "A quiet museum display showcases the hard won treasures of your colony's cosmic archeological efforts. Several terrestrial artifacts rest proudly on pristine white pedestals against a muted blue wall.";
					// [00:04] Pedestal details
					public static LocString PEDESTALS = "On the pedestals sit a vintage brick cell phone followed by a mesmerizing pink plasma globe. Beside the globe is a simple red teapot and finally a strange mechanical arm. The bright pink energy inside the glass globe arcs and dances continuously bringing a little spark of life to the serene room.";
					// [00:08] Corkboard and floor details
					public static LocString CORKBOARD = "Behind the collection hangs a messy corkboard covered in pinned notes photographs and a blueprint featuring the Gravitas corporation logo. A solitary clipboard with a red pen lies carelessly discarded on the floor beneath the displays.";
				}

				/// "Digging" tutorial video (40s). Dupes mine ore to build a
				/// Microbe Musher, then accidentally flood their workspace.
				public class DIGGING {
					// [00:00] Dupes greet and plan
					public static LocString GREETING = "A solitary duplicant stands in an empty blue void waving cheerfully to the player. He is soon joined by a fellow dupe who gestures towards a blueprint for a Microbe Musher, an early game cooking station.";
					// [00:07] Mining ore from ceiling
					public static LocString MINING = "Before they can cook they need building materials. A glowing dig command appears on the rocky ceiling overhead, and the first dupe eagerly fires up his mining laser to zap a chunk of raw ore loose.";
					// [00:13] Building the Musher
					public static LocString BUILDING = "Using her multi-tool, the second dupe blasts the materials together to construct the shiny new Microbe Musher. A red alert immediately pops up on the machine, indicating that it lacks the resources to operate.";
					// [00:21] Mining dirt for recipe
					public static LocString DIRT = "To whip up a batch of dubious survival food they need a lump of dirt. Another dig command marks a target overhead which is quickly lasered down and chucked straight into the Musher hopper.";
					// [00:30] Reckless ceiling mining
					public static LocString RECKLESS = "The recipe also calls for water, prompting a rather reckless row of dig commands across the entire ceiling. Our happy-go-lucky miner blasts away the rocky barrier, completely oblivious to what is resting directly on top of it.";
					// [00:35] Catastrophic flood
					public static LocString FLOOD = "A massive uncontained reservoir of water comes crashing down and instantly floods their little workspace. The soaking wet dupes stand bewildered in the dark as this lesson in fluid dynamics comes to a splashy end.";
				}

				/// Geothermal victory intro cinematic (9s). The Geothermal
				/// Plant activates and dupes celebrate.
				public class GEOTHERMAL {
					// [00:00] Machine lid pops
					public static LocString LID = "The top of a massive metallic machine sits quietly within a bare tiled room. Suddenly the pressure releases and the heavy lid pops up with a visible hiss of steam.";
					// [00:02] Full plant revealed, dupes cheer
					public static LocString PLANT = "The camera snaps back to reveal the fully operational Geothermal Plant as its giant accordion-like bellow pumps forcefully up and down. Two duplicants stand on either side of the massive structure and cheer wildly at their monumental achievement.";
					// [00:06] Celebration, fade out
					public static LocString CELEBRATE = "The duplicants throw their hands in the air to celebrate their hard-earned victory as the plant efficiently cycles energy. With geothermal power finally flowing steadily, the scene slowly fades away.";
				}

				/// "Insulation" tutorial video (67s). Heat destroys crops, an Ice
				/// Fan fails, then Insulated Tiles save the day.
				public class INSULATION {
					// [00:00] Peaceful colony scene
					public static LocString PEACEFUL = "A cheerful duplicant waves playfully while standing next to a healthy row of blooming Bristle Blossoms. A ceiling light shines down, and everything seems perfectly peaceful in the colony.";
					// [00:04] Plants wilt from heat
					public static LocString WILT = "Suddenly, the Bristle Blossoms completely wither and close up. The duplicant begins to sweat profusely, tugging uncomfortably at her collar as the area becomes dangerously sweltering.";
					// [00:13] Temperature overlay shows heat
					public static LocString OVERLAY = "The Temperature Overlay activates on the screen, painting the room in harsh shades of orange and yellow. This reveals that intense environmental heat is leaking in, baking both the delicate plants and the poor duplicant.";
					// [00:21] Ice Fan cools temporarily
					public static LocString ICE_FAN = "An Ice Fan is constructed beside the crops, and our sweaty duplicant starts furiously cranking its handle. Cool blue arrows of chilling air flow out of the machine, slowly dropping the temperature and turning the room a more pleasant green.";
					// [00:29] Heat seeps back in
					public static LocString HEAT_RETURNS = "Unfortunately, glowing orange arrows indicate that the surrounding environmental heat is forcefully seeping right back into the area. Exhausted from fighting an unwinnable battle against the relentless warmth, the duplicant collapses onto the machine.";
					// [00:37] Second dupe has an idea
					public static LocString IDEA = "Another duplicant arrives and crosses her arms at the pitiful display. She taps her chin in thought, then snaps her fingers as a brilliant engineering idea strikes her.";
					// [00:45] Insulated Tiles built
					public static LocString INSULATED_WALLS = "In a poof of construction dust, a thick continuous border of Insulated Tiles is built. It completely encloses the planted crops and the Ice Fan, creating a sealed, heat-resistant greenhouse.";
					// [00:51] Cooling stays trapped inside
					public static LocString COOLING = "With the revived duplicant back to cranking the fan, the cool blue air now bounces safely off the insulated interior walls. The chilling effect remains perfectly trapped inside their new enclosed room.";
					// [00:57] Heat blocked outside
					public static LocString HEAT_BLOCKED = "Meanwhile, scorching orange arrows of heat from the outside environment bounce harmlessly off the exterior of the new structure. The Insulated Tiles successfully block the sweltering temperatures from ruining their cooling efforts.";
					// [01:02] Room freezes, dupes shiver
					public static LocString FREEZE = "The sealed room quickly drops to a crisp, icy temperature. The two duplicants flash a triumphant thumbs-up to celebrate their survival, but immediately hug themselves and chatter their teeth as they start to freeze across the room.";
				}

				/// Large Impactor defeated cinematic (10s). Rockets destroy the
				/// asteroid threat and dupes celebrate.
				public class LARGE_IMPACTOR_DEFEATED {
					// [00:00] Asteroid floating, rockets strike
					public static LocString ASTEROID = "A massive rocky asteroid floats menacingly against a starry blue space background. This is the Large Impactor, threatening the very existence of your colony before glowing rockets strike it from all sides.";
					// [00:02] Explosion shatters asteroid
					public static LocString EXPLOSION = "A brilliant white explosion rocks the screen, expanding outward with bright yellow energy rings. As the flash fades, the Large Impactor is revealed to be completely shattered into harmless chunks of space rock.";
					// [00:04] Fireworks over debris
					public static LocString FIREWORKS = "Dazzling fireworks burst around the floating debris, marking the glorious destruction of the planetary threat. The remnants drift peacefully through the cosmos.";
					// [00:06] Dupes watching on monitor
					public static LocString WATCHING = "Back inside the base, three duplicants are gathered around a clunky monitor watching the spectacular event. Two joyful dupes leap into a celebratory hug as colorful confetti rains from the ceiling.";
					// [00:08] Third dupe sheds a tear
					public static LocString RELIEF = "The third duplicant sheds a single, dramatic tear of absolute relief. The colony is completely saved, and another catastrophic existential crisis has been successfully averted.";
				}

				/// Large Impactor victory loop (10s). Shattered asteroid
				/// fragments drift through space.
				public class LARGE_IMPACTOR_SPACE_POI {
					// [00:00] Debris field in space
					public static LocString DEBRIS = "A quiet scene unfolds in deep space, showing a scattering of jagged asteroid fragments drifting slowly against a backdrop of blue nebulae and twinkling stars. The shattered remains of the large impactor tumble gently through the void.";
					// [00:05] Surface details on fragments
					public static LocString DETAILS = "Some of the larger chunks of space rock show distinct surface details or exposed colorful ores on their broken edges. The debris field continues its peaceful, endless orbit as the dust settles on a hard fought victory.";
				}

				/// Temporal Tear victory cinematic (10s). A rocket enters
				/// the Temporal Tear while mission control watches.
				public class LEAVE {
					// [00:00] Rocket approaches Temporal Tear
					public static LocString APPROACH = "A swirling, vividly colored portal called the Temporal Tear ripples through the dark expanse of space. A multi stage rocket cruises past floating asteroids, heading straight for the glowing anomaly.";
					// [00:02] Pilot enters, ship plunges in
					public static LocString ENTRY = "Inside the spacecraft, a cheerful duplicant pilot grips the steering controls with a determined little smile. On the outside, the ship plunges directly into the bright white center of the portal.";
					// [00:04] Mission control operator salutes
					public static LocString MISSION_CONTROL = "Down at Mission Control, a headset wearing operator looks anxious as she tracks the flight. She punches a large green button on her terminal, then breaks into a proud smile and delivers a crisp salute while the rest of the crew cheers.";
					// [00:08] Pilot crosses through
					public static LocString CROSSING = "Back in the cockpit, the pilot's momentary confusion quickly melts into an eager grin as the intense light of the Temporal Tear washes over her. Her joyful, starry eyed face flashes across a blinding white screen as she embarks on the colony's ultimate journey.";
				}

				/// Temporal Tear victory loop (12s). The Temporal Tear
				/// pulses in space surrounded by asteroids.
				public class LEAVE_LOOP {
					// [00:00] Glowing vortex
					public static LocString VORTEX = "A vibrant glowing vortex swirls continuously in the dark expanse of space. At its center shines a brilliant white light surrounded by undulating rings of soft teal and deep purple.";
					// [00:05] Asteroids in foreground
					public static LocString ASTEROIDS = "Several jagged asteroids float silently in the foreground around the massive anomaly. The distant starry background remains still while the colorful cosmic whirlpool slowly ripples.";
					// [00:08] Looping context
					public static LocString CONTEXT = "This serene looping view of the Temporal Tear plays endlessly on the victory screen. Our intrepid duplicants have finally escaped through the tear into the great unknown.";
				}

				/// "Locomotion" tutorial video (40s). A Duplicant demonstrates
				/// movement rules: gap jumping, wall climbing, ceiling clearance,
				/// and a friendly encounter with a Shine Bug.
				public class LOCOMOTION {
					// [00:00] Dupe waves from a platform
					public static LocString WAVE = "A smiling duplicant stands on a suspended platform, happily waving hello. This little clone is ready to demonstrate the basics of colony navigation.";
					// [00:04] Two-tile gap is too wide
					public static LocString GAP_BLOCKED = "The duplicant approaches a two-tile-wide gap. An overlay featuring a red X indicates that gaps wider than one tile are too far to jump across, leaving the dupe looking quite dejected.";
					// [00:09] Single-tile gap is jumpable
					public static LocString GAP_JUMP = "The gap closes to a single tile. A green checkmark overlay shows this is a manageable distance, and the duplicant cheerfully leaps across the small chasm.";
					// [00:13] Three-tile wall too high
					public static LocString WALL_BLOCKED = "Next up is a steep vertical wall. A red X overlay demonstrates that a three-tile-high ledge is unfortunately just out of reach for a jumping duplicant.";
					// [00:17] Two-tile wall is climbable
					public static LocString WALL_CLIMB = "When the ledge is suddenly lowered to a two-tile height, a reassuring green checkmark appears. Our determined friend easily clambers up the shorter wall.";
					// [00:21] Shine Bug floats down
					public static LocString SHINE_BUG = "A glowing Shine Bug gracefully floats down from above. The duplicant smiles broadly and reaches out, eager to greet the friendly critter.";
					// [00:24] Two-tile corridor is passable
					public static LocString CORRIDOR = "The duplicant heads toward a narrow corridor. A green checkmark overlay confirms that dupes require a passage at least two tiles high to walk through safely.";
					// [00:28] Dupe smacks into low ceiling
					public static LocString COLLISION = "Sprinting after the Shine Bug, the overly eager dupe smacks face-first into a low-hanging ceiling. They fall backward and hit the ground, rubbing their head in a dizzy daze.";
					// [00:31] One-tile gap is impassable
					public static LocString STUCK = "A red X overlay confirms that duplicants simply cannot squeeze through a tight one-tile-high gap. The poor dupe cowers in fear, feeling completely trapped and alone.";
					// [00:36] Shine Bug reunites with dupe
					public static LocString REUNION = "Fortunately, the floating Shine Bug has no such movement restrictions. The glowing bug easily glides through the tiny gap to reunite with the duplicant, who celebrates the moment with a joyful and much-needed hug.";
				}

				/// "Morale" tutorial video (75s). A dupe learns skills at a
				/// Neural Vacillator, gets stressed, and finds ways to restore morale.
				public class MORALE {
					// [00:00] Dupe at Neural Vacillator
					public static LocString INTRO = "A cheerful duplicant stands beside a towering Neural Vacillator. A meter above his head shows one green and one red block, indicating his morale is balanced. He gives a double thumbs-up.";
					// [00:10] Brain scan grants first skill
					public static LocString SKILL = "He hops inside, and a mechanical scanner lowers over his head. A stylized animation shows his brain glowing as it receives a new skill, represented by a pair of spinning gears.";
					// [00:18] Dupe is miserable
					public static LocString MISERABLE = "Back outside, the duplicant is now slumped over and absolutely miserable. Another duplicant walks by, notices his distress, and strokes her chin thoughtfully.";
					// [00:30] Food cheers him up
					public static LocString FOOD = "She excitedly tosses a Mush Bar, followed by a spiky Bristle Berry, straight into his face. He happily munches the snacks, his spirits instantly restored as he cheers.";
					// [00:40] Second skill granted
					public static LocString SECOND_SKILL = "The Vacillator unexpectedly springs to life and scans him again. A third red gear drops into the mental display, granting his brain an advanced skill.";
					// [00:48] Stress increases
					public static LocString STRESS = "This upgrade increases his demands, dropping a second red block onto his morale meter. He slumps over from the stress, prompting his companion to roll her eyes.";
					// [00:54] Arcade Cabinet
					public static LocString ARCADE = "She marches toward an Arcade Cabinet, plucking him out of the scanner and hurling him at the controls. They aggressively mash the buttons, adding three green morale blocks to his meter, but he soon starts pouting again.";
					// [01:07] Comfy Bed restores balance
					public static LocString BED = "Out of patience, she hurls him across the base into a Comfy Bed surrounded by paintings. He smiles as he settles into the blankets, gaining one final green block to perfectly balance his morale.";
				}

				/// "Piping" tutorial video (35s). A dupe builds pipes to connect
				/// a pump to an Espresso Machine, learning input/output ports.
				public class PIPING {
					// [00:00] Miserable caffeine-deprived dupe
					public static LocString MISERABLE = "A duplicant stands alone, looking completely miserable. Searching for a solution to his caffeine deprivation, he sighs heavily and points downward.";
					// [00:06] Pump and Espresso Machine revealed
					public static LocString OVERVIEW = "The view expands to reveal a Liquid Pump submerged in a pool of water below him. On the platform next to the duplicant sits an un-plumbed Espresso Machine.";
					// [00:10] Plumbing overlay shows ports
					public static LocString OVERLAY = "The plumbing overlay activates, displaying a green output arrow on the pump and a white input arrow on the Espresso Machine. An animated line demonstrates that liquids must flow from a green output to a white input.";
					// [00:16] Pipe built, water flows
					public static LocString PIPE_BUILT = "The duplicant whips out his tools and quickly builds a Liquid Pipe connecting the two ports. Once finished, blue spheres of fresh water automatically begin traveling through the pipes and into the machine's intake.";
					// [00:26] Output dumps on second dupe
					public static LocString SPLASH = "A second duplicant wanders by just as the machine begins running. Unfortunately, the Espresso Machine's green output port is piped directly over his head, dumping a steaming splash of polluted water onto him.";
					// [00:30] Lesson on input/output icons
					public static LocString LESSON = "The drenched duplicant groans in disgust. The video freezes to highlight the golden rule of plumbing: a white box with a downward arrow marks an input, while a green box with an upward arrow marks an output.";
				}

				/// "Power" tutorial video (30s). A dupe overloads a circuit
				/// then reorganizes it into two balanced circuits.
				public class POWER {
					// [00:00] Dupe waves in front of blueprint backdrop
					public static LocString INTRO = "A duplicant with a stylish pompadour stands proudly in front of a blue blueprint backdrop, waving cheerfully. He's ready to learn a little something about power management.";
					// [00:04] Building a Jumbo Battery
					public static LocString BATTERY = "Armed with a trusty sci-fi blowtorch, the dupe eagerly constructs a brand new Jumbo Battery. It hooks right up to an existing electrical wire, ready to store some serious juice.";
					// [00:08] Wire overloads
					public static LocString OVERLOAD = "Suddenly, the battery sparks wildly and belches smoke with a loud pop! A broken connection icon pops up as the attached wire turns an angry red, buckling under the excessive electrical strain.";
					// [00:13] Power Overlay shows overloaded circuit
					public static LocString OVERLAY = "The view shifts into the Power Overlay, revealing a disastrously long line of buildings all crammed onto one single wire. The overloaded circuit pulses bright red as power shuts down across the entire room.";
					// [00:19] Dupe has an idea
					public static LocString IDEA = "Our dupe pauses, tapping his chin in deep thought before raising a finger with a brilliant idea! He whips out a fresh blueprint and gets straight to work fixing the mess.";
					// [00:22] Dust cloud rebuild
					public static LocString REBUILD = "A massive cartoon dust cloud erupts across the screen. Inside the chaos, the dupe rapidly deconstructs and reorganizes the colony's electrical grid.";
					// [00:26] Two balanced circuits
					public static LocString RESULT = "The dust clears to reveal a much smarter setup. The machines are now cleanly divided into two separate, perfectly functioning green power circuits, and our dupe celebrates a job well done!";
				}

				/// Spaced Out intro cinematic (108s). A Temporal Tear accident
				/// shatters a planet and strands dupes on an asteroid fragment.
				public class SPACED_OUT_INTRO {
					// [00:00] Rocky asteroid in space
					public static LocString ASTEROID = "A small, rocky asteroid floats peacefully through the cosmos. Tiny, cute buildings are nestled into its craters.";
					// [00:03] Control room, portal on screen
					public static LocString CONTROL_ROOM = "Inside a bustling control room, a cheerful duplicant monitors a screen. A vibrant portal swirls safely on the display.";
					// [00:08] Rocket emerges, screen glitches
					public static LocString ROCKET = "The duplicant cheers as a rocket ship boldly emerges from the portal. But then, the screen violently glitches.";
					// [00:13] Shattered planet on display
					public static LocString SHATTERED = "The display now shows a shattered planet. The controller gasps in horror as an animation shows a rocket diving into a swirling blue wormhole.";
					// [00:23] Rockets soaring through cosmos
					public static LocString ROCKETS = "We see the actual rockets soaring through the cosmos. Inside one, a duplicant waves cheerfully, completely oblivious to any impending doom.";
					// [00:31] Crew watches in panic
					public static LocString PANIC = "Back in the control room, the rest of the crew watches with growing panic. On screen, a swarm of rockets frantically orbits the completely fractured planet.";
					// [00:42] Wireframe around planet
					public static LocString WIREFRAME = "A glowing blue wireframe appears around the planet, attempting to hold the crumbling pieces together. The wireframe suddenly snaps and vanishes.";
					// [00:49] Planet splits in two
					public static LocString SPLIT = "The giant planet splits violently in two. It radiates a volatile blue energy from its exposed core.";
					// [00:58] Planet materializes near asteroid
					public static LocString COLLISION = "The massive, broken planet abruptly materializes right next to the peaceful little asteroid. The control room erupts in wildly flailing chaos.";
					// [01:05] Asteroid struck, alien flashes
					public static LocString STRUCK = "The asteroid is struck and sent hurtling off course. Glitches suddenly flash across the view, showing bizarre, large-eyed alien critters.";
					// [01:11] Blinding flash, asteroid shatters
					public static LocString FLASH = "With a blinding flash of white light, the entire asteroid is shattered into jagged pieces.";
					// [01:18] Dupes fall into cavern
					public static LocString CAVERN = "A rocky ceiling crumbles, and three soot-covered duplicants plummet down. They crash onto the floor in a tangled, groaning heap.";
					// [01:29] Hatch opens on surface
					public static LocString HATCH = "Out in the cold void, a metal hatch slowly creaks open on a barren surface. A solitary duplicant in a round space helmet cautiously peeks out.";
					// [01:38] More dupes peek out
					public static LocString PEEK = "Two more helmeted dupes awkwardly squeeze their heads out beside her. The view slowly pans out to reveal their new, very permanent predicament.";
					// [01:43] Trio stranded in space
					public static LocString STRANDED = "They are stranded on a critically tiny fragment of rock. The oblivious trio stares blankly into space, entirely surrounded by floating space debris.";
				}

				/// Thriving victory cinematic (10s). Dupes throw a confetti
				/// party celebrating colony sustainability.
				public class STAY {
					// [00:00] Close-up, dupe cheers
					public static LocString CHEER = "A close-up shows a duplicant peacefully closing their eyes, then throwing their hands in the air with a massive joyful cheer. Colorful square confetti begins raining down from above.";
					// [00:02] More dupes join celebration
					public static LocString JOINING = "The camera pulls back to reveal more duplicants joining the celebration. One cheers in a full atmo suit while another wearing a gas mask happily hoists a giant jar of pickled mealwood.";
					// [00:04] Full room party
					public static LocString PARTY = "The view zooms out further to show a group of duplicants dancing, hugging, and tossing supplies in a pristine plastic-tiled room. Confetti continues to fall as the colony celebrates reaching true sustainability.";
					// [00:08] Scene fades
					public static LocString FADE = "The joyous scene rapidly shrinks into the distance before fading entirely into black.";
				}

				/// Thriving victory loop (10s). The messy aftermath of a
				/// colony celebration in a neglected room.
				public class STAY_LOOP {
					// [00:00] Empty room after party
					public static LocString ROOM = "The screen displays an empty room inside the asteroid colony in the quiet aftermath of a massive celebration. This is the continuous background loop of the victory screen playing after your duplicants have successfully established a thriving colony and decided to stay.";
					// [00:04] Floor littered with party remnants
					public static LocString DEBRIS = "The floor is heavily littered with the colorful remnants of a party including scattered confetti and a few overturned drinking cups. A large bright puddle of questionable green sludge stains the floorboards with a stray drinking straw sticking haphazardly out of it.";
					// [00:07] Bubble, pipes, and ladder
					public static LocString DETAILS = "A single delicate bubble floats gently near the left wall under the rough and uneven rocky ceiling above. Rusty metallic pipes snake along the back wall near an old wooden ladder that stands completely unused in the quiet room.";
				}

				/// Geothermal victory loop (10s). The Geothermal Plant pumps
				/// rhythmically while dupes celebrate.
				public class GEOTHERMAL_LOOP {
					// [00:00] Machine in metal room
					public static LocString MACHINE = "A massive piece of industrial machinery sits in the center of a metal paneled room flanked by tall ladders. This is the background for the Geothermal Plant victory screen which loops continuously to celebrate your colony's success.";
					// [00:03] Bellows pumping, left dupe dances
					public static LocString PUMPING = "The machine rhythmically pumps upwards to reveal a large purple bellows expanding in its midsection before compressing back down. On the left side of the room a duplicant dances joyfully while waving a wrench in the air.";
					// [00:06] Right dupe taps along
					public static LocString DANCING = "Over on the right a second duplicant in a yellow hard hat taps their foot and nods along rhythmically while holding a clipboard. The entire scene continues to bounce along in a lively endless loop of geothermal triumph.";
				}
			}
		}

		// Dupe cycle navigator ([ / ] / \ keys)
		public class DUPES {
			public static LocString NO_DUPLICANTS = "no duplicants";
			public static LocString IDLE = "idle";
			public static LocString INCAPACITATED = "incapacitated";
			public static LocString HEALTH_CRITICAL = "critical health";
			public static LocString HEALTH_INJURED = "injured";
			public static LocString SICK = "sick";
			public static LocString KEY_BRACKETS = "Left bracket / Right bracket";

			public static LocString HELP_CYCLE = "Cycle through duplicants";
			public static LocString HELP_JUMP = "Jump cursor to current duplicant, or open details";
			public static LocString HELP_CHECK_PATH = "Check if current duplicant can reach cursor tile";

			public class PATHABILITY {
				// {0} = cost (int)
				public static LocString REACHABLE = "reachable, cost {0}";
				// {0} = offset description (e.g. "3 right 1 up")
				public static LocString UNREACHABLE_NEAREST = "unreachable, nearest reachable {0}";
				public static LocString UNREACHABLE_NO_NEARBY = "unreachable, no reachable tiles nearby";
			}

			public class FOLLOW {
				// {0} = dupe name
				public static LocString FOLLOWING = "following {0}";
				// {0} = status item name
				public static LocString STATUS_ENDED = "ended {0}";
				public static LocString HELP_FOLLOW = "Follow current duplicant with camera";
			}
		}

		// Bot cycle navigator (Shift+[ / Shift+] keys)
		public class BOTS {
			public static LocString NO_BOTS = "no bots";
			public static LocString KEY_SHIFT_BRACKETS = "Shift+Left bracket / Shift+Right bracket";
			public static LocString HELP_CYCLE = "Cycle through bots";
		}

		// Game clock and speed announcements
		public class GAME_STATE {
			// {0} = speed name (e.g. "1x", "2x", "3x")
			public static LocString UNPAUSED = "unpaused, {0}";
			// {0} = cycle number (int)
			public static LocString CYCLE = "Cycle {0}";
			// {0} = cycle number (int), {1} = schedule block/hour (int, 0-23)
			public static LocString CYCLE_STATUS = "Cycle {0}, block {1}";
			public static LocString READ_CYCLE_STATUS = "Read cycle status";
			public static LocString READ_TIME_PLAYED = "Read total playtime";
			public static LocString RED_ALERT_OFF = "Red Alert off";
			public static LocString YELLOW_ALERT_OFF = "Yellow Alert off";
			public static LocString TOGGLE_RED_ALERT = "Toggle red alert";
			public static LocString READ_COLONY_STATUS = "Read colony status";
			public static LocString SAVED = "Saved";
			// {0} = local world dupe count, {1} = total dupe count
			public static LocString DUPES_CLUSTER = "{0}/{1} dupes";
			// {0} = sick count
			public static LocString SICK = "{0} sick";
			// {0} = formatted kcal string from GameUtil.GetFormattedCalories
			public static LocString RATIONS = "{0}";
			// {0} = stress percentage (int)
			public static LocString STRESS = "{0}% stress";
			// {0} = formatted joules string from GameUtil.GetFormattedJoules
			public static LocString ELECTROBANKS = "{0}";
		}

		public class DEMOLIOR {
			// {0} = formatted cycles until impact
			public static LocString DISCOVERED = "Demolior detected, impact in {0}";
			public static LocString IMPACTED = "Demolior has impacted the colony";
			public static LocString DESTROYED = "Demolior destroyed";
			// {0} = health percentage (int)
			public static LocString HEALTH = "Demolior {0}%";
			// {0} = health percentage (int), {1} = formatted cycles until impact
			public static LocString STATUS = "Demolior {0}%, {1}";
		}

		// Tooltip summary readout at cursor
		public class TOOLTIP {
			public static LocString NO_TOOLTIP = "no tooltip";
			public static LocString CLOSED = "closed";
			public static LocString CANNOT_CONTINUE = "cannot continue";
			public static LocString CONTINUING = "continuing";
		}

		// Tool activation, selection feedback, and work order confirmations
		public class TOOLS {
			// Tool picker/filter panel names
			public static LocString PICKER_NAME = "tool menu";
			public static LocString FILTER_NAME = "tool filter";
			// Singular/plural for generic counted items in tool confirmations
			public static LocString ITEM_SINGULAR = "item";
			public static LocString ITEM_PLURAL = "items";
			// Announced when first rectangle corner is placed
			public static LocString CORNER_SET = "corner set";
			// {0} = width (int), {1} = height (int), {2} = valid cell count (int)
			public static LocString RECT_SUMMARY = "{0}x{1}, {2} valid";
			// {0} = width (int), {1} = height (int), {2} = valid cell count (int), {3} = invalid cell count (int)
			public static LocString RECT_SUMMARY_INVALID = "{0}x{1}, {2} valid, {3} invalid";
			public static LocString CANCELED = "canceled";
			public static LocString NO_VALID_CELLS = "no valid cells";
			// Tool confirmation messages after applying a tool to a selection.
			// {0} = cell/item count (int), {1} = priority level (int), {2} = item type word (singular or plural)
			public static LocString CONFIRM_DIG = "marked {0} for digging at priority {1}";
			public static LocString CONFIRM_MOP = "marked {0} for mopping at priority {1}";
			public static LocString CONFIRM_DISINFECT = "marked {0} for disinfection at priority {1}";
			public static LocString CONFIRM_SWEEP = "marked {0} for sweeping at priority {1}";
			public static LocString CONFIRM_DECONSTRUCT = "marked {0} for deconstruction at priority {1}";
			// {0} = count (int), {2} = item type word (singular or plural). No {1} used.
			public static LocString CONFIRM_CANCEL = "cancelled {0} {2}";
			// {0} = filter name (e.g. "All", "Buildings")
			public static LocString CANCEL_ALL_MAP = "cancelled {0} on entire map";
			// {0} = count (int), {1} = priority level (int), {2} = item type word (singular or plural)
			public static LocString CONFIRM_PRIORITIZE = "updated {0} {2} to priority {1}";
			// {0} = plant count (int), {1} = priority level (int)
			public static LocString CONFIRM_HARVEST = "set harvest on {0} plants at priority {1}";
			// {0} = count (int), {1} = priority level (int)
			public static LocString CONFIRM_ATTACK = "marked {0} for attack at priority {1}";
			public static LocString CONFIRM_CAPTURE = "marked {0} for capture at priority {1}";
			public static LocString CONFIRM_EMPTY_PIPE = "marked {0} pipe cells for emptying at priority {1}";
			// {0} = segment count (int)
			public static LocString CONFIRM_DISCONNECT = "disconnected {0} segments";
			// {0} = priority level (int)
			public static LocString PRIORITY_BASIC = "priority {0}";
			public static LocString PRIORITY_EMERGENCY = "emergency priority";
			// Tile cursor: existing work order labels.
			// {0} = priority level (int) when present
			public static LocString DIG_ORDER = "dig order";
			public static LocString DIG_ORDER_PRIORITY = "dig order, priority {0}";
			public static LocString MOP_ORDER = "mop order";
			public static LocString MOP_ORDER_PRIORITY = "mop order, priority {0}";
			public static LocString MARKED_DISINFECT = "marked for disinfect";
			public static LocString MARKED_SWEEP = "marked for sweep";
			public static LocString MARKED_SWEEP_PRIORITY = "marked for sweep, priority {0}";
			public static LocString MARKED_ATTACK = "marked for attack";
			public static LocString MARKED_CAPTURE = "marked for capture";
			public static LocString MARKED_DECONSTRUCT = "marked for deconstruct";
			public static LocString MARKED_DECONSTRUCT_PRIORITY = "marked for deconstruct, priority {0}";
			// Disinfect tool: object with disease info.
			// {0} = object name, {1} = disease name, {2} = disease germ count (int)
			public static LocString DISINFECT_OBJECT = "{0}, {1}, {2}";
			// Empty pipe tool: pipe with no contents.
			// {0} = pipe type name (e.g. "liquid", "gas")
			public static LocString PIPE_EMPTY = "{0}, empty";
			// Empty pipe tool: pipe with contents.
			// {0} = pipe type name, {1} = element name, {2} = formatted mass
			public static LocString PIPE_CONTENTS = "{0}, {1}, {2}";
			public static LocString DISCONNECT_TOO_FAR = "adjacent cells only";
			// Filter state change announcements
			public static LocString FILTERED = "filtered";
			public static LocString FILTER_REMOVED = "filter removed";
			public static LocString SELECTION_CLEARED = "selection cleared";
			public static LocString RECT_CLEARED = "rectangle cleared";
			// Single-cell selection mode (Ctrl+G toggle, Shift+Space cell removal)
			public static LocString CELL_CLEARED = "cleared";
			public static LocString CELL_SELECTED = "selected";
			public static LocString SINGLE_MODE_ON = "single selection";
			public static LocString SINGLE_MODE_OFF = "rectangle selection";
			// Tool activation announcements.
			// {0} = tool name, {1} = filter or priority text, {2} = priority text
			public static LocString ACTIVATION_PLAIN = "{0}";
			public static LocString ACTIVATION = "{0}, {1}";
			public static LocString ACTIVATION_WITH_FILTER = "{0}, {1}, {2}";
			public static LocString FALLBACK_LABEL = "tool";
			// {0} = entity name being moved to
			public static LocString MOVE_TO_ACTIVATION = "move to, {0}";
			public static LocString MOVE_TO_CONFIRMED = "destination set";
			public static LocString MOVE_TO_UNREACHABLE = "unreachable";
			// {0} = source building name
			public static LocString COPY_SETTINGS_ACTIVATION = "{0}, copy settings";
			public static LocString COPY_SETTINGS_NO_TARGET = "no matching building";
			// Spoken when copy-settings targets a farm tile but the plant could not
			// be changed (e.g. destination already has a different plant, or
			// something blocks placement)
			public static LocString COPY_SETTINGS_FAILED = "settings not applied";
			public static LocString COPY_SETTINGS_UNAVAILABLE = "no copyable settings";
			// {0} = item being placed
			public static LocString PLACE_ACTIVATION = "place, {0}";
			public static LocString PLACE_INVALID = "invalid location";
			public static LocString DONE = "done";
		}

		// Temperature warning labels on tile cursor
		public class TEMPERATURE {
			public static LocString NEAR_FREEZING = "near freezing";
			public static LocString NEAR_BOILING = "near boiling";
			public static LocString NEAR_OVERHEAT = "near overheat";
		}

		// Tile cursor glance info: brief summaries of what occupies a tile
		public class GLANCE {
			// {0} = plant name (e.g. "Arbor Tree")
			public static LocString PLANT_BRANCH = "{0} branch";
			// {0} = building name being constructed
			public static LocString UNDER_CONSTRUCTION = "constructing {0}";
			// {0} = building name marked for deconstruction
			public static LocString MARKED_DECONSTRUCTION = "deconstructing {0}";

			// Building port type labels
			public static LocString POWER_INPUT = "power input";
			public static LocString POWER_OUTPUT = "power output";
			public static LocString GAS_INPUT = "gas input";
			public static LocString GAS_OUTPUT = "gas output";
			public static LocString LIQUID_INPUT = "liquid input";
			public static LocString LIQUID_OUTPUT = "liquid output";
			public static LocString SOLID_INPUT = "conveyor input";
			public static LocString SOLID_OUTPUT = "conveyor output";
			public static LocString INPUT_PORT = "input";
			public static LocString OUTPUT_PORT = "output";

			// Work order labels on tiles
			public static LocString ORDER_BUILD = "build";
			public static LocString ORDER_DIG = "dig";
			public static LocString ORDER_MOP = "mop";
			public static LocString ORDER_SWEEP = "sweep";
			public static LocString ORDER_DECONSTRUCT = "deconstruct";
			public static LocString ORDER_HARVEST = "harvest";
			public static LocString ORDER_UPROOT = "uproot";
			public static LocString ORDER_DISINFECT = "disinfect";
			public static LocString ORDER_ATTACK = "attack";
			public static LocString ORDER_CAPTURE = "capture";
			public static LocString ORDER_EMPTY_PIPE = "empty pipe";
			// {0} = order type label (e.g. "dig"), {1} = priority level (int)
			public static LocString ORDER_PRIORITY = "{0} priority {1}";
			// {0} = order label (e.g. "dig priority 5")
			public static LocString ORDER_UNREACHABLE = "unreachable {0}";
			// {0} = order label (e.g. "dig priority 5")
			public static LocString ORDER_NEEDS_SKILL = "needs skill {0}";
			// {0} = order label (e.g. "sweep priority 5")
			public static LocString ORDER_CANT_STORE = "can't store {0}";

			// Point-of-interest marker on a tile
			public static LocString TILE_OF_INTEREST = "P O I";
			// Rocket access/output point labels
			public static LocString ACCESS_POINT = "access point";
			public static LocString OUTPUT_POINT = "output point";

			// Radbolt (radiation bolt) port labels
			public static LocString RADBOLT_INPUT = "radbolt input";
			public static LocString RADBOLT_OUTPUT = "radbolt output";
			public static LocString RADBOLT_OUTPUT_DIRECTION = "radbolt output, {0}";

			// Conduit/network type labels
			public static LocString CONDUIT_LIQUID = "liquid";
			public static LocString CONDUIT_GAS = "gas";
			public static LocString CONDUIT_SOLID = "solid";
			public static LocString WIRE = "wire";
			public static LocString UNKNOWN_ELEMENT = "unknown";

			// Semantic port qualifiers: {0} = base port label (e.g. "gas output")
			public static LocString FILTERED_PORT = "filtered {0}";
			public static LocString OVERFLOW_PORT = "overflow {0}";
			public static LocString PRIORITY_PORT = "priority {0}";

			// {0} = port type label, {1} = port number (int, when building has multiple ports of same type)
			public static LocString NUMBERED_PORT = "{0} {1}";

			// Building extension cells: {0} = building name
			public static LocString INTAKE_PIPE = "{0} intake pipe";
			public static LocString LURE = "{0} lure";
			// Bridge middle cell (between the two endpoints): {0} = building name
			public static LocString BRIDGE_MIDDLE = "{0} middle";
			// Wire bridge endpoint where wires attach. Spoken before the building
			// name, e.g. "connection port, Heavy-Watt Joint Plate".
			public static LocString CONNECTION = "connection port";
			// Unoccupied transit tube connection point near an entrance or crossing.
			public static LocString TUBE_CONNECTION = "connection";

			// Decor overlay value. {0} = sign prefix ("+" or ""), {1} = decor value (int)
			public static LocString OVERLAY_DECOR = "{0}{1} decor";
			// Disease overlay: tile is clean
			public static LocString DISEASE_CLEAR = "clean";
			// {0} = disease name, {1} = formatted germ count
			public static LocString DISEASE_ENTRY = "{0}, {1}";
			// {0} = element name, {1} = formatted mass
			public static LocString ELEMENT_MASS = "{0}, {1}";
			// Conduit/wire shape announcements.
			// Describes how a pipe or wire segment connects to its neighbors.
			// Pipe connecting up and down (straight vertical line)
			public static LocString SHAPE_VERTICAL = "vertical";
			// Pipe connecting left and right (straight horizontal line)
			public static LocString SHAPE_HORIZONTAL = "horizontal";
			// Pipe turning a corner. {0} = vertical direction (up/down), {1} = horizontal direction (left/right).
			// Example: "up right corner" for a pipe connecting up and right
			public static LocString SHAPE_CORNER = "{0} {1} corner";
			// Three-way junction (T-shape). {0} = direction the branch extends toward.
			// Example: "right tee junction" for a pipe connecting up, down, and right
			public static LocString SHAPE_T = "{0} tee junction";
			// Four-way junction (pipe connects in all directions)
			public static LocString SHAPE_CROSS = "cross";
			// Dead-end pipe with one connection. {0} = direction the pipe ends toward (opposite of its connection).
			// Example: "ends down" for a pipe that connects upward and has its dead end facing down
			public static LocString SHAPE_END = "ends {0}";
			// Pipe segment with no connections to neighbors
			public static LocString SHAPE_ALONE = "unconnected";

			// Flow direction readout. Appended to pipe glance when enabled
			public static LocString FLOW_EMPTY = "empty";
			public static LocString FLOW_NOT_FLOWING = "not flowing";
			// {0} = percentage, {1} = direction name (e.g. "right")
			public static LocString FLOW_DIRECTION_PERCENT = "{0}% {1}";
			// {0} = element name, {1} = direction percentages (e.g. "80% right 15% up")
			public static LocString FLOW_ELEMENT_DIRECTIONS = "{0}, {1}";

			// {0} = integer percentage of circuit load (can exceed 100)
			public static LocString WIRE_LOAD_PERCENT = "{0} percent";
		}

		public class SCANNER {
			// Confirmation
			public static LocString REFRESHED = "scanned";
			public static LocString EMPTY = "no results";
			// Announced when a previously scanned entity no longer exists
			public static LocString INVALID = "gone";

			// Announcement format: {0} = name/label, {1} = distance, {2} = index-of-count
			public static LocString INSTANCE_WITH_DISTANCE = "{0}, {1}, {2}";
			// Announcement format: {0} = name/label, {1} = index-of-count
			public static LocString INSTANCE_NO_DISTANCE = "{0}, {1}";
			// {0} = name, {1} = distance, {2} = mass, {3} = index-of-count
			public static LocString INSTANCE_WITH_DISTANCE_MASS = "{0}, {1}, {2}, {3}";
			// {0} = name, {1} = mass, {2} = index-of-count
			public static LocString INSTANCE_NO_DISTANCE_MASS = "{0}, {1}, {2}";
			// {0} = formatted mass string (e.g. "1.2 kg")
			public static LocString MASS_AVERAGE = "{0} average";
			// {0} = tile count, {1} = item name
			public static LocString CLUSTER_LABEL = "{0} {1}";
			// {0} = order type, {1} = target name
			public static LocString ORDER_LABEL = "{0} {1}";
			// {0} = tile count, {1} = order type, {2} = target name
			public static LocString ORDER_CLUSTER_LABEL = "{0} {1} {2}";
			// {0} = tile count, {1} = order type (no target)
			public static LocString ORDER_CLUSTER_COUNT = "{0} {1}";
			// {0} = current index (int), {1} = total count (int)
			public static LocString INSTANCE_OF = "{0} of {1}";
			public static LocString MIXED = "mixed";

			// {0} = biome zone name from game data (e.g. "Forest")
			public static LocString BIOME_NAME = "{0} Biome";
			// {0} = element name (e.g. "Water")
			public static LocString BOTTLE_NAME = "Bottled {0}";
			// {0} = element name (e.g. "Sand")
			public static LocString LOOSE_NAME = "Loose {0}";

			// Direction tokens
			public static LocString DIRECTION_UP = "up";
			public static LocString DIRECTION_DOWN = "down";
			public static LocString DIRECTION_LEFT = "left";
			public static LocString DIRECTION_RIGHT = "right";
			public static LocString DIRECTION_UP_LEFT = "up left";
			public static LocString DIRECTION_UP_RIGHT = "up right";
			public static LocString DIRECTION_DOWN_LEFT = "down left";
			public static LocString DIRECTION_DOWN_RIGHT = "down right";

			// Distance templates: {0} = tile count, {1} = direction word
			public static LocString DISTANCE_VERTICAL = "{0} {1}";
			public static LocString DISTANCE_HORIZONTAL = "{0} {1}";
			// {0} = vertical distance, {1} = horizontal distance
			public static LocString DISTANCE_BOTH = "{0} {1}";

			// Scanner category names
			public class CATEGORIES {
				public static LocString SOLIDS = "Solids";
				public static LocString LIQUIDS = "Liquids";
				public static LocString GASES = "Gases";
				public static LocString BUILDINGS = "Buildings";
				public static LocString NETWORKS = "Networks";
				public static LocString AUTOMATION = "Automation";
				public static LocString DEBRIS = "Debris";
				public static LocString ZONES = "Zones";
				public static LocString GEYSERS = "Geysers";
				public static LocString LIFE = "Life";
				public static LocString SEARCH = "Search";
			}

			// Scanner subcategory names within each category
			public class SUBCATEGORIES {
				public static LocString ALL = "all";

				// Solids
				public static LocString ORES = "Ores";
				public static LocString STONE = "Stone";
				public static LocString CONSUMABLES = "Consumables";
				public static LocString ORGANICS = "Organics";
				public static LocString ICE = "Ice";
				public static LocString REFINED = "Refined";
				public static LocString TILES = "Tiles";

				// Liquids
				public static LocString WATERS = "Waters";
				public static LocString FUELS = "Fuels";
				public static LocString MOLTEN = "Molten";
				public static LocString MISC = "Misc";

				// Gases
				public static LocString SAFE = "Safe";
				public static LocString UNSAFE = "Unsafe";

				// Buildings
				public static LocString OXYGEN = "Oxygen";
				public static LocString GENERATORS = "Generators";
				public static LocString FARMING = "Farming";
				public static LocString PRODUCTION = "Production";
				public static LocString STORAGE = "Storage";
				public static LocString REFINING = "Refining";
				public static LocString TEMPERATURE = "Temperature";
				public static LocString WELLNESS = "Wellness";
				public static LocString MORALE = "Morale";
				public static LocString INFRASTRUCTURE = "Infrastructure";
				public static LocString ROCKETRY = "Rocketry";
				public static LocString GRAVITAS = "Gravitas";

				// Networks
				public static LocString POWER = "Power";
				public static LocString LIQUID = "Liquid";
				public static LocString GAS = "Gas";
				public static LocString CONVEYOR = "Conveyor";
				public static LocString TRANSPORT = "Transport";

				// Automation
				public static LocString SENSORS = "Sensors";
				public static LocString GATES = "Gates";
				public static LocString CONTROLS = "Controls";
				public static LocString WIRES = "Wires";

				// Debris
				public static LocString MATERIALS = "Materials";
				public static LocString FOOD = "Food";
				public static LocString ITEMS = "Items";
				public static LocString BOTTLES = "Bottles";

				// Zones
				public static LocString ORDERS = "Orders";
				public static LocString ROOMS = "Rooms";
				public static LocString BIOMES = "Biomes";

				// Geysers
				public static LocString GEOTHERMAL = "Geothermal";

				// Life
				public static LocString DUPLICANTS = "Duplicants";
				public static LocString ROBOTS = "Robots";
				public static LocString TAME_CRITTERS = "Tame Critters";
				public static LocString WILD_CRITTERS = "Wild Critters";
				public static LocString WILD_PLANTS = "Wild Plants";
				public static LocString FARM_PLANTS = "Farm Plants";
			}

			// Auto-move toggle
			public static LocString AUTO_MOVE_ON = "auto-move on";
			public static LocString AUTO_MOVE_OFF = "auto-move off";

			// Spoken when the target is at the cursor position (scanner, pathability, building ports)
			public static LocString HERE = "here";

			// Search
			public class SEARCH {
				public static LocString PROMPT = "search";
			}

			// Help entries
			public class HELP {
				public static LocString REFRESH = "Refresh scanner";
				public static LocString TELEPORT = "Jump to selected scanner entry";
				public static LocString TOGGLE_AUTO_MOVE = "Toggle auto-move cursor";
				public static LocString CYCLE_CATEGORY = "Scanner: cycle category";
				public static LocString CYCLE_SUBCATEGORY = "Scanner: cycle subcategory";
				public static LocString CYCLE_ITEM = "Scanner: cycle item";
				public static LocString CYCLE_INSTANCE = "Scanner: cycle instance";
				public static LocString ORIENT_ITEM = "Distance to current item";
				public static LocString SEARCH = "Search scanner";
				public static LocString TELEPORT_BACK = "Return to pre-teleport position";
			}
		}

		// Color names for the Pixel Pack building's decorative color picker.
		// These are approximate labels, not precise color specifications.
		// Translate to the closest natural color word in the target language.
		public class COLORS {
			public static LocString DARK_GRAY = "dark gray";
			public static LocString BLUE = "blue";
			public static LocString DARK_BLUE = "dark blue";
			public static LocString INDIGO = "indigo";
			public static LocString PURPLE = "purple";
			public static LocString MAROON = "maroon";
			public static LocString DARK_RED = "dark red";
			public static LocString BROWN = "brown";
			public static LocString DARK_OLIVE = "dark olive";
			public static LocString DARK_GREEN = "dark green";
			public static LocString FOREST_GREEN = "forest green";
			public static LocString DEEP_GREEN = "deep green";
			public static LocString DARK_TEAL = "dark teal";
			public static LocString BLACK = "black";
			public static LocString GRAY = "gray";
			public static LocString DODGER_BLUE = "dodger blue";
			public static LocString ROYAL_BLUE = "royal blue";
			public static LocString BLUE_VIOLET = "blue violet";
			public static LocString MAGENTA = "magenta";
			public static LocString CRIMSON = "crimson";
			public static LocString RED_ORANGE = "red orange";
			public static LocString ORANGE = "orange";
			public static LocString DARK_GOLD = "dark gold";
			public static LocString GREEN = "green";
			public static LocString MEDIUM_GREEN = "medium green";
			public static LocString SEA_GREEN = "sea green";
			public static LocString TEAL = "teal";
			public static LocString WHITE = "white";
			public static LocString SKY_BLUE = "sky blue";
			public static LocString CORNFLOWER = "cornflower";
			public static LocString VIOLET = "violet";
			public static LocString ORCHID = "orchid";
			public static LocString HOT_PINK = "hot pink";
			public static LocString SALMON = "salmon";
			public static LocString TANGERINE = "tangerine";
			public static LocString GOLD = "gold";
			public static LocString CHARTREUSE = "chartreuse";
			public static LocString BRIGHT_GREEN = "bright green";
			public static LocString SPRING_GREEN = "spring green";
			public static LocString CYAN = "cyan";
			public static LocString MEDIUM_GRAY = "medium gray";
			public static LocString OFF_WHITE = "off-white";
			public static LocString LIGHT_BLUE = "light blue";
			public static LocString LAVENDER = "lavender";
			public static LocString LIGHT_PURPLE = "light purple";
			public static LocString LIGHT_PINK = "light pink";
			public static LocString LIGHT_ROSE = "light rose";
			public static LocString BEIGE = "beige";
			public static LocString CREAM = "cream";
			public static LocString LIGHT_GOLD = "light gold";
			public static LocString LIGHT_LIME = "light lime";
			public static LocString PALE_GREEN = "pale green";
		}

		// Pixel Pack building side screen and cursor announcement
		public class PIXEL_PACK {
			public static LocString ACTIVE = "active";
			public static LocString STANDBY = "standby";
			public static LocString PALETTE = "color palette";
			public static LocString ACTIVE_COLORS = "active colors";
			public static LocString STANDBY_COLORS = "standby colors";
			// {0} = pixel slot number (1-indexed int)
			public static LocString PIXEL_SLOT = "pixel {0}";
			public static LocString IN_USE = "in use";
			// {0} = number of colors in palette (int)
			public static LocString PALETTE_COUNT = "{0} colors";
		}

		// Door/checkpoint access control side screen
		// Telepad (Printing Pod) side screen badge notifications
		public class TELEPAD {
			public static LocString NEW_ACHIEVEMENTS = "new achievements";
			public static LocString SKILL_POINTS = "skill points available";
		}

		public class ACCESS_CONTROL {
			public static LocString ALLOWED = "allowed";
			public static LocString BLOCKED = "blocked";
			public static LocString LOCKED = "Door locked, access control disabled";
			// Traversal direction labels: named by travel direction
			public static LocString PASS_LEFT = "passing from right to left";
			public static LocString PASS_RIGHT = "passing from left to right";
			public static LocString PASS_UP = "passing from bottom to top";
			public static LocString PASS_DOWN = "passing from top to bottom";
			public static LocString DEFAULT_PASS_LEFT = "default, passing from right to left";
			public static LocString DEFAULT_PASS_RIGHT = "default, passing from left to right";
			public static LocString DEFAULT_PASS_UP = "default, passing from bottom to top";
			public static LocString DEFAULT_PASS_DOWN = "default, passing from top to bottom";
		}

		// Entity details screen (inspecting a selected building, duplicant, or item)
		public class DETAILS {
			public static LocString NO_ERRANDS = "No errands";
			public static LocString SCHEDULE = "Schedule";
			public static LocString CURRENT_TASK = "Current task";
			public static LocString ACTIONS_TAB = "Actions";
			public static LocString NO_ACTIONS = "No actions";
			// "both" as in "both directions" (left and right); used in
			// CURRENT_DIRECTION to describe a building that accepts
			// duplicants from either side (e.g. "Current direction: both.")
			public static LocString DIRECTION_BOTH = "both";
			// {0} = direction word (e.g. "both", "left", "right")
			public static LocString CURRENT_DIRECTION = "Current direction: {0}.";
			public static LocString PRIORITY = "Priority";
			public static LocString PIN_RESOURCE = "Pin resource";
			// {0} = duplicant name, {1} = hat/role name, {2} = skills subtitle
			public static LocString DUPE_HAT_SUBTITLE = "{0}, {1}, {2}";
			// {0} = duplicant name, {1} = skills subtitle
			public static LocString DUPE_SUBTITLE = "{0}, {1}";
			// {0} = entity name, {1} = tab display name
			public static LocString ENTITY_TAB = "{0}, {1}";
			// {0} = parent container/context label, {1} = item speech text
			public static LocString PARENT_ITEM = "{0}, {1}";
			// {0} = section header label, {1} = first item speech text
			public static LocString HEADER_ITEM = "{0}, {1}";
			// {0} = bottom-left coordinates, {1} = top-right coordinates
			public static LocString RANGE = "Range: {0} to {1}";
			// {0} = pathing description
			public static LocString PATHING = "Pathing behaviour: {0}";
			// {0} = raw NavGrid name for unmapped grids
			public static LocString PATHING_UNKNOWN = "{0} (unknown)";

			public class PATHING_DESC {
				public static LocString DUPLICANT = "Is 1 wide by 2 tall. Walks on solid ground and can travel vertically or horizontally on ladders. Can climb ledges up to 2 tiles tall, jump over gaps 1 tile wide, and drop down ledges up to 2 tiles tall. Can also reach a platform 2 tiles away horizontally and 1 tile up, or 1 tile away and 2 tiles up. Falls through larger drops, though will attempt to avoid this.";
				public static LocString WALKER_1X1 = "Occupies 1 tile. Walks on solid ground. Can climb ledges up to 2 tiles tall, cross gaps 1 tile wide, and drop down ledges up to 2 tiles tall.";
				public static LocString WALKER_BABY = "Occupies 1 tile. Walks on solid ground but can only move horizontally on flat surfaces. Cannot climb, jump, or cross gaps, unlike an adult.";
				public static LocString WALKER_1X2 = "Occupies 1 wide by 2 tall. Walks on solid ground. Can climb ledges up to 2 tiles tall, cross gaps 1 tile wide, and drop down ledges up to 2 tiles tall. Needs more vertical clearance than smaller walkers due to its height.";
				public static LocString WALKER_2X2 = "Occupies 2 wide by 2 tall. Walks on solid ground. Can climb or descend 1-tile ledges over a 2-tile horizontal distance, but cannot climb steeper ledges. Needs more clearance than smaller creatures due to its size.";
				public static LocString SURFACE_CLIMBER = "Occupies 1 tile. Walks on solid ground, climbs walls, and walks upside-down on ceilings, transitioning freely around corners between all four surfaces. On the floor, can climb ledges up to 2 tiles tall, cross gaps 1 tile wide, and reach platforms up to 2 tiles away horizontally and 2 tiles up.";
				public static LocString SURFACE_CLIMBER_BABY = "Occupies 1 tile. Walks on solid ground, climbs walls, and walks upside-down on ceilings, transitioning freely around corners between all four surfaces. Cannot climb ledges, jump gaps, or cross drops on the floor, unlike an adult.";
				public static LocString TREE_CLIMBER = "Occupies 1 tile. Walks on solid ground, climbs walls, and walks upside-down on ceilings, transitioning freely around corners between all four surfaces. On the floor, can climb ledges up to 2 tiles tall, cross gaps 1 tile wide, and drop down ledges up to 2 tiles tall.";
				public static LocString FLOATER = "Occupies 1 tile. Hovers 1 tile above the nearest solid surface or liquid below it, following the terrain contour from above. If submerged by rising liquid, can swim to escape.";
				public static LocString FLYER_1X1 = "Occupies 1 tile. Flies freely through open air in any direction. If submerged by rising liquid, can swim to escape.";
				public static LocString FLYER_1X2 = "Occupies 1 wide by 2 tall. Flies freely through open air in any direction. If submerged by rising liquid, can swim to escape. Needs more vertical clearance than smaller flyers.";
				public static LocString FLYER_2X2 = "Occupies 2 wide by 2 tall. Flies freely through open air in any direction. If submerged by rising liquid, can swim to escape. Needs significant clearance to navigate.";
				public static LocString SWIMMER_1X1 = "Occupies 1 tile. Swims through liquid in any direction. On dry land, flops around searching for nearby liquid but cannot walk.";
				public static LocString SWIMMER_2X2 = "Occupies 2 wide by 2 tall. Swims through liquid in any direction. On dry land, flops around searching for nearby liquid but cannot walk. Needs more space to navigate than smaller swimmers.";
				public static LocString DIGGER = "Occupies 1 tile. Walks on solid ground, climbs walls, and walks upside-down on ceilings, transitioning freely around corners between all four surfaces. Can also burrow into soft natural tiles and tunnel through solid material, emerging back onto any surface. Cannot dig through very hard materials or refined metals.";
				public static LocString ROBOT = "Is 1 wide by 2 tall. Walks on solid ground and can travel vertically or horizontally on ladders. Can climb ledges up to 2 tiles tall, jump over gaps 1 tile wide, and drop down ledges up to 2 tiles tall. Can also reach a platform 2 tiles away horizontally and 1 tile up, or 1 tile away and 2 tiles up. Cannot use transit tubes or fire poles.";
				public static LocString ROBOT_FLYER = "Occupies 1 tile. Flies freely through open air in any direction. Can pass through doors. If submerged by rising liquid, can swim to escape.";
			}
		}

		// Table-based screens (priorities, vitals, consumables)
		public class TABLE {
			// {0} = column name
			public static LocString SORT_DESC_FMT = "{0}, sorted high to low";
			public static LocString SORT_ASC_FMT = "{0}, sorted low to high";
			public static LocString SORT_CLEARED_FMT = "{0}, sort cleared";
			// Help entries for table navigation
			public static LocString NAVIGATE_TABLE = "Navigate rows and columns";
			public static LocString JUMP_FIRST_LAST = "Jump to first or last row";
			public static LocString SORT_COLUMN = "Sort by column on header row";
			// Resource storage column header
			public static LocString STORED = "Stored";
		}

		// Priorities table screen
		public class PRIORITY_SCREEN {
			public static LocString HANDLER_NAME = "Priorities table";
			public static LocString TOOLBAR = "Toolbar";
			// {0} = skill level (int) for the chore group
			public static LocString SKILL = "skill {0}";
			// {0} = trait name that disables this chore group
			public static LocString DISABLED_TRAIT = "Disabled, {0}";
			// Announced after adjusting all priorities in a row/column
			public static LocString ROW_INCREASED = "row increased";
			public static LocString ROW_DECREASED = "row decreased";
			public static LocString COLUMN_INCREASED = "column increased";
			public static LocString COLUMN_DECREASED = "column decreased";
			// {0} = priority name (e.g. "Very High")
			public static LocString COLUMN_SET = "column set to {0}";
			public static LocString PROXIMITY_ON = "Proximity, on";
			public static LocString PROXIMITY_OFF = "Proximity, off";
			// {0} = comma-joined list of chore type names
			public static LocString AFFECTED_ERRANDS = "Affected errands: {0}";
			// Help entries
			public static LocString SET_PRIORITY = "Set priority of current cell";
			public static LocString ADJUST_ROW = "Adjust all priorities in row";
			public static LocString ADJUST_COLUMN = "Adjust all priorities in column";
			public static LocString SET_COLUMN = "Set all priorities in column";
			public static LocString ADJUST_CELL = "Adjust priority of current cell";
			public static LocString RESET = "Reset all priorities";
		}

		// Vitals table screen (health, stress, calories)
		public class VITALS_SCREEN {
			public static LocString HANDLER_NAME = "Vitals table";
			// Announced when camera focuses on a duplicant
			public static LocString FOCUSED = "focused";
			public static LocString FOCUS_DUPLICANT = "Focus camera on duplicant";
			// {0} = formatted rate of change per time slice (e.g. "-500 kcal/cycle")
			public static LocString CHANGE = "change: {0}";
			// {0} = formatted calories eaten today (e.g. "1,200 kcal")
			public static LocString EATEN_TODAY = "eaten today: {0}";
			public static LocString FULLNESS_HEADER = "Calories remaining before starvation";
			public static LocString DISEASE_HEADER = "Current diseases and time remaining";
		}

		// Consumables permission table screen
		public class CONSUMABLES_SCREEN {
			public static LocString HANDLER_NAME = "Consumables table";
			// Per-duplicant permission states for a food/medicine item
			public static LocString PERMITTED = "permitted";
			public static LocString FORBIDDEN = "forbidden";
			public static LocString RESTRICTED = "restricted";
			// {0} = morale bonus value with sign (e.g. "+3", "-1")
			public static LocString MORALE = "morale {0}";
			// Column header state when some duplicants differ
			public static LocString MIXED = "mixed";
			public static LocString ALL_PERMITTED = "all permitted";
			public static LocString ALL_FORBIDDEN = "all forbidden";
			// {0} = game's diet restriction explanation text
			public static LocString DIET_RESTRICTED = "restricted, {0}";
			// Help entries
			public static LocString TOGGLE_ALL = "Toggle all duplicants for this consumable";
			public static LocString TOGGLE_PERMISSION = "Toggle permission";
		}

		// Research screen (browse, queue, and tree tabs)
		public class RESEARCH {
			public static LocString HANDLER_NAME = "Research";
			public static LocString BROWSE_TAB = "Browse";
			public static LocString QUEUE_TAB = "Queue";
			// Tab name shared by research and skills tree views
			public static LocString TREE_TAB = "Tree";
			// Tech status labels
			public static LocString AVAILABLE = "available";
			public static LocString LOCKED = "locked";
			public static LocString COMPLETED = "completed";
			public static LocString ACTIVE = "active";
			// {0} = comma-joined prerequisite names (research techs or skills)
			public static LocString NEEDS_FMT = "needs {0}";
			// {0} = comma-joined names of buildings/items unlocked
			public static LocString UNLOCKS_FMT = "unlocks {0}";
			// {0} = prerequisite tech name (appended to mark it complete)
			public static LocString PREREQ_COMPLETED = "{0} completed";
			// {0} = comma-joined research point values with type (e.g. "100 Science, 50 Engineering")
			public static LocString BANKED_POINTS_FMT = "banked research points: {0}";
			// {0} = tech name
			public static LocString QUEUED = "{0} queued";
			// {0} = tech name
			public static LocString CANCELED = "{0} canceled";
			public static LocString QUEUE_CLEARED = "queue cleared";
			public static LocString QUEUE_EMPTY = "no research queued";
			public static LocString NO_BANKED_POINTS = "no banked research points";
			public static LocString DEAD_END = "no further techs";
			// Spoken at the top of a tech or skill tree (no parent nodes)
			public static LocString ROOT_NODE = "no prerequisites";
			// Browse tab bucket/section headers (shared by research and skills screens)
			public static LocString BUCKET_AVAILABLE = "Available";
			public static LocString BUCKET_LOCKED = "Locked";
			public static LocString BUCKET_COMPLETED = "Completed";
			// Help entries
			public static LocString JUMP_TO_TREE_HELP = "Jump to tech in tree view";
			public static LocString QUEUE_CANCEL_HELP = "Select or cancel research";
			public static LocString CANCEL_HELP = "Cancel selected research";
			// {0} = current points (float), {1} = required points (float), {2} = research type name
			public static LocString PROGRESS_ENTRY = "{0} of {1} {2}";
		}

		// Skills screen (duplicant skills, hats, boosters)
		public class SKILLS {
			public static LocString HANDLER_NAME = "Skills";
			// Tab name shared by skills and schedule screens
			public static LocString DUPES_TAB = "Duplicants";
			public static LocString SKILLS_TAB = "Skills";

			// {0} = number of available skill points (int)
			public static LocString POINTS = "{0} points";
			// {0} = current morale (float), {1} = morale expectation (float)
			public static LocString MORALE_OF = "morale {0} of {1}";
			// {0} = hat/role name or NO_HAT
			public static LocString HAT_LABEL = "hat: {0}";
			public static LocString NO_HAT = "no hat";
			// {0} = comma-joined skill group interest names
			public static LocString INTERESTS = "interests: {0}";
			public static LocString NO_INTERESTS = "no interests";
			// {0} = current XP toward next point (float), {1} = XP needed for next point (float)
			public static LocString XP_PROGRESS = "{0} of {1} experience to next point";
			// {0} = hat/role name
			public static LocString HAT_QUEUED = "{0} queued";

			// Browse tab section headers
			public static LocString BUCKET_DUPE_INFO = "Dupe Info";
			public static LocString BUCKET_MASTERED = "Mastered";
			public static LocString BUCKET_BOOSTERS = "Boosters";

			// Skill status labels
			public static LocString AVAILABLE = "available";
			public static LocString MASTERED = "mastered";
			public static LocString LOCKED = "locked";
			// Skill was granted automatically (not learned)
			public static LocString GRANTED = "granted";
			public static LocString MORALE_DEFICIT = "morale deficit";
			// Duplicant has an interest in this skill's group
			public static LocString INTERESTED = "interested";
			public static LocString NO_SKILL_POINTS = "no skill points";
			// {0} = skill name, {1} = status text (mastered/available/locked reason)
			public static LocString NAME_STATUS = "{0}, {1}";
			// Attribute modifier line. {0} = modifier description, {1} = sign ("+" or ""), {2} = value (float)
			public static LocString MODIFIER_LINE = "{0} {1}{2}";
			// Attribute total. {0} = attribute name, {1} = total value (float)
			public static LocString HEADER_TOTAL = "{0} {1}";
			// {0} = duplicant name (in Duplicants tab, marked as stored/available)
			public static LocString NAME_STORED = "{0}, stored";
			// {0} = duplicant name, {1} = formatted points string (from POINTS)
			public static LocString NAME_POINTS = "{0}, {1}";
			// {0} = trait name that blocks learning this skill
			public static LocString BLOCKED_BY = "blocked by {0}";
			// {0} = morale cost (int)
			public static LocString MORALE_NEED = "{0} morale need";
			// {0} = number of duplicants who mastered this skill (int)
			public static LocString MASTERED_BY = "mastered by {0}";
			// {0} = skill name
			public static LocString LEARNED = "{0} learned";
			public static LocString CANNOT_LEARN = "cannot learn";
			public static LocString DEAD_END = "no further skills";

			// Booster slot management
			// {0} = assigned slot count (int), {1} = unlocked slot count (int)
			public static LocString BOOSTER_SLOTS = "{0} of {1} booster slots used";
			// {0} = count (int). Used for boosters assigned and crew member counts
			public static LocString ASSIGNED = "{0} assigned";
			// {0} = number of unassigned boosters available (int)
			public static LocString BOOSTER_AVAILABLE = "{0} available";
			public static LocString BOOSTER_HINT = "use plus to assign, minus to unassign";
			public static LocString BOOSTER_ASSIGNED = "booster assigned";
			public static LocString BOOSTER_UNASSIGNED = "booster unassigned";
			public static LocString NO_BOOSTERS_AVAILABLE = "no boosters available";
			public static LocString NONE_ASSIGNED = "none assigned";
			public static LocString NO_EMPTY_SLOTS = "no empty slots";

			// {0} = hat/role name
			public static LocString HAT_SELECTED = "{0} selected";

			// Help entries
			public static LocString JUMP_TO_TREE_HELP = "Jump to skill in tree view";
			public static LocString LEARN_HELP = "Learn skill or select hat";
			public static LocString BOOSTER_HELP = "Assign or unassign boosters with +/-";
		}

		// Schedule screen (timetable management)
		public class SCHEDULE {
			public static LocString HANDLER_NAME = "Schedule";
			public static LocString SCHEDULES_TAB = "Schedules";
			public static LocString ADD_SCHEDULE = "Add new schedule";
			public static LocString CANNOT_DELETE_LAST = "Cannot delete last schedule";
			public static LocString CANNOT_DELETE_LAST_ROW = "Cannot delete last row";
			public static LocString SCHEDULE_DELETED = "Schedule deleted";
			public static LocString TIMETABLE_ROW_ADDED = "Row added";
			public static LocString TIMETABLE_ROW_DELETED = "Row deleted";
			public static LocString OPTIONS_RENAME = "Rename";
			public static LocString OPTIONS_DUPLICATE = "Duplicate schedule";
			public static LocString OPTIONS_DELETE_SCHEDULE = "Delete schedule";
			public static LocString OPTIONS_ADD_ROW = "Add timetable row";
			public static LocString OPTIONS_DELETE_ROW = "Delete timetable row";
			// {0} = schedule group name (e.g. "Sleep", "Work"), {1} = block/hour number (int, 0-23)
			public static LocString BLOCK_LABEL = "{0}, block {1}";
			// Hours 0–4: Early Bird trait bonus window
			public static LocString MORNING = "morning";
			// Hours 21–23: Night Owl trait bonus window
			public static LocString NIGHT = "night";
			// Announced when painting a block that already has the selected type.
			// {0} = block number (int), {1} = schedule group name
			public static LocString BLOCK_ALREADY = "block {0}, already {1}";
			// {0} = brush/schedule group name (e.g. "Bathtime", "Downtime")
			public static LocString BRUSH_ACTIVE = "Brush: {0}";
			// {0} = schedule group name, {1} = start block number (int), {2} = end block number (int)
			public static LocString PAINTED_RANGE = "Painted {0}, blocks {1} through {2}";
			// {0} = row number (1-indexed int)
			public static LocString ROW_LABEL = "row {0}";
			// {0} = schedule name, {1} = row number (1-indexed int)
			public static LocString SCHEDULE_ROW = "{0}, row {1}";
			public static LocString MOVED_UP = "moved up";
			public static LocString MOVED_DOWN = "moved down";
			// Help entries
			public static LocString HELP_NAVIGATE_BLOCKS = "Navigate blocks";
			public static LocString HELP_JUMP_BLOCK = "Jump to first or last block";
			public static LocString HELP_SELECT_BRUSH = "Select brush";
			public static LocString HELP_PAINT = "Paint current block";
			public static LocString HELP_PAINT_MOVE = "Paint and move";
			public static LocString HELP_PAINT_RANGE = "Paint range to start or end";
			public static LocString HELP_REORDER_SCHEDULE = "Move schedule up or down";
			public static LocString HELP_ROTATE = "Rotate blocks left or right";
			public static LocString HELP_OPTIONS = "Open schedule options";
			public static LocString HELP_CHANGE_SCHEDULE = "Change schedule assignment";
		}

		// Daily report screen
		public class REPORT {
			public static LocString HANDLER_NAME = "Daily report";
			public static LocString COLONY_SUMMARY = "Colony summary";
			// {0} = formatted positive value for the report row
			public static LocString ADDED = "added {0}";
			// {0} = formatted negative value (shown as positive) for the report row
			public static LocString REMOVED = "removed {0}";
			// {0} = formatted net value for the report row
			public static LocString NET = "net {0}";
			// {0} = note description text, {1} = formatted note value
			public static LocString NOTE = "{0} {1}";
			public static LocString HELP_CYCLE = "Previous or next cycle";
			public static LocString NO_LATER_CYCLE = "Latest cycle";
			public static LocString NO_EARLIER_CYCLE = "Earliest cycle";
		}

		// In-game notification menu
		public class NOTIFICATIONS {
			// {0} = notification title text, {1} = count of grouped notifications (int)
			public static LocString GROUP_COUNT = "{0} x{1}";
			// Fallback when a notification member has no name. {0} = group title, {1} = member number (1-indexed int)
			public static LocString NUMBERED_ENTRY = "{0} {1}";
			public static LocString MENU_TITLE = "Notifications";
			public static LocString EMPTY = "no notifications";
			public static LocString DISMISSED = "dismissed";
			public static LocString CANNOT_DISMISS = "cannot dismiss";
			public static LocString OPEN_MENU_HELP = "Open notifications menu";
			public static LocString DISMISS_HELP = "Dismiss notification";
			public static LocString MESSAGE_DIALOG = "Message";
			public static LocString NEXT_MESSAGE = "next message";
			public static LocString DONT_SHOW_AGAIN = "don't show again";
			public static LocString PLAY_VIDEO = "play video";
			// {0} = achievement name
			public static LocString ACHIEVEMENT_EARNED = "achievement earned: {0}";
		}

		// In-game encyclopedia (Codex) browser
		public class CODEX {
			public static LocString CATEGORIES_TAB = "Categories";
			public static LocString CONTENT_TAB = "Content";
			public static LocString NO_ARTICLE = "no article selected";
			// Announced for codex entries that haven't been unlocked yet
			public static LocString LOCKED_CONTENT = "locked content";
			// Recipe input label, spoken before ingredient list (e.g. "requires 100 kg Dirt")
			public static LocString REQUIRES = "requires";
			// Recipe output label, spoken before result list (e.g. "produces 100 kg Fertilizer")
			public static LocString PRODUCES = "produces";
			// Recipe crafting duration label, spoken before a formatted time value (e.g. "time: 30 s")
			public static LocString TIME = "time:";
			// Prefix before the fabricator building name for a recipe (e.g. "made in Rock Crusher")
			public static LocString MADE_IN = "made in";
			// {0} = number of hyperlinks in the article (int)
			public static LocString LINK_MENU = "{0} links";
			// Help entries
			public static LocString FOLLOW_LINK_HELP = "Follow link";
			public static LocString NO_BACK = "nothing to go back to";
			public static LocString NO_FORWARD = "nothing to go forward to";
			public static LocString HISTORY_FORWARD_HELP = "Go forward";
			// Label for the sub-entries line. Followed by a comma-separated list of entry names.
			// Example: "entries: Sweetle, Grubgrub"
			public static LocString SUBENTRIES = "entries";
			// Spoken before the input element in a grouped converter line.
			// Full example: "takes Salt Water, 5 kg/s. produces Water, 4.65 kg/s, input temperature"
			public static LocString TAKES = "takes";
			// Output temperature mode: element is emitted at the same temperature as the input
			// Example: "produces Water, 4.65 kg/s, input temperature"
			public static LocString INPUT_TEMPERATURE = "input temperature";
			// Output temperature mode: element is emitted at the building's own temperature
			// Example: "produces Oxygen, 0.5 kg/s, building temperature"
			public static LocString BUILDING_TEMPERATURE = "building temperature";
			// Output temperature mode: element is emitted at least at a fixed temperature.
			// {0} = formatted temperature value (e.g. "40 °C")
			// Example: "produces Water, 5 kg/s, minimum 40 °C"
			public static LocString MINIMUM_TEMPERATURE = "minimum {0}";
		}

		// Measurement ruler tool on tile cursor
		public class RULER {
			public static LocString PLACED = "ruler set";
			public static LocString CLEARED = "ruler cleared";
			public static LocString HELP_PLACE = "Place ruler at cursor";
			public static LocString HELP_CLEAR = "Clear ruler";
		}

		// Tile cursor bookmarks for quick navigation
		public class BOOKMARKS {
			// {0} = bookmark slot number (1-indexed int)
			public static LocString BOOKMARK_SET = "bookmark {0} set";
			public static LocString NO_BOOKMARK = "no bookmark";
			// Announced when the Printing Pod can't be found
			public static LocString NO_HOME = "no printing pod";
			// Cursor is at the bookmark location. {0} = grid coordinates
			public static LocString ORIENT_HERE = "here. {0}";
			// Cursor is away from the bookmark. {0} = distance/direction text, {1} = grid coordinates
			public static LocString ORIENT_DISTANCE = "{0}. {1}";
			// Help entries
			public static LocString HELP_HOME = "Jump to Printing Pod";
			public static LocString HELP_SET_BOOKMARK = "Set bookmark";
			public static LocString HELP_GOTO_BOOKMARK = "Jump to bookmark";
			public static LocString HELP_ORIENT_BOOKMARK = "Distance to bookmark";
		}

		// Building placement and build menu
		public class BUILD_MENU {
			public static LocString ACTION_MENU = "action menu";
			public static LocString TOOLS_CATEGORY = "Tools";
			// Confirmation after placing a building or object (build tool, place tool)
			public static LocString PLACED = "placed";
			public static LocString PLACED_NO_MATERIAL = "placed, no material available";
			public static LocString NOT_ROTATABLE = "not rotatable";
			public static LocString NOT_BUILDABLE = "not buildable";
			public static LocString CANCELED = "canceled";
			public static LocString CANCEL_CONSTRUCTION = "canceled";
			public static LocString NO_CONSTRUCTION = "nothing to cancel";
			public static LocString MUST_BE_STRAIGHT = "must be a straight line";
			public static LocString INVALID_LINE = "invalid";
			// {0} = number of cells in the line segment (int)
			public static LocString LINE_CELLS = "{0} cells";
			// Wire/pipe start point or pathfinder start set
			public static LocString START_SET = "start set";
			public static LocString START_CLEARED = "start cleared";
			public static LocString INFO_PANEL = "info";
			public static LocString OBSTRUCTED = "obstructed";
			// Material picker entries. {0} = material name, {1} = available quantity
			public static LocString MATERIAL_ENTRY = "{0}, {1}";
			public static LocString MATERIAL_INSUFFICIENT = "{0}, {1}, insufficient";
			// Building footprint extent. {0} = tile count from center (int)
			public static LocString EXTENT_LEFT = "{0} left";
			public static LocString EXTENT_RIGHT = "{0} right";
			public static LocString EXTENT_UP = "{0} up";
			public static LocString EXTENT_DOWN = "{0} down";
			// {0} = direction name (up/down/left/right)
			public static LocString FACING = "facing {0}";
			// Building orientation direction names
			public static LocString ORIENT_UP = "up";
			public static LocString ORIENT_RIGHT = "right";
			public static LocString ORIENT_DOWN = "down";
			public static LocString ORIENT_LEFT = "left";
			public static LocString ORIENT_VERTICAL = "vertical";
			public static LocString ORIENT_HORIZONTAL = "horizontal";
			// Info panel section headers
			public static LocString EFFECTS = "effects";
			public static LocString REQUIREMENTS = "requirements";
			// Material slot summary. {0} = material category, {1} = selected material name, {2} = available quantity
			public static LocString MATERIAL_SLOT = "{0}: {1}, {2}";
			public static LocString MATERIAL_SLOT_INSUFFICIENT = "{0}: {1}, {2}, insufficient";
			// {0} = comma-joined extent directions (e.g. "2 left, 3 up")
			public static LocString EXTENT_FORMAT = "extends {0}";
			// Help entries
			public static LocString HELP_PLACE = "Place building or set start";
			public static LocString HELP_PLACE_AND_EXIT = "Place building and exit";
			public static LocString HELP_ROTATE = "Rotate building";
			public static LocString HELP_ROTATE_REVERSE = "Rotate building backward";
			public static LocString HELP_BUILDING_LIST = "Return to building list";
			public static LocString HELP_INFO = "Open info panel";
			public static LocString HELP_CANCEL_CONSTRUCTION = "Cancel construction at cursor";
			public static LocString HELP_OPEN_ACTION_MENU = "Open action menu";
			// Separator between alternative materials (e.g. "Copper or Gold")
			public static LocString MATERIAL_OR = " or ";
			// Info panel detail lines. {0} = comma-joined attribute name-value pairs
			public static LocString ATTRIBUTES_FMT = "attributes: {0}";
			// {0} = comma-joined attribute modifiers from chosen material
			public static LocString MATERIAL_EFFECTS_FMT = "material effects: {0}";
			// {0} = facade/skin name
			public static LocString FACADE_FMT = "facade: {0}";
			// {0} = comma-joined room category labels this building contributes to
			public static LocString CATEGORY_FMT = "category: {0}";
			// {0} = building description text
			public static LocString DESCRIPTION_FMT = "description: {0}";
			// {0} = descriptor type label, {1} = comma-joined descriptor values
			public static LocString DESCRIPTOR_FMT = "{0}: {1}";
			// {0} = building announcement, {1} = pre-build error description
			public static LocString PREBUILD_ERROR = "{0}, {1}";
			// Attribute display. {0} = attribute name, {1} = value
			public static LocString ATTR_VALUE = "{0} {1}";
			// Material attribute modifier. {0} = attribute name, {1} = sign ("+" or ""), {2} = modifier value
			public static LocString ATTR_MODIFIER = "{0} {1}{2}";
			public static LocString FACADE_DEFAULT = "default";
			public static LocString HELP_PORTS = "ports";
			public static LocString NO_PORTS = "no ports";
			// {0} = port name, {1} = offset description
			public static LocString PORT_AT = "{0}, {1}";
			public static LocString RECT_MODE_ON = "rectangle mode";
			public static LocString RECT_MODE_OFF = "single mode";
			public static LocString RECT_MODE_UNAVAILABLE = "rectangle mode not available";
			// {0} = number placed (int), {1} = priority level (int or "emergency")
			public static LocString CONFIRM_BUILD_RECT = "placed {0}, priority {1}";
			public static LocString HELP_RECT_MODE = "toggle rectangle mode";
		}

		// Cursor skip (jump to next tile change)
		public class SKIP {
			// {0} = number of tiles skipped (int), {1} = "tile" or "tiles"
			public static LocString COUNT_FORMAT = "{0} {1}";
			public static LocString TILE_SINGULAR = "tile";
			public static LocString TILE_PLURAL = "tiles";
			public static LocString NO_CHANGE_BOUNDARY = "no change till map boundary";
			public static LocString HELP_SKIP = "Skip cursor to next change";
			public static LocString HELP_SKIP_DEFAULT = "Skip by building or element (ignore overlay)";
		}

		// Colony diagnostics readout and browser
		public class DIAGNOSTICS {
			public static LocString NO_ALERTS = "no alerts";
			// Opinion words spoken when message is empty
			public static LocString OPINION_CRITICAL = "critical";
			public static LocString OPINION_BAD = "bad";
			public static LocString OPINION_WARNING = "warning";
			public static LocString OPINION_CONCERN = "concern";
			public static LocString OPINION_SUGGESTION = "suggestion";
			public static LocString OPINION_NORMAL = "normal";
			public static LocString OPINION_GOOD = "good";
			// Pin state labels
			public static LocString PIN_ALWAYS = "always";
			public static LocString PIN_ALERT_ONLY = "alert only";
			public static LocString PIN_NEVER = "never";
			public static LocString PIN_TUTORIAL_DISABLED = "tutorial disabled";
			// Help entries
			public static LocString HELP_READ = "Read diagnostic alerts";
			public static LocString HELP_OPEN_BROWSER = "Open diagnostics browser";
			public static LocString HELP_TOGGLE_PIN = "Cycle pin state";
			public static LocString HELP_TOGGLE_CRITERION = "Toggle criterion";
		}

		// World selector (Spaced Out DLC world list)
		public class WORLD_SELECTOR {
			// Prepended to the world name when it is the currently viewed asteroid
			public static LocString ACTIVE_LABEL = "active";
			public static LocString OPEN = "Open world list";
			// World type label for rocket interior worlds (e.g. "Voyager, rocket")
			public static LocString ROCKET = "rocket";
			// {0} = world name
			public static LocString DISCOVERED = "{0} discovered";
		}

		// Disinfect threshold settings (germ overlay sidebar panel)
		public class DISINFECT_SETTINGS {
			// Toggle label: spoken as "auto-disinfect, on" or "auto-disinfect, off"
			public static LocString AUTO_DISINFECT = "auto-disinfect";
			// Text input label: spoken as "threshold, 10000 Germs"
			public static LocString THRESHOLD_INPUT = "threshold";
			// Help screen entry for Shift+G
			public static LocString HELP_OPEN = "Open disinfect settings";
		}

		// Starmap screen (base game and Spaced Out DLC cluster map)
		public class STARMAP {
			// Announced when the starmap opens (both base game and DLC cluster map)
			public static LocString HANDLER_NAME = "Starmap";
			public static LocString ROCKETS_TAB = "Rockets";
			public static LocString DESTINATIONS_TAB = "Destinations";
			public static LocString DETAILS_TAB = "Destination details";
			// {0} = rocket name
			public static LocString DESTINATIONS_TAB_WITH_ROCKET = "assigning to {0}";
			public static LocString NO_ROCKETS = "no rockets";
			public static LocString NO_DESTINATIONS = "no destinations";
			public static LocString NO_DESTINATION_SELECTED = "no destination selected";
			// {0} = destination name, {1} = rocket name
			public static LocString DESTINATION_ASSIGNED = "{0} assigned to {1}";
			// {0} = rocket name
			public static LocString LAUNCHED = "{0} launched";
			public static LocString NO_ROCKET_SELECTED = "no rocket selected";
			public static LocString ROCKET_NOT_GROUNDED = "rocket not grounded";
			// Announced when a telescope is actively analyzing this destination
			public static LocString ANALYZING_THIS = "telescope analyzing";
			// Confirmation when the player begins telescope analysis
			public static LocString ANALYSIS_STARTED = "analysis started";
			// Confirmation when the player pauses telescope analysis
			public static LocString ANALYSIS_SUSPENDED = "analysis suspended";
			// {0} = research opportunity description, {1} = data point value
			public static LocString RESEARCH_COMPLETE = "complete: {0}, {1} points";
			public static LocString RESEARCH_INCOMPLETE = "incomplete: {0}, {1} points";
			// Variants when a rare resource has been discovered at the destination
			public static LocString RESEARCH_COMPLETE_RARE = "complete: rare {0}, {1} points";
			public static LocString RESEARCH_INCOMPLETE_RARE = "incomplete: rare {0}, {1} points";
			// Parenthetical note when the rocket has a cargo bay for this resource (e.g. "Fossil (can carry)")
			public static LocString CAN_CARRY = "can carry";
			// Prefix when a rocket lacks the required cargo bay. Followed by ": {bay name}"
			public static LocString NEEDS_BAY = "needs";
			// Suffix after a formatted cycle count for rocket travel time (e.g. "2.5 Cycles remaining")
			public static LocString REMAINING = "remaining";
			// Suffix after a percentage for mission/research progress (e.g. "40% complete")
			public static LocString COMPLETE = "complete";
			// Launch checklist status prefixes
			public static LocString CHECK_READY = "ready";
			public static LocString CHECK_WARNING = "warning";
			public static LocString CHECK_NOT_READY = "not ready";
			// Help entries
			public static LocString LAUNCH_HELP = "Launch rocket";
		}

		// Cluster map (Spaced Out DLC starmap) handler
		public class CLUSTER_MAP {
			public static LocString SELECT_OBJECT = "select object";
			public static LocString SELECT_DESTINATION = "select destination";
			// {0} = path length
			public static LocString DESTINATION_SET = "destination set, {0} hexes";
			public static LocString DESTINATION_CANCELLED = "destination cancelled";
			// {0} = hex distance, {1} = compass direction
			public static LocString HEX_COORDINATES = "{0} {1}";
			// {0} = path length
			public static LocString PATH_RESULT = "path: {0}";
			// {0} = total path, {1} = fog count
			public static LocString PATH_THROUGH_FOG = "path: {0}, {1} through fog";
			// {0} = total path, {1} = fog count, {2} = visible-only path
			public static LocString PATH_FOG_WITH_ALT = "path: {0}, {1} through fog. Without scanner: {2}";
			public static LocString NO_PATH = "no path";
			public static LocString SET_START_FIRST = "set start with space";
			public static LocString UNSEEN = "unseen";

			// Compass directions for hex coordinate reading
			public class COMPASS {
				public static LocString NORTH = "north";
				public static LocString NORTHEAST = "northeast";
				public static LocString EAST = "east";
				public static LocString SOUTHEAST = "southeast";
				public static LocString SOUTH = "south";
				public static LocString SOUTHWEST = "southwest";
				public static LocString WEST = "west";
				public static LocString NORTHWEST = "northwest";
			}

			// Scanner categories for cluster map entities
			public class CATEGORIES {
				public static LocString ALL = "All";
				public static LocString ASTEROIDS = "Asteroids";
				public static LocString ROCKETS = "Rockets";
				public static LocString POIS = "Points of interest";
				public static LocString METEORS = "Meteor showers";
				public static LocString UNKNOWN = "Unknown";
			}

			// Help entries
			public class HELP {
				public static LocString HEX_MOVE = "Move cursor on hex grid";
				public static LocString HEX_MOVE_ARROWS = "Move cursor (diagonal varies by row)";
				public static LocString READ_COORDS = "Read hex coordinates";
				public static LocString READ_TOOLTIP = "Read entity details";
				public static LocString SELECT_ENTITY = "Select entity";
				public static LocString SWITCH_WORLD = "Switch to asteroid";
				public static LocString PATHFIND_START = "Set pathfinder start";
				public static LocString PATHFIND_CALC = "Calculate path to cursor";
				public static LocString JUMP_HOME = "Jump to active world";
			}
		}

		// Resource browser and pinned resource readout
		public class RESOURCES {
			public static LocString BROWSER_TITLE = "Resources";
			public static LocString NO_PINNED = "no pinned resources";
			public static LocString PINNED = "pinned";
			public static LocString UNPINNED = "unpinned";
			public static LocString ALL_UNPINNED = "all unpinned";
			public static LocString NO_INSTANCES = "none available";
			// Resource amount qualifiers
			public static LocString RESERVED = "reserved";
			public static LocString AVAILABLE = "available";
			// Announced when reserved amount exceeds total
			public static LocString OVERDRAWN = "overdrawn";
			// Resource trend direction
			public static LocString RISING = "rising";
			public static LocString FALLING = "falling";
			// {0} = resource name
			public static LocString DISCOVERED = "Discovered: {0}";
			// {0} = amount, {1} = building name, {2} = coordinates
			public static LocString INSTANCE_IN_BUILDING = "{0} in {1} at {2}";
			// {0} = amount, {1} = coordinates
			public static LocString INSTANCE_LOOSE = "{0} at {1}";
			// Help entries
			public static LocString HELP_PIN = "Pin or unpin resource";
			public static LocString HELP_CLEAR_PINS = "Unpin all resources";
			public static LocString HELP_JUMP = "Jump to instance location";
			public static LocString HELP_OPEN = "Open resource browser";
			public static LocString HELP_READ_PINNED = "Read pinned resources";
		}

		// Side screen spoken labels
		public class SIDESCREENS {
			// Appended to rocket pilot info when an auto-pilot module is active
			public static LocString COPILOT_ROBO = "Copilot: Robo-Pilot";
		}

		// Sandbox tool mode
		public class SANDBOX {
			public static LocString TOOLS_ON = "sandbox tools on";
			public static LocString TOOLS_OFF = "sandbox tools off";
			public static LocString TOOLS_CATEGORY = "Sandbox Tools";
			public static LocString PARAM_MENU = "sandbox parameters";
			public static LocString SAMPLE = "sampled";
			public static LocString TOOL_FALLBACK = "sandbox tool";
			// {0} = cell count (int)
			public static LocString APPLIED = "applied to {0} cells";
			public static LocString APPLIED_ONE = "applied";

			public class HELP {
				public static LocString SET_CORNER = "Set rectangle corner or place";
				public static LocString CONFIRM = "Apply and dismiss";
				public static LocString OPEN_PARAMS = "Open parameter menu";
				public static LocString SAMPLE = "Sample cell under cursor";
			}
		}
	}
}
