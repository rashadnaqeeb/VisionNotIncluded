using System.Collections.Generic;
using UnityEngine;

namespace OniAccess.Util {
	internal static class SkyVisibility {
		internal static bool TryGetSkyVisibilityInfo(GameObject target, out SkyVisibilityInfo info) {
			var skyMon = target.GetSMI<SkyVisibilityMonitor.Instance>();
			if (skyMon != null) {
				info = skyMon.def.skyVisibilityInfo;
				return true;
			}

			var telescope = target.GetSMI<ClusterTelescope.Instance>();
			if (telescope != null) {
				info = telescope.def.skyVisibilityInfo;
				return true;
			}

			if (target.GetComponent<Telescope>() != null) {
				info = TelescopeConfig.SKY_VISIBILITY_INFO;
				return true;
			}

			if (target.GetSMI<ClusterCometDetector.Instance>() != null
				|| target.GetSMI<CometDetector.Instance>() != null) {
				info = CometDetectorConfig.SKY_VISIBILITY_INFO;
				return true;
			}

			info = default;
			return false;
		}

		internal static List<(int worldX, int worldY)> GetBlockedColumns(
			GameObject target, SkyVisibilityInfo info) {
			var blocked = new List<(int, int)>();
			int centerCell = Grid.PosToCell(target);
			WorldContainer world = ClusterManager.Instance.GetWorld(Grid.WorldIdx[centerCell]);
			if (world.IsModuleInterior)
				return blocked;

			int leftOrigin = Grid.OffsetCell(centerCell, info.scanLeftOffset);
			CollectBlocked(blocked, leftOrigin, -1, info.verticalStep,
				info.scanLeftCount, world);

			int rightOrigin = Grid.OffsetCell(centerCell, info.scanRightOffset);
			int rightStart = info.scanLeftOffset == info.scanRightOffset ? 1 : 0;
			for (int i = rightStart; i <= info.scanRightCount; i++) {
				int cell = Grid.OffsetCell(rightOrigin, i, i * info.verticalStep);
				if (!SkyVisibilityInfo.IsVisible(cell, world))
					blocked.Add((Grid.CellColumn(cell), Grid.CellRow(cell)));
			}

			return blocked;
		}

		private static void CollectBlocked(List<(int, int)> blocked,
			int originCell, int stepX, int stepY, int count, WorldContainer world) {
			for (int i = 0; i <= count; i++) {
				int cell = Grid.OffsetCell(originCell, i * stepX, i * stepY);
				if (!SkyVisibilityInfo.IsVisible(cell, world))
					blocked.Add((Grid.CellColumn(cell), Grid.CellRow(cell)));
			}
		}
	}
}
