using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// SupportBuilding's range test, usable for a building that exists and
	/// for one still being placed: a rectangle around the origin cell
	/// shifted by OriginOffset, cut by line of sight through solid tiles
	/// with the blocking tile itself counted as visible
	/// (SupportBuilding.UpdateCellsInRange and IsCellInRange).
	/// </summary>
	internal static class SupportBuildingRange {
		/// <summary>
		/// Built buildings on the Building layer, in range of a support
		/// building whose origin is at originCell, that its boost works on.
		/// Each appears once, in scan order. Says nothing about stacking.
		/// </summary>
		internal static List<SupportBuildingReceptor> CompatibleReceptors(
				SupportBuilding support, int originCell) {
			var result = new List<SupportBuildingReceptor>();
			var seen = new HashSet<UnityEngine.GameObject>();
			var boost = support.GetBoostData();
			int pov = PovCell(support, originCell);
			int corner = Grid.OffsetCell(pov, support.RangeMin.x,
				support.RangeMin.y);
			for (int i = 0; i < support.AreaWidth; i++) {
				for (int j = 0; j < support.AreaHeight; j++) {
					int c = Grid.OffsetCell(corner, i, j);
					if (!InSight(support, pov, c)) continue;
					var go = Grid.Objects[c, (int)ObjectLayer.Building];
					if (go == null || !seen.Add(go)) continue;
					var receptor = go.GetComponent<SupportBuildingReceptor>();
					if (receptor != null
							&& boost.IsReceptorCompatible(support, receptor))
						result.Add(receptor);
				}
			}
			return result;
		}

		/// <summary>
		/// True when any of cells is in range of a support building whose
		/// origin is at originCell.
		/// </summary>
		internal static bool Covers(SupportBuilding support, int originCell,
				List<int> cells) {
			int pov = PovCell(support, originCell);
			Grid.CellToXY(pov, out int px, out int py);
			foreach (int c in cells) {
				Grid.CellToXY(c, out int x, out int y);
				int dx = x - px, dy = y - py;
				if (dx < support.RangeMin.x || dx > support.RangeMax.x
						|| dy < support.RangeMin.y || dy > support.RangeMax.y)
					continue;
				if (InSight(support, pov, c)) return true;
			}
			return false;
		}

		/// <summary>
		/// The cell the range is measured from.
		/// </summary>
		internal static int PovCell(SupportBuilding support, int originCell) {
			return Grid.OffsetCell(originCell, support.OriginOffset.x,
				support.OriginOffset.y);
		}

		/// <summary>
		/// The farthest any support building's range reaches from its
		/// origin cell, across every building the game defines.
		/// </summary>
		internal static int MaxReach() {
			int reach = 0;
			foreach (var def in Assets.BuildingDefs) {
				var s = def.BuildingComplete.GetComponent<SupportBuilding>();
				if (s == null) continue;
				int r = System.Math.Max(
					System.Math.Max(System.Math.Abs(s.OriginOffset.x + s.RangeMin.x),
						System.Math.Abs(s.OriginOffset.x + s.RangeMax.x)),
					System.Math.Max(System.Math.Abs(s.OriginOffset.y + s.RangeMin.y),
						System.Math.Abs(s.OriginOffset.y + s.RangeMax.y)));
				if (r > reach) reach = r;
			}
			return reach;
		}

		private static bool InSight(SupportBuilding support, int pov, int cell) {
			if (!Grid.IsValidCell(cell)) return false;
			Grid.CellToXY(pov, out int px, out int py);
			Grid.CellToXY(cell, out int x, out int y);
			return Grid.TestLineOfSight(px, py, x, y, support.BlockingCb,
				_ => true);
		}
	}
}
