using System.Collections.Generic;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.Scanner.Routing {
	/// <summary>
	/// Routes non-tile, non-utility buildings to scanner category/subcategory.
	/// Built once from TUNING.BUILDINGS.PLANORDER at construction time.
	///
	/// Two-layer mapping:
	///   Layer 1: prefab ID → (game category, game subcategory)
	///   Layer 2: (game category, game subcategory) → (scanner category, scanner subcategory)
	///
	/// Prefab override table applied first for known routing exceptions.
	/// </summary>
	public class BuildingRouter {
		private readonly Dictionary<string, (string category, string subcategory)> _prefabToScanner;

		private static readonly Dictionary<string, (string, string)> _prefabOverrides =
			new Dictionary<string, (string, string)> {
				{ "HighEnergyParticleRedirector", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ "HEPBridgeTile", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ "GravitasDoor", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "GravitasBathroomStall", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "GravitasCreatureManipulator", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "GravitasLabLight", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "GravitasContainer", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "GravitasPedestal", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "HijackedHeadquarters", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "TeleportalPad", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "MassiveHeatSink", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "WarpConduitSender", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "WarpConduitReceiver", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "MegaBrainTank", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "MorbRoverMaker", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "FossilDig", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "TemporalTearOpener", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "LonelyMinionHouse", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "LonelyMailBox", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "FacilityBackWallWindow", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "POIFacilityDoor", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "POIDoorInternal", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "POIDlc2ShowroomDoor", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "POIBunkerExteriorDoor", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasLabWall", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasLabWindow", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasLabWindowHorizontal", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasWall", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasWallPurple", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "PropGravitasWallPurpleWhiteDiagonal", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Gravitas) },
				{ "Headquarters", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ "ResetSkillsStation", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				{ "RoleStation", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
			};

		// Whole-category mappings: any building in these game categories routes here.
		// Keys are lowercase — HashCache returns lowercase category strings at runtime.
		private static readonly Dictionary<string, (string, string)> _wholeCategoryMap =
			new Dictionary<string, (string, string)> {
				{ "oxygen", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Oxygen) },
				{ "refining", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Refining) },
				{ "medical", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Wellness) },
				{ "rocketry", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ "hep", (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
			};

		// Subcategory-level mappings: (game category, game subcategory) → scanner destination.
		// Category keys are lowercase — HashCache returns lowercase at runtime.
		private static readonly Dictionary<(string, string), (string, string)> _subcategoryMap =
			new Dictionary<(string, string), (string, string)> {
				// Buildings > Generators
				{ ("power", "generators"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Generators) },
				{ ("power", "electrobankbuildings"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Generators) },
				{ ("equipment", "industrialstation"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Generators) },

				// Buildings > Farming
				{ ("food", "farming"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Farming) },
				{ ("food", "ranching"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Farming) },
				{ ("equipment", "workstations"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Farming) },
				{ ("equipment", "farming"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Farming) },
				{ ("equipment", "ranching"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Farming) },

				// Buildings > Production
				{ ("food", "cooking"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				{ ("equipment", "research"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				{ ("equipment", "manufacturing"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				{ ("equipment", "archaeology"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				{ ("equipment", "meteordefense"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },
				// Marine Drill (geyser tamer, next to GeoTuner which routes via equipment > research)
				{ ("utilities", "conveyancestructures"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Production) },

				// Buildings > Storage
				{ ("base", "storage"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Storage) },
				{ ("food", "storage"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Storage) },

				// Buildings > Temperature
				{ ("utilities", "temperature"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Temperature) },

				// Buildings > Refining
				{ ("utilities", "oil"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Refining) },

				// Buildings > Wellness
				{ ("plumbing", "washroom"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Wellness) },
				{ ("furniture", "beds"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Wellness) },

				// Buildings > Morale
				{ ("furniture", "lights"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Morale) },
				{ ("furniture", "dining"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Morale) },
				{ ("furniture", "recreation"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Morale) },
				{ ("furniture", "decor"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Morale) },

				// Buildings > Infrastructure
				{ ("base", "doors"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("base", "printingpods"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("base", "operations"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("base", "tiles"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("base", "ladders"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("utilities", "sanitation"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("utilities", "materials"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("equipment", "equipment"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },
				{ ("equipment", "operations"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Infrastructure) },

				// Buildings > Rocketry (subcategory-level additions)
				{ ("equipment", "exploration"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ ("equipment", "telescopes"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ ("plumbing", "buildmenuports"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ ("hvac", "buildmenuports"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },
				{ ("conveyance", "buildmenuports"), (ScannerTaxonomy.Categories.Buildings, ScannerTaxonomy.Subcategories.Rocketry) },

				// Networks > Transport
				{ ("base", "transport"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Transport) },

				// Networks > Power
				{ ("power", "batteries"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Power) },
				{ ("power", "powercontrol"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Power) },
				{ ("power", "switches"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Power) },
				{ ("power", "wires"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Power) },

				// Networks > Liquid
				{ ("plumbing", "pumps"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Liquid) },
				{ ("plumbing", "pipes"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Liquid) },
				{ ("plumbing", "valves"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Liquid) },
				{ ("plumbing", "sensors"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Liquid) },

				// Networks > Gas
				{ ("hvac", "pumps"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Gas) },
				{ ("hvac", "pipes"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Gas) },
				{ ("hvac", "valves"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Gas) },
				{ ("hvac", "sensors"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Gas) },

				// Networks > Conveyor
				{ ("conveyance", "conveyancestructures"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Conveyor) },
				{ ("conveyance", "automated"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Conveyor) },
				{ ("conveyance", "pumps"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Conveyor) },
				{ ("conveyance", "sensors"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Conveyor) },
				{ ("conveyance", "valves"), (ScannerTaxonomy.Categories.Networks, ScannerTaxonomy.Subcategories.Conveyor) },

				// Automation > Sensors
				{ ("automation", "sensors"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Sensors) },

				// Automation > Gates
				{ ("automation", "logicgates"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Gates) },

				// Automation > Controls
				{ ("automation", "switches"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Controls) },
				{ ("automation", "logicmanager"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Controls) },
				{ ("automation", "logicaudio"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Controls) },
				{ ("automation", "transmissions"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Controls) },

				// Automation > Wires
				{ ("automation", "wires"), (ScannerTaxonomy.Categories.Automation, ScannerTaxonomy.Subcategories.Wires) },
			};

		public BuildingRouter() {
			_prefabToScanner = new Dictionary<string, (string, string)>();
			BuildMap();
		}

		/// <summary>
		/// Returns (category, subcategory) for the given building prefab ID.
		/// Returns null values if the building is unmapped.
		/// </summary>
		public (string category, string subcategory) Route(string prefabId) {
			if (_prefabToScanner.TryGetValue(prefabId, out var dest))
				return dest;
			return (null, null);
		}

		private void BuildMap() {
			foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
				string gameCategoryStr = HashCache.Get().Get(planInfo.category);

				foreach (var kvp in planInfo.buildingAndSubcategoryData) {
					string buildingPrefabId = kvp.Key;
					string gameSubcategory = kvp.Value;

					if (_prefabOverrides.TryGetValue(buildingPrefabId, out var overrideDest)) {
						_prefabToScanner[buildingPrefabId] = overrideDest;
						continue;
					}

					if (_prefabToScanner.ContainsKey(buildingPrefabId))
						continue;

					if (_wholeCategoryMap.TryGetValue(gameCategoryStr, out var wholeDest)) {
						_prefabToScanner[buildingPrefabId] = wholeDest;
						continue;
					}

					if (_subcategoryMap.TryGetValue((gameCategoryStr, gameSubcategory), out var subDest)) {
						_prefabToScanner[buildingPrefabId] = subDest;
						continue;
					}

					Log.Warn($"BuildingRouter: unmapped building '{buildingPrefabId}' " +
						$"in game category '{gameCategoryStr}' > '{gameSubcategory}'");
				}
			}

			foreach (var kvp in _prefabOverrides) {
				if (!_prefabToScanner.ContainsKey(kvp.Key))
					_prefabToScanner[kvp.Key] = kvp.Value;
			}
		}
	}
}
