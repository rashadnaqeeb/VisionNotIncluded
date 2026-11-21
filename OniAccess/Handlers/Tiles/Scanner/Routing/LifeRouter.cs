using UnityEngine;

namespace OniAccess.Handlers.Tiles.Scanner.Routing {
	/// <summary>
	/// Entity classification for the Life category.
	/// Determines whether critters are wild/tame and plants are wild/farmed.
	/// </summary>
	public static class LifeRouter {
		public static bool IsWild(GameObject go) {
			return go.GetComponent<KPrefabID>().HasTag(GameTags.Creatures.Wild);
		}

		/// <summary>
		/// A branch has no planter of its own; it goes where its trunk goes.
		/// </summary>
		public static bool IsFarmPlant(Uprootable plant) {
			var trunk = plant.GetSMI<PlantBranch.Instance>()?.GetTrunk();
			if (trunk != null)
				plant = trunk.GetComponent<Uprootable>();
			return plant.GetPlanterStorage != null;
		}
	}
}
