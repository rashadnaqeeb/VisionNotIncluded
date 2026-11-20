using System.Collections.Generic;
using OniAccess.Handlers.Build;
using OniAccess.Handlers.Tiles.Sections;

namespace OniAccess.Handlers.Tiles.ToolProfiles.Sections {
	/// <summary>
	/// While placing a building, names what the game highlights for
	/// support buildings. Placing a support building (Kitchen Tools and
	/// the like) names the buildings it would optimize from here; placing
	/// a building that can be optimized names the support buildings whose
	/// range covers it. Non-stacking boosts skip buildings that already
	/// have one, as SupportBuildingPreview.HighlightReceptors does.
	/// </summary>
	public class SupportBuildingSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var handler = BuildToolHandler.Instance;
			if (handler == null || handler._def == null
					|| !(PlayerController.Instance.ActiveTool is BuildTool))
				return System.Array.Empty<string>();
			var def = handler._def;
			int origin = handler.GetOriginCell(cell);
			if (!Grid.IsValidCell(origin)) return System.Array.Empty<string>();

			var support = def.BuildingComplete.GetComponent<SupportBuilding>();
			if (support != null)
				return Format(STRINGS.ONIACCESS.BUILD_MENU.SUPPORT_OPTIMIZES,
					ReceptorsInRange(support, origin));

			var receptor = def.BuildingComplete.GetComponent<SupportBuildingReceptor>();
			if (receptor != null)
				return Format(STRINGS.ONIACCESS.BUILD_MENU.SUPPORT_OPTIMIZED_BY,
					SupportBuildingsCovering(def, receptor, origin));

			return System.Array.Empty<string>();
		}

		private static IEnumerable<string> Format(string pattern,
				List<string> names) {
			if (names.Count == 0) return System.Array.Empty<string>();
			return new[] { string.Format(pattern, string.Join(", ", names)) };
		}

		/// <summary>
		/// Built buildings in range of a support building placed at origin
		/// that it would optimize.
		/// </summary>
		private static List<string> ReceptorsInRange(SupportBuilding support,
				int origin) {
			var names = new List<string>();
			var boost = support.GetBoostData();
			foreach (var receptor in SupportBuildingRange.CompatibleReceptors(
					support, origin)) {
				if (!boost.CanStack
						&& receptor.GetAppliedBoostWithID(boost.Id) != null)
					continue;
				names.Add(receptor.GetComponent<KSelectable>().GetName());
			}
			return names;
		}

		/// <summary>
		/// Existing support buildings, built or under construction, whose
		/// range reaches any cell of the building being placed at origin.
		/// The search box spans the widest support range among the game's
		/// buildings, so a new support building is covered automatically.
		/// </summary>
		private static List<string> SupportBuildingsCovering(BuildingDef def,
				SupportBuildingReceptor receptor, int origin) {
			var names = new List<string>();
			var orientation = BuildMenuData.GetCurrentOrientation();
			var footprint = new List<int>();
			foreach (var offset in def.PlacementOffsets) {
				int c = Grid.OffsetCell(origin,
					Rotatable.GetRotatedCellOffset(offset, orientation));
				if (Grid.IsValidCell(c)) footprint.Add(c);
			}
			if (footprint.Count == 0) return names;

			int reach = SupportBuildingRange.MaxReach();
			int minX = int.MaxValue, minY = int.MaxValue;
			int maxX = int.MinValue, maxY = int.MinValue;
			foreach (int c in footprint) {
				Grid.CellToXY(c, out int x, out int y);
				if (x < minX) minX = x;
				if (y < minY) minY = y;
				if (x > maxX) maxX = x;
				if (y > maxY) maxY = y;
			}

			var seen = new HashSet<UnityEngine.GameObject>();
			for (int x = minX - reach; x <= maxX + reach; x++) {
				for (int y = minY - reach; y <= maxY + reach; y++) {
					int c = Grid.XYToCell(x, y);
					if (!Grid.IsValidCell(c)) continue;
					var go = Grid.Objects[c, (int)ObjectLayer.Building];
					if (go == null || !seen.Add(go)) continue;
					var support = go.GetComponent<SupportBuilding>();
					if (support == null) continue;
					if (!support.GetBoostData().IsReceptorCompatible(support, receptor))
						continue;
					if (!SupportBuildingRange.Covers(support,
							Grid.PosToCell(support), footprint))
						continue;
					names.Add(ConduitSection.ConstructionName(
						go, go.GetComponent<KSelectable>()));
				}
			}
			return names;
		}
	}
}
