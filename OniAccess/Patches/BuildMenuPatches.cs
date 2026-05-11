using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers;
using OniAccess.Handlers.Build;

namespace OniAccess.Patches {
	[HarmonyPatch(typeof(PlanScreen), nameof(PlanScreen.OpenCategoryByName))]
	internal static class PlanScreen_OpenCategoryByName_Patch {
		private static bool Prefix(string category) {
			if (!ModToggle.IsEnabled) return true;
			if (BuildMenuData._selectBuildingInProgress) return true;
			HandlerStack.Push(new ActionMenuHandler((HashedString)category));
			return false;
		}
	}

	[HarmonyPatch(typeof(PlanScreen), nameof(PlanScreen.CopyBuildingOrder), typeof(BuildingDef), typeof(string))]
	internal static class PlanScreen_CopyBuildingOrder_Patch {
		private static void Prefix() {
			if (ModToggle.IsEnabled)
				BuildMenuData._selectBuildingInProgress = true;
		}

		private static void Postfix(BuildingDef buildingDef) {
			BuildMenuData._selectBuildingInProgress = false;
			if (!ModToggle.IsEnabled) return;

			var categoryMap = Traverse.Create(PlanScreen.Instance)
				.Field<Dictionary<Tag, HashedString>>("tagCategoryMap").Value;
			if (categoryMap == null || !categoryMap.TryGetValue(buildingDef.Tag, out var category))
				return;

			// CopyBuildingOrder's body can complete without ever activating
			// PrebuildTool/BuildTool (e.g. an exception inside the inner
			// foreach is swallowed, or the def isn't in activeCategoryBuildingToggles).
			// Harmony still runs this Postfix, so the visible side effect would be
			// a handler on the stack while ActiveTool is whatever it was before
			// (typically SelectTool with a destroyed visualizer left over from
			// the prior build session). RectSetCorner / CountValidPlacements
			// would then NRE on BuildTool.Instance.visualizer.transform.
			var activeTool = PlayerController.Instance.ActiveTool;
			if (!(activeTool is PrebuildTool) && !(activeTool is BuildTool)
					&& !(activeTool is BaseUtilityBuildTool)) {
				Util.Log.Warn(
					$"CopyBuildingOrder Postfix: build tool not active after select ({activeTool?.GetType().Name ?? "null"}); skipping handler push for {buildingDef.PrefabID}");
				return;
			}

			var handler = new BuildToolHandler(category, buildingDef);
			HandlerStack.Push(handler);
			handler.AnnounceInitialState();
		}
	}
}
