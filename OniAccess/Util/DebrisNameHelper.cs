using UnityEngine;

namespace OniAccess.Util {
	/// <summary>
	/// Shared helper for the mod's additions to entity names: "Loose" or
	/// "Bottled" on pickupables, "branch" on plant branches. Used by
	/// DetailsScreenHandler, BuildingSection, and EntityBackend to keep the
	/// naming logic in one place.
	/// </summary>
	public static class DebrisNameHelper {
		public static string GetDisplayName(GameObject go) {
			string name = go.GetComponent<KSelectable>()?.GetName()
				?? go.GetProperName();
			var prefabId = go.GetComponent<KPrefabID>();
			if (prefabId != null && IsBottle(prefabId))
				return string.Format((string)STRINGS.ONIACCESS.SCANNER.BOTTLE_NAME, name);
			if (go.GetComponent<ElementChunk>() != null)
				return string.Format((string)STRINGS.ONIACCESS.SCANNER.LOOSE_NAME, name);
			return WithBranchSuffix(go, name);
		}

		public static bool IsBottle(KPrefabID prefabId) {
			return prefabId.HasTag(GameTags.Liquid)
				|| prefabId.HasTag(GameTags.Breathable)
				|| prefabId.HasTag(GameTags.Unbreathable);
		}

		/// <summary>
		/// Branches of the Arbor and Bonbon trees carry their trunk's name,
		/// so without the suffix a tree and its branches read the same.
		/// </summary>
		public static string WithBranchSuffix(GameObject go, string name) {
			var prefabId = go.GetComponent<KPrefabID>();
			if (prefabId != null && prefabId.HasTag(GameTags.PlantBranch))
				return string.Format((string)STRINGS.ONIACCESS.GLANCE.PLANT_BRANCH, name);
			return name;
		}
	}
}
