using HarmonyLib;

namespace OniAccess.Patches {
	/// <summary>
	/// PlayerControlledToggleSideScreen.RenderEveryTick polls
	/// Input.GetKeyDown(Return) every frame to toggle the switch.
	/// This conflicts with our handler's Enter key handling,
	/// causing a double-toggle that cancels itself out.
	/// Skip the method entirely when the mod is active.
	/// </summary>
	[HarmonyPatch(typeof(PlayerControlledToggleSideScreen), nameof(PlayerControlledToggleSideScreen.RenderEveryTick))]
	internal static class PlayerControlledToggleSideScreen_RenderEveryTick_Patch {
		private static bool Prefix() {
			return !ModToggle.IsEnabled;
		}
	}

	/// <summary>
	/// TreeFilterableSideScreen.OnSpawn calls inputField.ActivateInputField(),
	/// giving the search box keyboard focus. A blind player's type-ahead
	/// keystrokes then bleed into that field, its filter hides rows, and because
	/// SetTarget only clears the search when the target changes, the hidden state
	/// persists across close/reopen of the same storage/conveyor loader until the
	/// save is reloaded. Deactivate the field on spawn so nothing gets captured.
	/// </summary>
	[HarmonyPatch(typeof(TreeFilterableSideScreen), "OnSpawn")]
	internal static class TreeFilterableSideScreen_OnSpawn_Patch {
		private static void Postfix(TreeFilterableSideScreen __instance) {
			if (!ModToggle.IsEnabled) return;
			try {
				var field = Traverse.Create(__instance).Field("inputField")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"TreeFilterableSideScreen_OnSpawn: failed to deactivate search field: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Same problem as TreeFilterableSideScreen, but for the shared SearchBar
	/// component used by SingleItemSelectionSideScreenBase and its subclasses
	/// (SingleItemSelectionSideScreen for storage tiles, FilterSideScreen for
	/// element/liquid/logic filter buildings). SearchBar.OnSpawn activates its
	/// input field, which swallows type-ahead keystrokes and filters rows out
	/// of view. Deactivate on spawn so all four filterable side screens behave
	/// consistently for blind players.
	/// </summary>
	[HarmonyPatch(typeof(SearchBar), "OnSpawn")]
	internal static class SearchBar_OnSpawn_Patch {
		private static void Postfix(SearchBar __instance) {
			if (!ModToggle.IsEnabled) return;
			try {
				var field = Traverse.Create(__instance).Field("inputField")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"SearchBar_OnSpawn: failed to deactivate search field: {ex.Message}");
			}
		}
	}
}
