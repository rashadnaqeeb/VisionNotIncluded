# Oxygen Not Included — Codebase Index

Decompiled from `Assembly-CSharp.dll` (4,230 files) and `Assembly-CSharp-firstpass.dll` (2,446 files).

---

## Assembly-CSharp (Main Game Logic)

### Named Namespaces

#### `Klei.AI` — Runtime AI state: attributes, amounts, traits, effects, sicknesses, and disease definitions attached to game objects.
- **Amount** — A tracked numeric stat with min/max/delta sub-attributes (e.g. Calories, Stamina, Stress)
- **AmountInstance** — Live per-object value of an `Amount`, attached to a `GameObject`
- **Amounts** — Component holding all `AmountInstance` objects on a `GameObject`
- **Attribute** — A named stat that can be trained and modified (e.g. Construction, Digging)
- **AttributeInstance** — Live per-object value of an `Attribute`
- **AttributeLevel** — Tracks XP and level for a trainable `Attribute`
- **AttributeLevels** — Component holding all `AttributeLevel` entries on a `GameObject`
- **AttributeModifier** — A delta/multiplier applied to an `Attribute` (used by effects, traits, sicknesses)
- **AttributeConverter** — Converts one attribute's value into a modifier on another attribute
- **AttributeConverters** — Component holding all `AttributeConverterInstance` objects on a `GameObject`
- **Modifier** — Abstract base for `Trait`, `Effect`, and `PlantMutation`; holds a list of `AttributeModifier`
- **Modifiers** — Component that aggregates `Amounts`, `Attributes`, and `Sicknesses` on a `GameObject`
- **Modifications\<T,I\>** — Generic list container for modifier instances; base for `Amounts`, `Sicknesses`
- **ModifierGroup\<T\>** — Named group of modifiers, used to categorise traits
- **Trait** — A permanent modifier applied to a duplicant at spawn or via skill; can disable chore groups
- **TraitGroup** — Named group of `Trait` objects, used for spawn/non-spawn categorisation
- **Traits** — Serializable component holding a duplicant's active trait list
- **Effect** — A temporary timed `Modifier` with optional emote, icon, and sound
- **EffectInstance** — Live running instance of an `Effect` on a `GameObject`
- **Effects** — Component managing all active `EffectInstance` objects, ticks duration each second
- **Sickness** — Abstract base for diseases (FoodSickness, SlimeSickness, etc.); defines infection vectors
- **SicknessInstance** — Live running instance of a `Sickness` on a `GameObject`
- **Sicknesses** — Component managing all active `SicknessInstance` objects on a `GameObject`
- **Disease** — Abstract base for germ types (FoodGerms, SlimeGerms, etc.); defines cell-level growth rules
- **Emote** — Named animation sequence played on a duplicant to express a mood
- **EmoteStep** — A single animation step within an `Emote`
- **GameplaySeason** — A timed cycle of gameplay events (meteor showers, random events, etc.)
- **GameplaySeasonInstance** — Live running instance of a `GameplaySeason`
- **PlantMutation** — A modifier applied to a plant that changes its stats or behaviour
- **BonusEvent** — A conditional gameplay event triggered by building use, achievements, etc.
- **StoryTraitStateMachine** — Abstract base state machine for story trait progression
- **Allergies** — Concrete sickness (inhaled, causes sneezing/stress); example of `Sickness` implementation

#### `Klei.AI.DiseaseGrowthRules` — Rules controlling how germs grow, diffuse, and die in simulation cells.
- **GrowthRule** — Base rule specifying growth/death rates and diffusion for elements matching a predicate
- **CompositeGrowthRule** — Aggregated rule combining multiple `GrowthRule` overlays into final values
- **ElemGrowthInfo** — Per-element struct holding the resolved growth parameters used by the sim
- **ElementGrowthRule** — `GrowthRule` that matches by a specific `SimHashes` element
- **StateGrowthRule** — `GrowthRule` that matches by element state (solid/liquid/gas)
- **TagGrowthRule** — `GrowthRule` that matches elements by tag
- **ExposureRule** — Rule controlling how quickly germs infect duplicants per element type
- **CompositeExposureRule** — Aggregated `ExposureRule` combining overlays into final exposure values
- **ElemExposureInfo** — Per-element struct holding resolved exposure half-life values
- **ElementExposureRule** — `ExposureRule` that matches by a specific `SimHashes` element

#### `Database` — The central game database (`Db`) containing all resource collections accessed at runtime.
- **Amounts** — All stat amounts (Stamina, Calories, Stress, Breath, Temperature, HitPoints, etc.)
- **Attributes** — Duplicant skill attributes (Construction, Digging, Cooking, Art, Ranching, etc.)
- **BuildingAttributes** — Building-specific attributes (Decor, DecorRadius, NoisePollution, OverheatTemperature)
- **CritterAttributes** — Critter-specific attributes (Happiness, Metabolism)
- **PlantAttributes** — Plant-specific attributes (YieldAmount, HarvestTime, MinLightLux, etc.)
- **Sicknesses** — All disease types (FoodSickness, SlimeSickness, ZombieSickness, Allergies, RadiationSickness, Sunburn)
- **Diseases** — All germ types (FoodGerms, SlimeGerms, PollenGerms, ZombieSpores, RadiationPoisoning)
- **Deaths** — All death causes (Generic, Frozen, Suffocation, Starvation, Slain, Overheating, etc.)
- **ChoreTypes** — All chore type definitions (Attack, Sleep, Eat, Doctor, WashHands, etc.)
- **SkillGroups** — Skill tree branch definitions (Mining, Building, Farming, Cooking, Art, Research, etc.)
- **Skills** — All individual skills with tier, perks, and prerequisites
- **Techs** — All research tree nodes with tier costs and unlocks
- **TechItems** — Specific tech-unlocked items (automation overlay, suit types, etc.)
- **Personalities** — Duplicant personality definitions loaded from CSV (head shape, attributes, etc.)
- **RoomTypes** — All room type definitions (Barracks, Bedroom, MessHall, Laboratory, etc.)
- **ScheduleGroups** — Schedule block types (Worktime, Hygene, Recreation, Sleep)
- **ColonyAchievements** — All colony achievement definitions and their requirement checklists
- **ColonyAchievement** — A single achievement with requirements, victory flag, and video name
- **GameplaySeasons** — All seasonal event schedules (meteor showers, random events, etc.)
- **GameplayEvents** — All individual gameplay events (HatchSpawn, Party, Eclipse, MeteorShowers, etc.)
- **Expressions** — Duplicant facial expression states (Neutral, Happy, Hot, Cold, Angry, etc.)
- **Thoughts** — Duplicant thought bubble definitions (Starving, Hot, Cold, FullBladder, etc.)
- **Emotes** — Collections of duplicant and critter emote animations
- **Dreams** — Sleep dream definitions used during rest
- **Urges** — AI urge priorities (Sleep, Eat, Pee, WashHands, HealCritical, etc.)
- **FertilityModifiers** — Breeding probability modifiers per critter tag
- **PlantMutations** — All plant mutation definitions
- **SpaceDestinationTypes** — Space destination categories (Satellite, Asteroid types, Planets, etc.)
- **PermitResources** — All cosmetic permit resources (BuildingFacades, ClothingItems, ClothingOutfits, ArtableStages, etc.)
- **Accessories** — Individual duplicant body part sprites loaded from KAnim builds
- **AccessorySlots** — Named slots for duplicant accessories (Eyes, Hair, Hat, Body, Neck, Belt, etc.)
- **Stories** — Story trait definitions (MegaBrainTank, LonelyMinion, FossilHunt, etc.)
- **Quests** — Quest definitions for story scenarios

#### `Klei.CustomSettings` — Custom game settings system: configs and levels for difficulty and sandbox options.
- **SettingConfig** — Abstract base for a single game setting; has id, label, tooltip, default level
- **ToggleSettingConfig** — A two-state on/off `SettingConfig`
- **ListSettingConfig** — A multi-level `SettingConfig` with an ordered list of `SettingLevel` values
- **SeedSettingConfig** — A numeric seed `SettingConfig` for world generation
- **MixingSettingConfig** — Base for DLC mixing settings
- **DlcMixingSettingConfig** — DLC-specific mixing setting
- **SubworldMixingSettingConfig** — Subworld mixing probability setting
- **WorldMixingSettingConfig** — World-level mixing setting
- **SettingLevel** — A named option within a `SettingConfig`, with label, tooltip, and coordinate value
- **CustomGameSettingConfigs** — Static class holding all built-in `SettingConfig` instances (CalorieBurn, SandboxMode, StressBreaks, etc.)
- **CustomMixingSettingsConfigs** — Static class holding all world/subworld mixing setting configs

#### `Klei.Actions` — Action pattern framework for tool actions (dig, build, etc.) using a factory pattern.
- **ActionFactory\<TFactory, TAction, TEnum\>** — Generic factory that creates and caches action instances keyed by enum value
- **ActionTypeAttribute** — Attribute applied to action enum values to associate metadata
- **ActionAttribute** — Attribute describing an action's properties
- **DigToolActionFactory** — Concrete factory for dig-tool actions, with an `Actions` enum of dig modes

#### `Klei.Input` — Input tool configuration assets for the interface tool system.
- **InterfaceToolConfig** — `ScriptableObject` specifying priority and dig-action for an interface tool mode

#### `Klei` — Save/load data structures and simulation utilities used across the game.
- **SaveFileRoot** — Top-level save file structure (dimensions, active mods, streamed data blobs)
- **ClusterLayoutSave** — Cluster-level save data including per-world data, story traits, and POI types
- **Data** — World generation data (seeds, terrain cells, rivers, world layout, spawn data)
- **WorldGenSave** — Serialized world-gen result (version, `Data`, world ID, trait IDs)
- **SimUtil** — Static utility for simulation math (energy flow, disease info struct, disease colour helpers)
- **SolidInfo** — Struct describing a solid tile's element and mass
- **TerrainCellLogged** — Serialized record of a terrain cell for save/debug logging
- **WorldDetailSave** — Per-world detail data persisted in the save file

#### `KMod` — Mod loading, management, and Steam/local distribution platform integration.
- **Mod** — Represents an installed mod with status, label, file source, and error tracking
- **Manager** — Core mod manager: loads, enables/disables mods, tracks mod events and errors
- **UserMod2** — Base class for mod entry points; override `OnLoad(Harmony)` to apply patches
- **Content** — Flags enum for mod content types (DLL, Strings, Animation, Translation, LayerableFiles)
- **Label** — Identifies a mod across platforms (id, title, version, `DistributionPlatform`)
- **IFileSource** — Interface for a mod's file system (directory, zip, or Steam package)
- **IDistributionPlatform** — Interface for a mod host platform (Steam, local, Epic)
- **Steam** — `IDistributionPlatform` implementation using the Steam Workshop/UGC API
- **Local** — `IDistributionPlatform` implementation for mods in the local mods folder
- **Directory** — `IFileSource` implementation backed by a filesystem directory
- **ZipFile** — `IFileSource` implementation backed by a `.zip` archive
- **DLLLoader** — Internal loader that finds and loads `ModLoader.dll` from the Managed folder
- **Event** — Struct recording a mod lifecycle event (install, error, update) with type and details
- **EventType** — Enum of mod event types (LoadError, Installed, Uninstalled, VersionUpdate, etc.)
- **Testing** — Static class with enums for simulating DLL load and save/load failures during testing
- **KModUtil** — Utility for reading mod headers and metadata from file sources
- **KModHeader** — Struct holding a mod's parsed header (title, staticID, description)
- **LoadedModData** — Runtime data for a loaded mod (assembly, user mod instance)

#### `ProcGen` — Data model for procedural world generation layout (read-only reference structures).
- **WorldLayout** — Full voronoi/graph layout for a world, including leaf cells, rivers, and overworld graph

#### `ProcGenGame` — Runtime world generation execution: terrain placement, mob spawning, template spawning.
- **WorldGen** — Orchestrates full world generation (noise, terrain, biomes, features, spawning)
- **GameSpawnData** — Collected spawn data (buildings, pickupables, ores, entities) to place after gen
- **TerrainCell** — A voronoi cell with element overrides, feature tags, and temperature data
- **MobSpawning** — Static utility that places critters and ambient mobs within terrain cell cavities
- **TemplateSpawning** — Places pre-built template structures (POIs, starting biomes) into the world
- **WorldGenSimUtil** — Utilities for applying sim cell data during world generation
- **WorldgenMixing** — Handles DLC world/subworld mixing during generation
- **WorldgenException** — Exception type thrown on world generation failure
- **Cluster** — Data structure representing the full asteroid cluster for cluster-mode generation
- **Neighbors** — Utility tracking neighbour relationships between voronoi cells
- **River** — Data structure for a river path between voronoi nodes
- **SymbolicMapElement** — A named element placeholder in a symbolic world map

#### `Rendering` — High-level tile renderers for built and under-construction block tiles.
- **BlockTileRenderer** — `MonoBehaviour` rendering built/replacement/under-construction block tiles with neighbour bitmask logic
- **IBlockTileInfo** — Interface for block tile atlas and mesh data
- **BackWall** — (`rendering` namespace, lowercase) `MonoBehaviour` applying a `Texture2DArray` to the back-wall material

#### `Rendering.World` — Low-level mesh and brush system for rendering world tiles.
- **TileRenderer** — Abstract `KMonoBehaviour` managing a grid of `Tile` objects and dirty/active `Brush` lists
- **LiquidTileOverlayRenderer** — Concrete `TileRenderer` for liquid surface overlay tiles
- **DynamicMesh** — Manages a set of `DynamicSubMesh` objects that grow as tiles are added
- **DynamicSubMesh** — A single Unity `Mesh` within a `DynamicMesh`, rebuilt when dirty
- **Brush** — Groups tiles sharing a material into a single mesh draw call
- **Mask** — UV coordinates for a single atlas texture entry used to texture a tile face
- **SourceMask** — Source atlas reference for constructing a `Mask`
- **Tile** — Struct holding a tile's grid index and cell coordinates
- **TileCells** — Struct holding the four corner cell indices of a tile

#### `STRINGS` — All localised game text, organised by game domain; every field is a `LocString`.
- **BLUEPRINTS** — Strings for the blueprints/print pod screen
- **BUILDING** — Strings for generic building UI labels (status, tooltips)
- **BUILDINGS** — Per-building names, descriptions, and effect text for every building in the game
- **CLUSTER_NAMES** — Names and descriptions for asteroid cluster presets
- **CODEX** — Codex entry titles and body text for in-game encyclopedia articles
- **COLONY_ACHIEVEMENTS** — Achievement names, descriptions, and victory messages
- **CREATURES** — Names, descriptions, stats, and disease text for every critter
- **DUPLICANTS** — Duplicant trait names, disease names, attribute names, and morale strings
- **ELEMENTS** — Names and descriptions for every element (gases, liquids, solids)
- **EQUIPMENT** — Strings for equipment items (suits, tools, toys)
- **GAMEPLAY_EVENTS** — Names and descriptions for random gameplay events and seasons
- **INPUT** — Player-facing input and control hint strings
- **INPUT_BINDINGS** — Display names for every key binding action
- **ITEMS** — Strings for miscellaneous inventory items
- **LORE** — Lore codex entry body text
- **MISC** — Miscellaneous shared strings (notifications, tooltips, common labels)
- **NAMEGEN** — Name generation lists (duplicant first/last names, colony names)
- **RESEARCH** — Research tech tree node names and descriptions
- **ROBOTS** — Names, descriptions, and stat strings for robot duplicants/droids
- **ROOMS** — Room type names, descriptions, and requirements text
- **SEARCH_TERMS** — Additional search keywords attached to buildings and items
- **SETITEMS** — Names and descriptions for set/themed cosmetic items
- **STICKERNAMES** — Display names for sticker bomb permit items
- **SUBWORLDS** — Names and descriptions for procedural sub-biomes
- **UI** — All UI widget labels, button text, screen titles, overlay names, tooltips, and error messages
- **VIDEOS** — Titles and descriptions for in-game tutorial/lore videos
- **WORLDS** — Names and descriptions for asteroid/world presets
- **WORLD_TRAITS** — Names and descriptions for world trait modifiers

#### `TUNING` — All numerical game balance constants, organised by game system.
- **AUDIO** — Material and size category strings for audio event routing
- **BUILDINGS** — Building-specific constants (overheat temperatures, overpressure tiers, fabrication times)
- **CREATURES** — Creature hit points, mass, calorie, and temperature tiers
- **CROPS** — Plant growth rate constants and harvest timing values
- **CUSTOMGAMESETTINGS** — Numeric values for custom game setting levels (hunger, stress, disease rates)
- **DECOR** — Decor bonus and penalty `EffectorValues` tiers with radii
- **DISEASE** — Disease duration tiers and immune attack strength values
- **DUPLICANTSTATS** — All duplicant base stats, attribute levelling tables, radiation thresholds, movement modifiers
- **EQUIPMENT** — Equipment slot names, attribute modifier IDs, and toy constants
- **FIXEDTRAITS** — Fixed world trait identifiers (e.g. northern lights settings)
- **FOOD** — Food spoil time tiers and `FoodInfo` definitions for every food type
- **GAMEPLAYEVENT** — Season period tiers for gameplay event scheduling
- **GERM_EXPOSURE** — Germ exposure thresholds, inhale tick counts, and exposure type definitions
- **INDUSTRIAL** — Standard fabrication time constants
- **ITEMS** — Bionic upgrade power cost tiers
- **LIGHT2D** — Light colour constants and default direction for 2D lights
- **MATERIALS** — Material category tag constants (Metal, Glass, Plastic, BuildableRaw, etc.)
- **MEDICINE** — Medicine mass, work time, and `MedicineInfo` definitions for every medicine
- **METEORS** — Meteor shower difficulty multipliers (period, density, count)
- **NOISE_POLLUTION** — Noise duration and `EffectorValues` tier constants
- **OVERLAY** — Legend entry definitions for temperature, disease, and other overlay modes
- **PLANTS** — Plant mass and radiation threshold tiers
- **POWER** — Power system float fudge factor
- **RADIATION** — Radiation emitter pulse rates, radius scales, and rads-per-second tiers
- **RELAXATION** — Recreation priority tier values
- **ROBOTS** — Robot (Scoutbot) stat constants (carry capacity, battery, digging, etc.)
- **ROLES** — Skill experience scale constants and attribute bonus values
- **ROCKETRY** — Rocket destination research and analysis point values
- **SKILLS** — Skill XP curve parameters, morale costs per tier, and experience portion constants
- **SORTORDER** — Sort order integers for inventory categories (food, seeds, eggs, elements)
- **STORAGE** — Storage locker filled margin constant
- **STORAGEFILTERS** — Pre-built `Tag` filter lists for storage buildings (FOOD, BAGABLE_CREATURES, etc.)
- **STRESS** — Stress acting-out constants (shocker radius, vomit amount, banshee wail radius)
- **TRAITS** — Joy reaction constants (balloon count, sticker duration, super productive chance)

#### `TemplateClasses` — Serializable data classes for pre-built world template structures (POIs, starting areas).
- **Cell** — A single sim cell in a template (element, mass, temperature, disease, position)
- **Prefab** — A single entity placed by a template (type, position, amounts, storage items)
- **StorageItem** — An item stored inside a template `Prefab`'s storage (element, units, rottable)
- **Rottable** — Rot amount data attached to a `StorageItem`

#### `ImGuiObjectDrawer` — Debug ImGui drawers for reflecting arbitrary C# objects in the in-game debug UI.
- **ArrayDrawer** — Draws arrays as collapsible element lists
- **CollectionDrawer** — Abstract base for collection drawers (arrays, lists, dictionaries)
- **EnumDrawer** — Draws enum values as labelled fields
- **FallbackDrawer** — Catches types no other drawer handles
- **HashedStringDrawer** — Draws `HashedString` values showing both hash and resolved string
- **IDictionaryDrawer** — Draws `IDictionary` types as key-value pair lists
- **IEnumerableDrawer** — Draws `IEnumerable` types as element lists
- **SimpleDrawer** — Draws primitives and strings as plain labelled fields
- **NullDrawer** — Renders a `[null]` label for null references
- **UnityObjectDrawer** — Draws `UnityEngine.Object` references with name and type

#### `FoodRehydrator` — Components for the Food Rehydrator building (dehydrated food dispensing).
- **AccessabilityManager** — Manages reservation and workable access for the rehydrator
- **DehydratedManager** — Main building logic: tracks chosen food tag, storage meters, and copy-settings support
- **ResourceRequirementMonitor** — Watches storage levels and sets the `HasSufficientResources` operational flag

#### `EventSystem2Syntax` — Internal prototype/stub of a typed event system (not used in production game).
- **GameHashes** — Minimal internal enum (only `ObjectDestroyed`)
- **IEventData** — Marker interface for typed event payloads

#### `TMPro` — Game-local TextMeshPro input validators and event helpers.
- **TMP_DigitValidator** — `TMP_InputValidator` that accepts only digit characters
- **TMP_PhoneNumberValidator** — `TMP_InputValidator` that accepts phone number format characters
- **TMP_TextEventHandler** — MonoBehaviour demonstrating TMP link/word click callbacks

#### `TMPro.Examples` — TextMeshPro example and benchmark scripts (not used in production game).
- **Benchmark01 / Benchmark01_UGUI / Benchmark02 / Benchmark03 / Benchmark04** — Performance benchmarks
- **TMP_ExampleScript_01** — Demonstrates TMP string formatting and colour tags
- **TMP_FrameRateCounter / TMP_UiFrameRateCounter** — On-screen FPS display using TMP
- **TMP_TextInfoDebugTool** — Visualises TMP character/word/line bounding boxes
- **TextMeshProFloatingText** — Spawns floating damage/notification numbers
- **VertexColorCycler / VertexJitter / VertexShakeA / VertexShakeB / VertexZoom** — Vertex manipulation effects
- **WarpTextExample / SkewTextExample** — Text deformation examples

#### `UnityEngine.EventSystems` — Game-local extension to Unity's event system for controller/virtual input.
- **VirtualInputModule** — `PointerInputModule` that accepts synthetic pointer and button events from `IInputHandler`

---

### Global Namespace (No Namespace Declaration — ~3,795 files)

The majority of game code lives in the global namespace, categorised below by function.

#### Building Configs (~443 `IBuildingConfig` implementations)
Each class defines a constructible building: cost, size, ports, and behavior.
- **AirConditionerConfig** — chills gas piped through it by 14 degrees C, consumes 240W
- **AlgaeDistilleryConfig** — converts slime into algae and polluted water
- **ElectrolyzerConfig** — splits water into oxygen and hydrogen via electrolysis
- **BedConfig** — basic sleep station that restores duplicant morale
- **EggIncubatorConfig** — incubates critter eggs and accelerates hatching
- **FarmTileConfig** — tilled tile for growing crops with bonus yield
- **GeneratorConfig** — coal-burning power generator
- **HydrogenEngineConfig** — rocket engine burning hydrogen fuel
- **RocketControlStationConfig** — duplicant workstation for piloting rockets
- **SolarPanelConfig** — generates power from light exposure
- **SteamTurbineConfig** — generates power from steam input
- **StorageLockerConfig** — stores loose materials up to a configured mass limit
- **SuitFabricatorConfig** — fabricates exosuits from refined materials
- **TravelTubeConfig** — pneumatic tube segment duplicants can transit at high speed
- **WaterPurifierConfig** — converts polluted water to clean water
- **ResearchCenterConfig** — workstation for researching technologies
- **GasPumpConfig** — moves gas through pipe networks
- **SweepBotStationConfig** — charging dock and home for automated sweep bots
- **NuclearReactorConfig** — generates large amounts of power from enriched uranium
- *(~423 more)*

#### Creature Configs (~111 `IEntityConfig` implementations)
Each class defines a creature and its baby/egg variants.
- **HatchConfig** — surface-dwelling omnivore that excretes coal; variants: HatchHard, HatchMetal, HatchVeggie
- **PuftConfig** — floating gas-breather that excretes slime; variants: PuftAlpha, PuftBleachstone, PuftOxylite
- **DreckoConfig** — ranching creature that sheds fiber; variant: DreckoPlastic
- **PacuConfig** — aquatic fish; variants: PacuCleaner, PacuTropical, PrehistoricPacu
- **OilFloaterConfig** — floats on liquid and excretes oil; variants: OilFloaterDecor, OilFloaterHighTemp
- **LightBugConfig** — bioluminescent insect; color variants: Black, Blue, Crystal, Orange, Pink, Purple
- **MooConfig** — large gas-producing bovine creature; variant: DieselMoo
- **SquirrelConfig** — small surface critter that harvests plants; variant: SquirrelHug
- **CrabConfig** — hard-shelled critter; variants: CrabFreshWater, CrabWood
- **StaterpillarConfig** — large caterpillar-like creature that generates power; variants: StaterpillarGas, StaterpillarLiquid
- **StegoConfig** — creature that grazes on plants; variant: AlgaeStego
- **RaptorConfig** — fast predatory creature
- **SealConfig** — aquatic mammal-like critter
- **BeeConfig** — colonial insect managed via BeeHive structure
- **MosquitoConfig** — pest insect with larva stage (MosquitoLarvaConfig)
- *(~96 more, including all Baby\* variants and light-bug color variants)*

#### Item / Equipment / Consumable Configs (~95 `IEntityConfig` implementations)
- **AdvancedCureConfig** — advanced medicine that cures major diseases
- **BasicCureConfig** — basic medicine for minor ailments
- **AntihistamineConfig** — medicine that suppresses allergic reactions
- **BasicRadPillConfig** — radiation protection medicine; variant: IntermediateRadPill
- **BasicFabricConfig** — raw textile material produced from farm crops; variant: FeatherFabric
- **FarmStationToolsConfig** — tool item consumed by farm station upgrades
- **PowerStationToolsConfig** — tool item consumed by power station upgrades
- **ElectrobankConfig** — rechargeable power cell for bionic duplicants; variants: GarbageElectrobank, SelfChargingElectrobank
- **MissileBasicConfig** — basic rocket missile payload; variant: MissileLongRange
- **AtmoSuitConfig** — oxygen suit for hazardous environments
- **JetSuitConfig** — jetpack for vertical traversal
- **LeadSuitConfig** — radiation protection suit
- **MushBarConfig** — basic low-quality food made from mushrooms
- **BerryPieConfig** — cooked meal from mealwood berries
- **PemmicanConfig** — preserved trail ration food
- *(~80 more)*

#### UI Screens & Dialogs (~103 `KScreen` / `KModalScreen` subclasses)
- **MainMenu** — the game's main menu screen
- **LoadScreen** — modal dialog for loading a saved colony
- **SaveScreen** — modal dialog for saving the current game
- **CharacterSelectionController** — duplicant selection/customization screen at game start
- **SkillsScreen** — modal screen for managing duplicant skills and job assignments
- **ResearchScreen** — modal screen for the research tree
- **ScheduleScreen** — screen for managing duplicant daily schedules
- **ColonyDiagnosticScreen** — colony health diagnostic panel
- **ReportScreen** — end-of-cycle report screen
- **StarmapScreen** — modal starmap for the base-game rocket system
- **ClusterMapScreen** — cluster-mode map for the Spaced Out DLC
- **CodexScreen** — in-game codex/lore browser
- **ConfirmDialogScreen** — generic modal yes/no confirmation dialog
- **ModsScreen** — modal screen for managing installed mods
- **VictoryScreen** — modal victory/win condition screen
- **GameOverScreen** — modal game-over screen
- **PriorityScreen** — duplicant task priority management screen
- **HoverTextScreen** — overlay screen that renders hover/tooltip text on the world
- **NotificationScreen** — notification log and alert panel
- **SideDetailsScreen** — right-side panel showing details for the selected entity
- **PrinterceptorScreen** — screen for the colony Printingceptor (care package printer)
- **OverlayLegend** — HUD overlay legend panel showing current overlay key
- *(~79 more)*

#### Side Screens (~83 `SideScreenContent` subclasses)
Context-sensitive panels shown when selecting buildings/entities.
- **ValveSideScreen** — sets flow rate on pipe valves
- **ThresholdSwitchSideScreen** — configures sensor threshold for automation switches
- **TemperatureSwitchSideScreen** — sets temperature threshold on temperature sensors
- **TimerSideScreen** — configures on/off timing for timer automation
- **TreeFilterableSideScreen** — element/tag filter tree (used on storage, filters, etc.)
- **ComplexFabricatorSideScreen** — recipe queue management for fabricators
- **AssignableSideScreen** — assigns a building or item to a specific duplicant
- **ReceptacleSideScreen** — configures plant/seed/egg receptacles (planters, incubators)
- **CapacityControlSideScreen** — sets storage capacity limits
- **DoorToggleSideScreen** — sets access permissions (open/locked/auto) on doors
- **PlayerControlledToggleSideScreen** — generic on/off toggle for player-controlled buildings
- **SingleSliderSideScreen** — single-value slider for configurable buildings
- **IntSliderSideScreen** — integer slider variant for whole-number settings
- **LoreBearerSideScreen** — shows lore/story text for objects that carry lore entries
- **ArtableSelectionSideScreen** — selects artwork style for decorative buildings
- **MinionTodoSideScreen** — shows and manages a duplicant's current errand queue
- **RocketModuleSideScreen** — configures rocket module settings
- **LogicBroadcastChannelSideScreen** — sets the broadcast channel for logic transmitters
- *(~65 more)*

#### Building Components (~605 `KMonoBehaviour` subclasses)
Behavior scripts attached to building/entity GameObjects.
- **AirConditioner** — manages heat exchange for HVAC buildings
- **Battery** — stores and supplies electrical power
- **Building** — base component on every placed building; holds def, orientation, and cell footprint
- **ConduitConsumer** — pulls gas/liquid from a pipe network into a building
- **ConduitDispenser** — pushes gas/liquid output from a building into a pipe network
- **Generator** — produces electrical power and tracks wattage
- **Operational** — tracks whether a building is enabled, has power, and has workers
- **Pump** — moves gas or liquid through conduit networks
- **Refrigerator** — keeps stored food cold to prevent spoilage
- **SmartReservoir** — large storage tank with logic port support
- **SolidConduitConsumer** — receives packaged solids from conveyor rails
- **SolidConduitDispenser** — dispatches packaged solids onto conveyor rails
- **StorageLocker** — stores items in a defined area; base for most storage buildings
- **Switch** — togglable on/off automation switch
- **Turbine** — generates power from steam pressure differential
- **Workable** — marks an entity as having a work task; base for all interactive buildings
- **Storage** — general-purpose item container component
- **Assignable** — workstation/bed assignment
- **Pickupable** — item that can be carried by duplicants
- *(~585 more)*

#### Game Systems & Managers (~197 singleton/manager classes)
Central coordinators for game subsystems.
- **Game** — root MonoBehaviour for an active colony; owns the main update loop
- **BuildingConfigManager** — registers and indexes all `IBuildingConfig` definitions at startup
- **EntityConfigManager** — instantiates all `IEntityConfig` prefabs and registers them with the game
- **CircuitManager** — tracks all power circuits, loads, and generator output each tick
- **ClusterManager** — manages the cluster map, asteroid worlds, and inter-world travel
- **ComplexRecipeManager** — central registry for all multi-step fabrication recipes
- **LogicCircuitManager** — simulates automation wire networks and propagates signal values
- **ChoreGroupManager** — owns the list of chore groups and maps them to skill categories
- **GameFlowManager** — drives top-level game state (new game, load, colony lost, etc.)
- **GameplayEventManager** — fires and tracks story/gameplay events during a colony
- **NotificationManager** — queues and displays in-game alert notifications
- **ReportManager** — aggregates per-cycle statistics for the reports screen
- **ScheduleManager** — owns all colony work/sleep schedules and their block definitions
- **SpacecraftManager** — tracks rockets, their missions, and cargo manifests
- **StoryManager** — enables and tracks DLC story trait missions
- **YellowAlertManager** — triggers and clears colony-wide yellow alert state
- **SaveLoader** — save/load orchestration
- **PopFXManager** — spawns floating pop-up text effects over entities
- *(~177 more)*

#### State Machines (~610 `GameStateMachine` subclasses)
Define complex entity behavior as state graphs.
- **AgeMonitor** — tracks creature aging and triggers death-by-old-age transition
- **AlertStateManager** — drives the colony-wide alert level state (green/yellow/red)
- **AttackChore** — duplicant attack sequence: approach, wind-up, strike, recover
- **AutoMiner** — cycles the auto-miner between scanning, digging, and idle states
- **BabyMonitor** — tracks whether a critter is in baby state and handles maturation
- **BeIncapacitatedChore** — duplicant incapacitation and rescue wait loop
- **WildnessMonitor** — tracks tame/wild status transitions for critters
- **WarmBlooded** — regulates creature body temperature against environment
- **WorkChore** — generic duplicant work task: travel to workable, perform work, finish
- **AirFilter** — runs the air filter building through idle/working/depleted states
- **AlgaeHabitat** — manages the algae terrarium through fill, produce, and empty states
- **AquaticCreatureSuffocationMonitor** — detects when an aquatic critter is out of water
- **BalloonArtistChore** — state machine for the balloon artist recreational activity
- **WarpPortal** — drives the warp portal building through charging and transit states
- *(~595 more)*

#### Logic / Automation (~38 classes)
- **LogicCircuitManager** — simulates and updates all automation wire networks each tick
- **LogicCircuitNetwork** — represents one connected automation circuit and its current signal
- **LogicGate** — base for all combinatorial logic gates (AND, OR, NOT, XOR)
- **LogicGateBase** — abstract gate with input/output port wiring and signal propagation
- **LogicGateBuffer** — single-bit signal delay/buffer gate
- **LogicGateFilter** — passes only specific bit-masked signals through
- **LogicPorts** — component that exposes input and output automation ports on a building
- **LogicSwitch** — manual toggle switch that drives an automation signal high or low
- **LogicWire** — single-bit wire segment carrying a green/red signal between ports
- **LogicRibbonReader** — reads a 4-bit ribbon and exposes each bit as a separate output
- **LogicRibbonWriter** — combines up to 4 single-bit inputs into a 4-bit ribbon signal
- **LogicCounter** — counts rising-edge pulses and outputs high when a threshold is reached
- **LogicMemory** — SR latch; holds its last set/reset signal
- **LogicAlarm** — triggers an audible/visual alarm when its input goes high
- **LogicBroadcaster / LogicBroadcastReceiver** — send and receive named wireless signals
- **LogicCritterCountSensor** — outputs a signal based on critter count in a room
- **LogicDiseaseSensor** — outputs a signal when germ concentration exceeds a threshold
- **LogicDuplicantSensor** — outputs a signal when a duplicant is in range
- **LogicElementSensor** — detects a specific gas or liquid element in the cell
- **LogicHEPSensor** — outputs a signal based on radbolt particle count
- **LogicLightSensor** — outputs a signal based on ambient light level
- **LogicMassSensor** — outputs a signal based on mass of gas or liquid in the cell
- **LogicPressureSensor** — outputs a signal based on gas/liquid pressure threshold
- **LogicRadiationSensor** — outputs a signal based on radiation level
- **LogicTemperatureSensor** — outputs a signal based on cell temperature threshold
- **LogicTimeOfDaySensor** — outputs a signal during a configured time-of-day window
- **LogicTimerSensor** — square-wave oscillator; toggles output on a configurable period
- **LogicWattageSensor** — outputs a signal based on circuit wattage draw
- **LogicOperationalController** — enables/disables a building based on its logic input signal
- **LogicClusterLocationSensor** — outputs a signal based on rocket cluster-map position

#### Core Utility & Infrastructure
- **Grid** — static class encoding the tile grid; cell indexing, adjacency, and element lookups
- **GameTags** — global `Tag` constant definitions for every entity, material, and category
- **SimHashes** — integer hash constants for every element known to the simulation
- **Element** — data class describing a material: phase, thermal properties, state changes
- **ElementLoader** — parses `elements/*.yaml` and populates the `ElementTable` at startup
- **Tag** — lightweight interned string wrapper used as a universal entity/item label
- **Db** — central database owning all `Techs`, `Traits`, `Skills`, `Effects`, and `StatusItems`
- **Game** — root MonoBehaviour for an active colony; owns the main update loop
- **GameClock** — tracks in-game time, current cycle number, and time-of-day fraction
- **SaveGame** — attached to the save-game root object; serializes and deserializes colony state
- **World** — represents one asteroid world: its worldgen data, zone map, and biome info
- **Assets** — global registry mapping prefab names and tags to loaded `GameObject` prefabs
- **Components** — typed component registries (e.g., `Components.Workables`) for fast entity queries
- **BuildingDef** — data record for a building type: size, cost, power, conduit ports, and tech
- **BuildingTemplates** — helper methods for creating building definitions
- **EntityTemplates** — helper methods for creating entity definitions
- **ConduitFlow** — simulates mass/temperature/disease flow through gas and liquid pipe networks
- **Storage** — general-purpose item container component; used by most buildings and duplicants
- **Workable** — base component marking an entity as having an interactable work task
- **Operational** — component tracking whether a building is fully functional

---

## Assembly-CSharp-firstpass (Engine, Libraries, Platform SDKs)

### Global Namespace (326 files) — Core engine utilities: animation, input, audio, UI framework, and data structures.

#### Animation & Graphics
- **KAnim** — Core animation data types: `Anim`, `Frame`, `PlayMode`, `SymbolFlags`; defines the ONI animation model
- **KAnimFile** — `ScriptableObject` wrapping raw animation bytes and textures for a single anim asset
- **KAnimFileData** — Runtime parsed animation data (frames, build index, batch tags) produced from a `KAnimFile`
- **KAnimGroupFile** — `ScriptableObject` grouping named animation sets by `HashedString` id and render type
- **KAnimBatch** — GPU instancing batch for a set of animated sprites; holds per-symbol GPU data slots
- **KAnimBatchGroup** — Manages a group of batches sharing the same atlas texture cache and renderer type
- **KAnimBatchManager** — Singleton managing all active batches, organized into spatial chunks for culling
- **KAnimConverter** — Interface and helpers for converting animation state into GPU instance data
- **TextureAtlas** — `ScriptableObject` mapping named sprite items to UV boxes and mesh data
- **TexturePage / TexturePagePool / TextureBuffer** — Runtime atlas page management and pooling for batched sprite rendering

#### Input & Control
- **KKeyCode** — Extended key code enum that mirrors `UnityEngine.KeyCode` and adds Klei-specific virtual keys
- **BindingEntry** — Serializable struct pairing a `KKeyCode`, modifier, action, and gamepad button for a single key binding
- **GameInputMapping** — Loads, saves, and queries the full set of key bindings from `keybindings.json`
- **KInputController** — Stateful input handler that maps raw key events to game `Action` values and dispatches `KButtonEvent`s
- **KInputHandler** — Routing layer that forwards `KButtonEvent`s to a priority-ordered chain of registered screen handlers
- **KButtonEvent** — Carries a single button-press event through the handler chain; supports `TryConsume()` to claim it
- **SteamInputInterpreter** — Translates Steam Input analogue and digital actions into game actions

#### Audio
- **Audio** — `ScriptableObject` singleton holding global FMOD listener configuration
- **SoundDescription** — Struct describing an FMOD event path, falloff distance, and named parameter list for a sound
- **SoundListenerController** — MonoBehaviour singleton that positions the FMOD listener and controls the looping VCA
- **ButtonSoundPlayer** — Serializable component wired to a `KButton`; maps click/hover/reject events to FMOD event paths
- **ToggleSoundPlayer / WidgetSoundPlayer** — Sound player variants for toggle and generic widget events

#### Application & Platform
- **App** — MonoBehaviour singleton managing scene loading, crash reporting, focus state, and quit flow
- **SteamManager** — MonoBehaviour that initialises Steamworks.NET, holds all ONI Steam app IDs, and manages DLC detection
- **SteamDistributionPlatform** — Implements `DistributionPlatform.Implementation` over Steamworks
- **ThreadedHttps\<T\>** — Thread-safe HTTP client base for background service calls
- **AsyncLoadManager\<T\>** — Parallel asset loader that runs typed `AsyncLoader` workers on a thread pool
- **DlcManager** — DLC availability and content restriction management

#### UI Components
- **KScreen** — Base MonoBehaviour for all ONI UI screens; manages canvas, sort order, activation, and input handler registration
- **KScreenManager** — Singleton maintaining the ordered stack of active `KScreen` instances and routing input to them
- **KButton** — Klei-flavoured button extending Unity pointer events with sound, visual states, and an interactability flag
- **KToggle** — `UnityEngine.UI.Toggle` subclass with Klei sound player and art-extension hooks
- **KSlider** — `UnityEngine.UI.Slider` subclass with per-event FMOD sounds (start, move, end, boundary)
- **KScrollRect** — `UnityEngine.UI.ScrollRect` subclass that plays scroll sounds and exposes velocity
- **ToolTip** — KMonoBehaviour component providing configurable tooltip text, position, and dynamic content callbacks
- **ToolTipScreen** — Screen-level singleton that receives tooltip requests and renders them at the correct canvas layer
- **DetailScreenTabHeader / TabHeaderIcon** — Tab header component pair that resizes tabs on selection

#### Utility & Data Structures
- **Tag / TagManager / TagSet** — Hashed-string label system used throughout the game to categorise entities, elements, and world features
- **Strings / StringTable / StringKey / StringEntry** — Hierarchical localized string lookup
- **HashedString** — Serializable struct pairing a string with a stable integer hash for fast comparison
- **BinaryHeap\<T\>** — Generic min/max-heap with configurable `IComparer`
- **ContainerPool\<T\> / StackPool\<T\>** — Object pool implementations to reduce GC pressure
- **Util** — Static utility class with path helpers, string sanitisation, random access, colour math, and reflection helpers
- **KRandom** — Seeded Lehmer-style pseudo-random number generator
- **StateMachineUpdater** — Singleton managing bucketed per-frame update scheduling for all state machine instances
- **SimAndRenderScheduler** — Coordinates simulation ticks (5/s) and render sub-ticks (60/s)
- **UpdateManager** — Constants defining simulation tick rates (5 Hz sim, 12 sub-ticks per sim tick)
- **CPUBudget** — Profiles CPU load across a tree of `ICPULoad` nodes and rebalances thread counts at runtime
- **TuningData\<T\> / TuningSystem** — Generic accessor for per-type balance tuning structs loaded from YAML
- **AxialI / AxialUtil** — Axial-coordinate integer struct for hexagonal grid arithmetic
- **CellOffset** — Integer (x, y) grid offset struct with direction constants

---

### `Klei` (13 files) — File system abstraction, YAML I/O, CSV parsing, profiling, and game-settings utilities.
- **FileSystem** — Static virtual file system with a priority-ordered list of `IFileDirectory` sources
- **IFileDirectory** — Interface implemented by every file source (disk, zip, memory, alias)
- **RootDirectory** — `IFileDirectory` backed by the real OS file system at the game's data root
- **ZipFileDirectory** — `IFileDirectory` backed by a `DotNetZip` archive; used for mods and DLC packages
- **MemoryFileDirectory** — `IFileDirectory` backed by an in-memory `byte[]` dictionary
- **AliasDirectory** — `IFileDirectory` that maps a virtual path prefix to a concrete root
- **FileUtil** — Static helpers for safe file read/write/delete with logging
- **FileHandle** — Lightweight struct pairing a file path with its source directory for deferred reading
- **CSVReader** — Parallel CSV parser that splits lines on a thread pool and returns jagged string arrays
- **YamlIO** — YAML serialization/deserialization wrapper around YamlDotNet with structured error reporting
- **GenericGameSettings** — Singleton holding developer/QA overrides loaded from JSON
- **MeshCreator** — Static helper that generates a flat `Mesh` for a given width x height tile grid
- **KProfilerPlugin** — P/Invoke bridge to `SimDLL` for native Klei profiler integration

### `KSerialization` (18 files) — Binary serialization framework for save-game persistence, using opt-in attribute marking and type templates.
- **Manager** — Central registry; builds and caches templates per type, maps type names for versioned save compatibility
- **Serializer** — Writes an object to a `BinaryWriter` using its `SerializationTemplate`
- **Deserializer** — Reads an object from an `IReader`, matches saved type string, and applies a `DeserializationMapping`
- **SerializationTemplate** — Reflection-built description of which fields/properties to serialize for a type
- **DeserializationTemplate** — Describes the on-disk field layout by name and `TypeInfo`
- **DeserializationMapping** — Maps each `DeserializationTemplate` field name to a live field/property on the current type
- **TypeInfo** — Holds a type's `SerializationTypeInfo` flags plus generic type arguments
- **SerializationTypeInfo** — Flags enum encoding whether a type is a value, array, list, dictionary, or user-defined type
- **MemberSerialization** — Enum (`OptOut`, `OptIn`, `Fields`) controlling which members are serialized by default
- **SerializationConfig** — `[Attribute]` placed on a class/struct to set its `MemberSerialization` mode
- **Serialize** — `[Attribute]` marking individual fields or properties for inclusion when using `OptIn` mode
- **Helper** — Static utilities for type-info bit manipulation and `BinaryWriter`/`IReader` extension methods
- **IOHelper** — Low-level binary I/O helpers (Klei string format, primitive packing)
- **BoundaryTag** — Sentinel value written to the stream to delimit serialized object boundaries

### `ProcGen` (52 files) — World generation data model: world/cluster definitions, biome and terrain band settings, feature placement, mob spawning, and weighted random utilities.
- **SettingsCache** — Static registry that loads and holds all world gen YAML assets
- **WorldGenSettings** — Runtime configuration for a single world generation run
- **World** — YAML-deserialized descriptor for one asteroid: size, subworld list, feature placements, traits
- **ClusterLayout** — YAML descriptor for a full cluster: ordered world placements, starting asteroid
- **ClusterLayouts / Worlds** — Collection wrappers that load and index all layout/world YAML files by name
- **SubWorld** — YAML descriptor for a biome region: zone type, temperature, features, mob and element bands
- **WorldTrait** — Optional modifier applied to a world at game start
- **MutatedWorldData** — Runtime-assembled combination of a `World` with its selected trait overrides applied
- **BiomeSettings / TerrainElementBandSettings** — Element band configurations used during terrain painting
- **Feature / FeatureSettings** — Describes a named feature type (geysers, ruins, POIs)
- **Room** — Procedural room descriptor: shape, size ranges, tags, and element overrides
- **Mob / MobSettings** — Critter spawn descriptor: location type, weight, and element mass overrides
- **WeightedRandom** — Static utility that performs weighted random selection from any `IWeighted` list
- **Graph\<N,A\>** — Generic seeded graph used as the skeleton for Voronoi-based world layout
- **Temperature** — Named temperature range enum used in subworld and element band specs
- **WorldGenTags** — Static registry of all `Tag` constants used during world generation

### `ProcGen.Map` (4 files) — Voronoi map graph types representing the spatial skeleton of a generated world.
- **MapGraph** — Specialised `Graph<Cell, Edge>` that additionally stores `Corner` vertices
- **Cell** — Graph node representing one Voronoi polygon (a world region)
- **Edge** — Graph arc connecting two adjacent `Cell`s with bounding `Corner` vertices
- **Corner** — 2D `Vector2` vertex at the junction of three or more Voronoi cells

### `ProcGen.Noise` (14 files) — Node-graph noise pipeline for procedural terrain: wires LibNoiseDotNet primitives through filters, modifiers, combiners, selectors, and transformers.
- **Tree** — Runtime noise graph; holds named dictionaries of all node types and resolves them into a connected `IModule3D` pipeline
- **NoiseTreeFiles** — Loads and caches named `Tree` instances from YAML files
- **NoiseBase** — Abstract base for all noise graph nodes
- **SampleSettings** — Root node specifying zoom, normalisation, seamless tiling, and coordinate bounds
- **Primitive** — Leaf node wrapping a LibNoiseDotNet primitive generator (Perlin, Billow, Voronoi, etc.)
- **Filter** — Node applying a fractal filter to one input module
- **Modifier** — Node applying a unary modifier (Abs, Clamp, ScaleBias, Curve, Terrace, etc.)
- **Combiner** — Node combining two input modules with a binary operation (Add, Multiply, Max, Min, Power)
- **Selector** — Node blending or selecting between two inputs based on a control module
- **Transformer** — Node spatially transforming the sample point (Displace, Turbulence, RotatePoint)

---

### Third-Party Libraries

#### `ClipperLib` (25 files) — 2D polygon clipping library supporting boolean operations (union, intersection, difference, XOR) and polygon offsetting.
- **Clipper** — main engine performing clip operations on subject and clip polygons
- **ClipperBase** — base class managing edge tables and local minima lists
- **ClipperOffset** — offsets polygon outlines by a given delta using join/end type strategies
- **PolyTree / PolyNode** — hierarchical output structure representing nested polygons with parent-child holes
- **IntPoint / IntRect** — integer coordinate types used throughout

#### `Delaunay` (15 files) — Fortune's algorithm implementation for Voronoi diagrams and Delaunay triangulations in 2D.
- **Voronoi** — main entry point; computes Voronoi diagrams and Delaunay triangulations from a set of 2D sites
- **Site** — input point with optional weight for weighted diagrams
- **SiteList** — sorted collection of sites processed by the sweep line
- **Edge** — a Voronoi edge between two sites
- **Halfedge / HalfedgePriorityQueue** — internal sweep-line data structures
- **Triangle** — Delaunay triangle formed by three sites
- **DelaunayHelpers** — static helpers for spanning trees, hull extraction, and graph construction

#### `Delaunay.Geo` (4 files) — 2D geometric primitives supporting the Delaunay/Voronoi library.
- **LineSegment** — line segment between two nullable Vector2 endpoints
- **Circle** — circle defined by center and radius
- **Polygon** — polygon with winding-order utilities

#### `Satsuma` (65 files) — General-purpose graph theory library providing algorithms for shortest paths, flows, matching, spanning trees, TSP, and connected components.
- **IGraph / IArcLookup** — core interfaces for graph traversal and arc lookup
- **CustomGraph / Subgraph / Supergraph** — mutable and derived graph implementations
- **Dijkstra / BellmanFord / AStar / Bfs / Dfs** — shortest-path and search algorithms
- **Kruskal / Prim** — minimum spanning tree algorithms
- **MaximumMatching / MinimumCostMatching** — bipartite matching algorithms
- **Preflow / IntegerPreflow / NetworkSimplex** — network flow algorithms
- **ConnectedComponents / StrongComponents / BiEdgeConnectedComponents** — connectivity analysis

#### `MIConvexHull` (23 files) — N-dimensional convex hull, Delaunay triangulation, and Voronoi mesh computation using the quickhull algorithm.
- **ConvexHull / ConvexHullAlgorithm** — incremental convex hull computation
- **ConvexFace / DefaultConvexFace** — a face (simplex) of the convex hull with adjacency
- **DelaunayTriangulation** — builds a Delaunay triangulation as a set of triangulation cells
- **VoronoiMesh / VoronoiEdge** — dual Voronoi mesh derived from the triangulation

#### `FuzzySharp` (47 files total) — Fuzzy string matching library (port of Python fuzzywuzzy) providing Levenshtein-based similarity ratios and best-match extraction.
- **Fuzz** — static facade with `Ratio`, `PartialRatio`, `TokenSortRatio`, `TokenSetRatio` methods
- **Process** — extracts best-matching strings from a collection against a query
- **Levenshtein** — core edit-distance computation
- Sub-namespaces: `Edits`, `SimilarityRatio.Scorer.*`, `SimilarityRatio.Strategy.*`, `Extractor`, `PreProcess`, `Utils`

#### `Geometry` (5 files) — 2D axis-aligned rectangle subtraction utilities using a sweep-line strip decomposition.
- **RectangleUtil** — subtracts one KRect from another, producing a list of remainder rectangles
- **KRect** — axis-aligned rectangle with min/max Vector2 corners

#### `HUSL` (1 file) — HUSL perceptually uniform colour space converter.
- **ColorConverter** — static conversion methods for RGB, XYZ, LUV, and HUSL colour spaces

#### `VoronoiTree` (6 files) — Hierarchical Voronoi space-partitioning tree used for procedural world generation.
- **Tree** — internal node holding child nodes; seeds and relaxes a Voronoi partition
- **Node** — base class for tree nodes with site, polygon, and child relationships
- **Leaf** — terminal node representing a final region
- **PowerDiagram / PowerDiagramSite** — weighted Voronoi (power diagram) computation

#### `NodeEditorFramework` (39 files total) — Unity-based visual node graph editor framework for building and rendering node canvases.
- **NodeEditor** — static core managing the active canvas and editor state
- **NodeCanvas** — serializable asset holding a collection of nodes and connections
- **Node** — base class for all user-defined node types with input/output knobs
- **NodeInput / NodeOutput / NodeKnob** — typed connection ports on nodes

#### `YamlDotNet` (209 files total) — Full-featured YAML 1.1 parser, emitter, and serializer/deserializer for .NET.
- **Serializer / Deserializer** — high-level serialise/deserialise API
- **SerializerBuilder / DeserializerBuilder** — fluent configuration builders
- **Scanner / Parser / Emitter** — low-level YAML parsing and output
- **YamlDocument / YamlNode / YamlScalarNode / YamlSequenceNode / YamlMappingNode** — document object model
- Sub-namespaces: `Core`, `Core.Events`, `Core.Tokens`, `RepresentationModel`, `Serialization.*`, `Helpers`, `Samples`

#### `UnityEngine.UI.Extensions` (4 files) — Community extensions to Unity UI adding line renderer and primitive drawing.
- **UILineRenderer** — UI graphic component drawing multi-point lines with configurable thickness
- **BezierPath** — utility generating smooth Bezier curve point arrays

#### `UnityStandardAssets.ImageEffects` (4 files) — Unity Standard Assets camera image effects.
- **PostEffectsBase** — base MonoBehaviour for camera image effects with shader/material validation
- **ColorCorrectionLookup** — applies a 3D LUT colour correction effect to the camera output

---

### Platform SDKs

#### `rail` (476 files) — Tencent Rail SDK for the WeGame (Chinese) distribution platform.
Covers friends, DLC ownership, achievements/stats, leaderboards, voice channels, in-game browser, cloud storage, anti-addiction timers, and networking. Not relevant for modding.

#### `Epic.OnlineServices` (1,075 files across 19 sub-namespaces) — Epic Online Services (EOS) SDK providing cross-platform social and backend services.
Root namespace defines shared infrastructure: `EpicAccountId`, `ProductUserId`, `Result`, `Helper`. Sub-namespaces: `Achievements`, `Auth`, `Connect`, `Ecom`, `Friends`, `Leaderboards`, `Lobby`, `Logging`, `Metrics`, `P2P`, `Platform`, `PlayerDataStorage`, `Presence`, `Sessions`, `Stats`, `TitleStorage`, `UI`, `UserInfo`. Not relevant for modding.

---

## Key Patterns for Modders

| Pattern | Description |
|---|---|
| `IBuildingConfig` | Implement to define a new building (~443 implementations) |
| `IEntityConfig` | Implement to define a new creature, item, or equipment (~206 implementations) |
| `*Config` suffix | Convention for entity/building definition classes |
| `KMonoBehaviour` | Base class for all game components (replaces Unity MonoBehaviour) |
| `GameStateMachine<,,>` | Base class for state machine definitions (~610 implementations) |
| `SideScreenContent` | Base class for context-sensitive UI panels (~83 implementations) |
| `KScreen` / `KModalScreen` | Base classes for full-screen UI (~103 implementations) |
| `Db.Get()` | Singleton access to all `Database` content |
| `GameTags` | Static tag constants for filtering and matching |
| `SimHashes` | Enum of all simulation elements |
| `STRINGS` | All localisable text constants (28 top-level classes) |
| `TUNING` | All balance/tuning constants (34 top-level classes) |
| `KMod.UserMod2` | Base class for mod entry points |
| `Harmony (0Harmony.dll)` | Patching framework used for runtime method hooking |
| `KScreen.Activate()` / `OnActivate()` | Screen lifecycle — called when screen becomes active |
| `KScreen.Show()` / `OnShow()` | Screen lifecycle — called when screen is shown/hidden |
| `KButton` / `KToggle` / `KSlider` | Klei UI components with sound and input integration |
| `ConduitConsumer` / `ConduitDispenser` | Standard pipe input/output components on buildings |
| `LogicPorts` | Expose automation input/output ports on a building |
| `Storage` | General-purpose item container used by most buildings |
| `Operational` | Tracks whether a building is fully functional |
| `Tag` / `HashedString` | Lightweight identifiers used for filtering and entity lookup |
| `ComplexRecipeManager` | Central registry for all fabrication recipes |
| `BuildingDef` | Data record for a building type: size, cost, power, conduit ports |
| `Components.*` | Typed component registries for fast entity queries |
