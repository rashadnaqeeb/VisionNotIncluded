using System.Collections.Generic;
using UnityEngine;

using OniAccess.Handlers.Tiles;
using OniAccess.Util;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Details {
	/// <summary>
	/// Details-screen section for support buildings (Cooking Tools and the
	/// like): lists every built building in range that the boost works on,
	/// nearest first, saying whether this support building is optimizing it
	/// and where it is. Enter selects the building, re-targeting the
	/// details screen. The range is the game's own test (see
	/// SupportBuildingRange), so the list matches what the game highlights
	/// when the support building is selected, plus the compatible buildings
	/// it is not optimizing: those already optimized by another support
	/// building of a kind that cannot stack, or every one while it is
	/// inactive.
	/// </summary>
	static class SupportBuildingRangeSection {
		public static void Append(GameObject target, List<DetailSection> sections) {
			var support = target.GetComponent<SupportBuilding>();
			if (support == null || target.GetComponent<Constructable>() != null)
				return;

			int origin = Grid.PosToCell(support);
			Grid.CellToXY(SupportBuildingRange.PovCell(support, origin),
				out int px, out int py);
			var receptors = SupportBuildingRange.CompatibleReceptors(support, origin);
			receptors.Sort((a, b) => {
				int byDist = Distance(a, px, py).CompareTo(Distance(b, px, py));
				if (byDist != 0) return byDist;
				return Grid.PosToCell(a).CompareTo(Grid.PosToCell(b));
			});

			var section = new DetailSection {
				Key = "supportRange",
				Header = (string)STRINGS.ONIACCESS.DETAILS.IN_RANGE
			};
			foreach (var receptor in receptors) {
				var go = receptor.gameObject;
				section.Items.Add(new UserMenuButtonWidget {
					Key = go.GetInstanceID().ToString(),
					SpeechFunc = () => FormatItem(receptor, support),
					OnClick = () => SelectBuilding(go)
				});
			}
			if (section.Items.Count == 0)
				section.Items.Add(new LabelWidget {
					Key = "supportRangeEmpty",
					SpeechFunc = () =>
						(string)STRINGS.ONIACCESS.DETAILS.NOTHING_IN_RANGE
				});
			sections.Add(section);
		}

		private static string FormatItem(SupportBuildingReceptor receptor,
				SupportBuilding support) {
			if (receptor == null || support == null) return null;
			var parts = new List<string> {
				receptor.GetComponent<KSelectable>().GetName(),
				receptor.HasABoostAppliedBySupportBuilding(support)
					? (string)STRINGS.ONIACCESS.DETAILS.SUPPORT_OPTIMIZED
					: (string)STRINGS.ONIACCESS.DETAILS.SUPPORT_NOT_OPTIMIZED,
				GridCoordinates.Format(Grid.PosToCell(receptor))
			};
			return string.Join(", ", parts);
		}

		private static int Distance(SupportBuildingReceptor receptor, int px, int py) {
			Grid.CellToXY(Grid.PosToCell(receptor), out int x, out int y);
			return Mathf.Max(Mathf.Abs(x - px), Mathf.Abs(y - py));
		}

		private static void SelectBuilding(GameObject go) {
			var selectable = go.GetComponent<KSelectable>();
			if (!(PlayerController.Instance.ActiveTool is SelectTool))
				SelectTool.Instance.Activate();
			SelectTool.Instance.Select(selectable);
		}
	}
}
