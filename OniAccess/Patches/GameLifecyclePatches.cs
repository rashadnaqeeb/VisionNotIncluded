using HarmonyLib;
using OniAccess.Input;
using OniAccess.Util;

namespace OniAccess.Patches {
	/// <summary>
	/// Harmony patches for one-time game lifecycle events.
	/// Screen lifecycle patches (activate, deactivate, show/hide) are in
	/// ScreenLifecyclePatches.cs.
	///
	/// Game.OnDestroy is intentionally NOT patched. It fires during scene
	/// transitions (loading a save), not just on application quit. A previous
	/// patch called SpeechEngine.Shutdown() here, which killed all speech
	/// after loading a save. Prism cleanup is handled by OS process exit.
	/// </summary>

	[HarmonyPatch(typeof(SaveLoader), nameof(SaveLoader.Save), typeof(string), typeof(bool), typeof(bool))]
	internal static class SaveLoader_Save_Patch {
		private static void Postfix() {
			if (!ModToggle.IsEnabled) return;
			Speech.SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.GAME_STATE.SAVED);
		}
	}

	/// <summary>
	/// Register ModInputRouter in ONI's input handler tree when the input system initializes.
	/// Idempotent: checks if already registered before adding.
	/// Note: InputInit is internal, so we use TargetMethod with AccessTools for type resolution.
	/// </summary>
	[HarmonyPatch]
	internal static class InputInit_Awake_Patch {
		private static System.Reflection.MethodBase TargetMethod() {
			var type = AccessTools.TypeByName("InputInit");
			if (type == null) {
				Log.Error("InputInit_Awake_Patch: InputInit type not found");
				return null;
			}
			var method = AccessTools.Method(type, "Awake");
			if (method == null)
				Log.Error("InputInit_Awake_Patch: InputInit.Awake method not found");
			return method;
		}

		private static void Postfix() {
			try {
				ModInputRouter.Register();

				var buildText = BuildWatermark.GetBuildText();
				if (!buildText.StartsWith("U59-736649"))
					Log.Warn($"Game build '{buildText}' is newer than last tested 'U59-736649'. Field names may have changed.");
			} catch (System.Exception ex) {
				Log.Error($"Failed to register ModInputRouter: {ex}");
			}
		}
	}
}
