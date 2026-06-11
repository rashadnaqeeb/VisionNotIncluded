using System.Collections.Generic;
using System.Reflection;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.Scanner.Routing {
	/// <summary>
	/// Routes elements to scanner subcategories based on phase and
	/// materialCategory/element identity. Three static methods, one per phase.
	/// </summary>
	public static class ElementRouter {
		private static Dictionary<SimHashes, float> _gasExposureRates;

		private static readonly Dictionary<Tag, string> _solidSubcategories =
			new Dictionary<Tag, string> {
				{ GameTags.Metal, ScannerTaxonomy.Subcategories.Ores },
				{ GameTags.RefinedMetal, ScannerTaxonomy.Subcategories.Ores },
				{ GameTags.Alloy, ScannerTaxonomy.Subcategories.Ores },
				{ GameTags.BuildableRaw, ScannerTaxonomy.Subcategories.Stone },
				{ GameTags.ConsumableOre, ScannerTaxonomy.Subcategories.Consumables },
				{ GameTags.Filter, ScannerTaxonomy.Subcategories.Consumables },
				{ GameTags.CookingIngredient, ScannerTaxonomy.Subcategories.Consumables },
				{ GameTags.Sublimating, ScannerTaxonomy.Subcategories.Consumables },
				{ GameTags.Other, ScannerTaxonomy.Subcategories.Consumables },
				{ GameTags.Organics, ScannerTaxonomy.Subcategories.Organics },
				{ GameTags.Farmable, ScannerTaxonomy.Subcategories.Organics },
				{ GameTags.Agriculture, ScannerTaxonomy.Subcategories.Organics },
				{ GameTags.Liquifiable, ScannerTaxonomy.Subcategories.Ice },
				{ GameTags.BuildableProcessed, ScannerTaxonomy.Subcategories.Refined },
				{ GameTags.ManufacturedMaterial, ScannerTaxonomy.Subcategories.Refined },
				{ GameTags.RareMaterials, ScannerTaxonomy.Subcategories.Refined },
			};

		private static readonly HashSet<SimHashes> _waters = new HashSet<SimHashes> {
			SimHashes.Water, SimHashes.DirtyWater, SimHashes.SaltWater, SimHashes.Brine,
		};

		private static readonly HashSet<SimHashes> _fuels = new HashSet<SimHashes> {
			SimHashes.CrudeOil, SimHashes.Petroleum, SimHashes.Naphtha, SimHashes.Ethanol,
		};

		private static readonly HashSet<SimHashes> _molten = new HashSet<SimHashes> {
			SimHashes.Magma, SimHashes.MoltenIron, SimHashes.MoltenCopper,
			SimHashes.MoltenGold, SimHashes.MoltenAluminum, SimHashes.MoltenTungsten,
			SimHashes.MoltenNiobium, SimHashes.MoltenCobalt, SimHashes.MoltenGlass,
			SimHashes.MoltenLead, SimHashes.MoltenSteel, SimHashes.MoltenCarbon,
			SimHashes.MoltenSalt, SimHashes.MoltenUranium, SimHashes.MoltenSyngas,
			SimHashes.MoltenSucrose, SimHashes.MoltenNickel, SimHashes.MoltenIridium,
			SimHashes.MoltenZinc,
		};

		public static string GetSolidSubcategory(Element element) {
			if (_solidSubcategories.TryGetValue(element.materialCategory, out string sub))
				return sub;
			return ScannerTaxonomy.Subcategories.Consumables;
		}

		public static string GetLiquidSubcategory(Element element) {
			if (_waters.Contains(element.id))
				return ScannerTaxonomy.Subcategories.Waters;
			if (_fuels.Contains(element.id))
				return ScannerTaxonomy.Subcategories.Fuels;
			if (_molten.Contains(element.id))
				return ScannerTaxonomy.Subcategories.Molten;
			return ScannerTaxonomy.Subcategories.Misc;
		}

		/// <summary>
		/// Uses GasLiquidExposureMonitor.customExposureRates to classify gases.
		/// Rates >= 1.0 are irritants (Unsafe). Unlisted gases default to 1.0
		/// matching the game's default exposure rate.
		/// </summary>
		public static string GetGasSubcategory(Element element) {
			if (element.HasTag(GameTags.Breathable))
				return ScannerTaxonomy.Subcategories.Safe;
			float rate = GetGasExposureRate(element.id);
			return rate >= 1.0f
				? ScannerTaxonomy.Subcategories.Unsafe
				: ScannerTaxonomy.Subcategories.Safe;
		}

		// The game lazily initializes customExposureRates inside
		// InitializeCustomRates, so we invoke that first to ensure
		// the dictionary exists before we read it.
		private static float GetGasExposureRate(SimHashes id) {
			if (_gasExposureRates == null) {
				var initMethod = typeof(GasLiquidExposureMonitor).GetMethod(
					"InitializeCustomRates",
					BindingFlags.Static | BindingFlags.NonPublic);
				initMethod?.Invoke(null, null);

				var field = typeof(GasLiquidExposureMonitor).GetField(
					"customExposureRates",
					BindingFlags.Static | BindingFlags.NonPublic);
				_gasExposureRates = field?.GetValue(null) as Dictionary<SimHashes, float>;
				if (_gasExposureRates == null) {
					Log.Warn("ElementRouter: could not read GasLiquidExposureMonitor.customExposureRates");
					_gasExposureRates = new Dictionary<SimHashes, float>();
				}
			}
			return _gasExposureRates.TryGetValue(id, out float rate) ? rate : 1.0f;
		}
	}
}
