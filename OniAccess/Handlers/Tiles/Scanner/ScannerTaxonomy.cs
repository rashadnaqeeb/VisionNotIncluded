using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Scanner {
	/// <summary>
	/// Defines category and subcategory ordering for the scanner hierarchy.
	/// Pure data — no game API calls. Backends reference these constants
	/// to ensure typos are compile errors.
	/// </summary>
	public static class ScannerTaxonomy {
		public static class Categories {
			public const string Solids = "Solids";
			public const string Liquids = "Liquids";
			public const string Gases = "Gases";
			public const string Buildings = "Buildings";
			public const string Networks = "Networks";
			public const string Automation = "Automation";
			public const string Debris = "Debris";
			public const string Zones = "Zones";
			public const string Geysers = "Geysers";
			public const string Life = "Life";
		}

		public static readonly string[] CategoryOrder = {
			Categories.Solids,
			Categories.Liquids,
			Categories.Gases,
			Categories.Buildings,
			Categories.Networks,
			Categories.Automation,
			Categories.Debris,
			Categories.Zones,
			Categories.Geysers,
			Categories.Life,
		};

		public static class Subcategories {
			public const string All = "all";

			// Solids
			public const string Ores = "Ores";
			public const string Stone = "Stone";
			public const string Consumables = "Consumables";
			public const string Organics = "Organics";
			public const string Ice = "Ice";
			public const string Refined = "Refined";
			public const string Tiles = "Tiles";

			// Liquids
			public const string Waters = "Waters";
			public const string Fuels = "Fuels";
			public const string Molten = "Molten";
			public const string Misc = "Misc";

			// Gases
			public const string Safe = "Safe";
			public const string Unsafe = "Unsafe";

			// Buildings
			public const string Oxygen = "Oxygen";
			public const string Generators = "Generators";
			public const string Farming = "Farming";
			public const string Production = "Production";
			public const string Storage = "Storage";
			public const string Refining = "Refining";
			public const string Temperature = "Temperature";
			public const string Wellness = "Wellness";
			public const string Morale = "Morale";
			public const string Infrastructure = "Infrastructure";
			public const string Rocketry = "Rocketry";
			public const string Gravitas = "Gravitas";

			// Networks
			public const string Power = "Power";
			public const string Liquid = "Liquid";
			public const string Gas = "Gas";
			public const string Conveyor = "Conveyor";
			public const string Transport = "Transport";

			// Automation
			public const string Sensors = "Sensors";
			public const string Gates = "Gates";
			public const string Controls = "Controls";
			public const string Wires = "Wires";

			// Debris
			public const string Materials = "Materials";
			public const string Food = "Food";
			public const string Items = "Items";
			public const string Bottles = "Bottles";

			// Zones
			public const string Orders = "Orders";
			public const string Rooms = "Rooms";
			public const string Biomes = "Biomes";

			// Geysers
			public const string Geothermal = "Geothermal";

			// Life
			public const string Duplicants = "Duplicants";
			public const string Robots = "Robots";
			public const string TameCritters = "Tame Critters";
			public const string WildCritters = "Wild Critters";
			public const string WildPlants = "Wild Plants";
			public const string FarmPlants = "Farm Plants";
		}

		public static readonly Dictionary<string, string[]> SubcategoryOrder =
			new Dictionary<string, string[]> {
				{ Categories.Solids, new[] {
					Subcategories.All, Subcategories.Ores, Subcategories.Stone,
					Subcategories.Consumables, Subcategories.Organics,
					Subcategories.Ice, Subcategories.Refined, Subcategories.Tiles,
				}},
				{ Categories.Liquids, new[] {
					Subcategories.All, Subcategories.Waters, Subcategories.Fuels,
					Subcategories.Molten, Subcategories.Misc,
				}},
				{ Categories.Gases, new[] {
					Subcategories.All, Subcategories.Safe, Subcategories.Unsafe,
				}},
				{ Categories.Buildings, new[] {
					Subcategories.All, Subcategories.Oxygen, Subcategories.Generators,
					Subcategories.Farming, Subcategories.Production,
					Subcategories.Storage, Subcategories.Refining,
					Subcategories.Temperature, Subcategories.Wellness,
					Subcategories.Morale, Subcategories.Infrastructure,
					Subcategories.Rocketry,
				Subcategories.Gravitas,
				}},
				{ Categories.Networks, new[] {
					Subcategories.All, Subcategories.Power, Subcategories.Liquid,
					Subcategories.Gas, Subcategories.Conveyor, Subcategories.Transport,
				}},
				{ Categories.Automation, new[] {
					Subcategories.All, Subcategories.Sensors, Subcategories.Gates,
					Subcategories.Controls, Subcategories.Wires,
				}},
				{ Categories.Debris, new[] {
					Subcategories.All, Subcategories.Materials, Subcategories.Food,
					Subcategories.Items, Subcategories.Bottles,
				}},
				{ Categories.Zones, new[] {
					Subcategories.All, Subcategories.Orders, Subcategories.Rooms,
					Subcategories.Biomes,
				}},
				{ Categories.Geysers, new[] {
					Subcategories.All, Subcategories.Gas, Subcategories.Liquid,
					Subcategories.Molten, Subcategories.Geothermal,
				}},
				{ Categories.Life, new[] {
					Subcategories.All, Subcategories.Duplicants,
					Subcategories.Robots,
					Subcategories.TameCritters, Subcategories.WildCritters,
					Subcategories.WildPlants, Subcategories.FarmPlants,
				}},
			};

		private static readonly Dictionary<string, int> _categoryIndices;
		private static readonly Dictionary<string, Dictionary<string, int>> _subcategoryIndices;

		static ScannerTaxonomy() {
			_categoryIndices = new Dictionary<string, int>();
			for (int i = 0; i < CategoryOrder.Length; i++)
				_categoryIndices[CategoryOrder[i]] = i;

			_subcategoryIndices = new Dictionary<string, Dictionary<string, int>>();
			foreach (var kvp in SubcategoryOrder) {
				var map = new Dictionary<string, int>();
				for (int i = 0; i < kvp.Value.Length; i++)
					map[kvp.Value[i]] = i;
				_subcategoryIndices[kvp.Key] = map;
			}
		}

		public static int CategorySortIndex(string category) {
			return _categoryIndices.TryGetValue(category, out int idx) ? idx : int.MaxValue;
		}

		public static int SubcategorySortIndex(string category, string subcategory) {
			if (_subcategoryIndices.TryGetValue(category, out var map)
				&& map.TryGetValue(subcategory, out int idx))
				return idx;
			return int.MaxValue;
		}

		/// <summary>
		/// The named subcategories of a category in taxonomy order, excluding
		/// the synthetic "all". Returns an empty array for an unknown category.
		/// </summary>
		public static string[] NamedSubcategories(string category) {
			if (!SubcategoryOrder.TryGetValue(category, out var subs))
				return new string[0];
			var list = new List<string>(subs.Length);
			foreach (var s in subs)
				if (s != Subcategories.All) list.Add(s);
			return list.ToArray();
		}
	}
}
