using System;
using System.Collections.Generic;
using System.Linq;

namespace OniAccess.Handlers.Tiles.AreaScan {
	/// <summary>
	/// Area scan for the default overlay and utility overlays.
	/// Reports: unexplored %, solid/liquid/gas/vacuum breakdown,
	/// building count by type, dupe count, critter count,
	/// pending order count by type.
	/// </summary>
	public class DefaultAreaScanner: IAreaScanner {
		public string Scan(int[] cells, int totalCells, int unexploredCount) {
			try {
				return ScanCore(cells, totalCells, unexploredCount);
			} catch (Exception ex) {
				Util.Log.Error($"DefaultAreaScanner.Scan: {ex}");
				return (string)STRINGS.ONIACCESS.BIG_CURSOR.SCAN_ERROR;
			}
		}

		private static string ScanCore(int[] cells, int totalCells, int unexploredCount) {
			var tokens = new List<string>();
			AreaScanUtil.AddUnexploredToken(tokens, totalCells, unexploredCount);

			int solid = 0, liquid = 0, gas = 0, vacuum = 0;
			var buildings = new Dictionary<string, int>();
			var seenBuildings = new HashSet<UnityEngine.GameObject>();
			int dupeCount = 0;
			int critterCount = 0;
			var orders = new Dictionary<string, int>();
			var seenOrderBuildings = new HashSet<UnityEngine.GameObject>();

			for (int i = 0; i < cells.Length; i++) {
				int cell = cells[i];
				var element = Grid.Element[cell];
				if (element.IsSolid)
					solid++;
				else if (element.IsLiquid)
					liquid++;
				else if (element.IsGas)
					gas++;
				else if (element.IsVacuum)
					vacuum++;

				CountBuilding(cell, buildings, seenBuildings);
				CountEntities(cell, ref dupeCount, ref critterCount);
				CountOrders(cell, orders, seenOrderBuildings);
			}

			AddStatePercent(tokens, (string)STRINGS.ONIACCESS.BIG_CURSOR.SOLID,
				solid, totalCells);
			AddStatePercent(tokens, (string)STRINGS.ONIACCESS.BIG_CURSOR.LIQUID,
				liquid, totalCells);
			AddStatePercent(tokens, (string)STRINGS.ONIACCESS.BIG_CURSOR.GAS,
				gas, totalCells);
			AddStatePercent(tokens, (string)STRINGS.ONIACCESS.BIG_CURSOR.VACUUM,
				vacuum, totalCells);

			foreach (var kv in buildings.OrderByDescending(kv => kv.Value))
				tokens.Add(string.Format(
					STRINGS.ONIACCESS.BIG_CURSOR.BUILDING_COUNT,
					kv.Value, kv.Key));

			if (dupeCount > 0) {
				string fmt = dupeCount == 1
					? (string)STRINGS.ONIACCESS.BIG_CURSOR.DUPE_SINGULAR
					: (string)STRINGS.ONIACCESS.BIG_CURSOR.DUPE_PLURAL;
				tokens.Add(string.Format(fmt, dupeCount));
			}
			if (critterCount > 0) {
				string fmt = critterCount == 1
					? (string)STRINGS.ONIACCESS.BIG_CURSOR.CRITTER_SINGULAR
					: (string)STRINGS.ONIACCESS.BIG_CURSOR.CRITTER_PLURAL;
				tokens.Add(string.Format(fmt, critterCount));
			}

			foreach (var kv in orders.OrderByDescending(kv => kv.Value))
				tokens.Add(string.Format(
					STRINGS.ONIACCESS.BIG_CURSOR.ORDER_COUNT,
					kv.Value, kv.Key));

			return tokens.Count > 0
				? string.Join(", ", tokens)
				: (string)STRINGS.ONIACCESS.BIG_CURSOR.EMPTY;
		}

		private static void AddStatePercent(List<string> tokens, string label,
				int count, int total) {
			if (count == 0) return;
			int pct = (int)Math.Round(100.0 * count / total);
			if (pct == 0) pct = 1;
			tokens.Add(string.Format(
				STRINGS.ONIACCESS.BIG_CURSOR.ELEMENT_PCT, label, pct));
		}

		private static void CountBuilding(int cell, Dictionary<string, int> buildings,
				HashSet<UnityEngine.GameObject> seen) {
			CountBuildingOnLayer(cell, ObjectLayer.Building, buildings, seen);
			CountBuildingOnLayer(cell, ObjectLayer.AttachableBuilding, buildings, seen);
		}

		private static void CountBuildingOnLayer(int cell, ObjectLayer layer,
				Dictionary<string, int> buildings, HashSet<UnityEngine.GameObject> seen) {
			var go = Grid.Objects[cell, (int)layer];
			if (go == null) return;
			if (go.GetComponent<Growing>() != null) return;
			var uncoverable = go.GetComponent<Uncoverable>();
			if (uncoverable != null && !uncoverable.IsUncovered) return;
			if (!seen.Add(go)) return;
			string name = GetBuildingName(go);
			if (name == null) return;
			if (buildings.ContainsKey(name))
				buildings[name]++;
			else
				buildings[name] = 1;
		}

		internal static string GetBuildingName(UnityEngine.GameObject go) {
			var bc = go.GetComponent<BuildingComplete>();
			if (bc != null) return bc.Def.Name;
			var buc = go.GetComponent<BuildingUnderConstruction>();
			if (buc != null) return buc.Def.Name;
			return null;
		}

		private static void CountEntities(int cell, ref int dupeCount,
				ref int critterCount) {
			if (Grid.Objects[cell, (int)ObjectLayer.Minion] != null)
				dupeCount++;

			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return;
			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return;
			for (var item = pickupable.objectLayerListItem;
				item != null; item = item.nextItem) {
				if (item.gameObject.GetComponent<CreatureBrain>() != null)
					critterCount++;
			}
		}

		private static void CountOrders(int cell, Dictionary<string, int> orders,
				HashSet<UnityEngine.GameObject> seenBuildings) {
			if (Grid.Objects[cell, (int)ObjectLayer.DigPlacer] != null)
				Increment(orders, Strings.Get("STRINGS.UI.TOOLS.DIG.TOOLNAME"));
			if (Grid.Objects[cell, (int)ObjectLayer.MopPlacer] != null)
				Increment(orders, Strings.Get("STRINGS.UI.TOOLS.MOP.TOOLNAME"));

			CountOrdersOnBuildingLayer(cell, ObjectLayer.Building, orders, seenBuildings);
			CountOrdersOnBuildingLayer(cell, ObjectLayer.AttachableBuilding, orders, seenBuildings);

			var foundationGo = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
			if (foundationGo != null && seenBuildings.Add(foundationGo)) {
				var constructable = foundationGo.GetComponent<Constructable>();
				if (constructable != null)
					Increment(orders, Strings.Get("STRINGS.UI.TOOLS.BUILD.TOOLNAME"));
				var decon = foundationGo.GetComponent<Deconstructable>();
				if (decon != null && decon.IsMarkedForDeconstruction())
					Increment(orders,
						Strings.Get("STRINGS.UI.TOOLS.DECONSTRUCT.TOOLNAME"));
			}

			var pickGo = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (pickGo != null) {
				var pickupable = pickGo.GetComponent<Pickupable>();
				if (pickupable != null) {
					for (var item = pickupable.objectLayerListItem;
						item != null; item = item.nextItem) {
						var clearable = item.gameObject.GetComponent<Clearable>();
						if (clearable != null && clearable.HasTag(GameTags.Garbage)) {
							Increment(orders,
								Strings.Get("STRINGS.UI.TOOLS.MARKFORSTORAGE.TOOLNAME"));
							break;
						}
					}
				}
			}
		}

		private static void CountOrdersOnBuildingLayer(int cell, ObjectLayer layer,
				Dictionary<string, int> orders, HashSet<UnityEngine.GameObject> seen) {
			var go = Grid.Objects[cell, (int)layer];
			if (go == null || !seen.Add(go)) return;
			var constructable = go.GetComponent<Constructable>();
			if (constructable != null)
				Increment(orders, Strings.Get("STRINGS.UI.TOOLS.BUILD.TOOLNAME"));
			var decon = go.GetComponent<Deconstructable>();
			if (decon != null && decon.IsMarkedForDeconstruction())
				Increment(orders,
					Strings.Get("STRINGS.UI.TOOLS.DECONSTRUCT.TOOLNAME"));
			var harvest = go.GetComponent<HarvestDesignatable>();
			if (harvest != null && harvest.MarkedForHarvest)
				Increment(orders,
					Strings.Get("STRINGS.UI.TOOLS.HARVEST.TOOLNAME"));
			var uproot = go.GetComponent<Uprootable>();
			if (uproot != null && uproot.IsMarkedForUproot)
				Increment(orders,
					Strings.Get("STRINGS.UI.TOOLS.UPROOT.TOOLNAME"));
		}

		private static void Increment(Dictionary<string, int> dict, string key) {
			if (dict.ContainsKey(key))
				dict[key]++;
			else
				dict[key] = 1;
		}
	}
}
