namespace OniAccess.Util {
	/// <summary>
	/// Names the practical suit a duplicant is wearing (atmo, jet, lead, or
	/// oxygen mask). Clothing is excluded: only items tagged GameTags.Suit
	/// qualify, which the game applies to exactly those four.
	/// </summary>
	public static class WornSuit {
		/// <summary>
		/// Localized name of the practical suit the dupe is wearing, or null
		/// if none is equipped.
		/// </summary>
		public static string GetName(MinionIdentity mi) {
			foreach (var slot in mi.GetEquipment().Slots) {
				var equippable = slot.assignable as Equippable;
				if (equippable != null && equippable.isEquipped
						&& IsSuit(equippable.GetComponent<KPrefabID>()))
					return equippable.def.Name;
			}
			return null;
		}

		/// <summary>
		/// True if the prefab is one of the practical suits, not clothing.
		/// </summary>
		public static bool IsSuit(KPrefabID prefabID) {
			return prefabID != null && prefabID.HasTag(GameTags.Suit);
		}
	}
}
