using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers;
using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Patches {
	/// <summary>
	/// Fires immediately on trapped/untrapped transitions regardless of which
	/// world is currently active. The game's own visual notification gate
	/// drops these alerts when the player isn't viewing the affected world,
	/// which can cost a blind player an in-game day of silent starvation.
	/// </summary>
	[HarmonyPatch(typeof(TrappedDuplicantDiagnostic), nameof(TrappedDuplicantDiagnostic.CheckTrapped))]
	internal static class TrappedDuplicantDiagnostic_CheckTrapped_Patch {
		private static readonly Dictionary<int, ColonyDiagnostic.DiagnosticResult.Opinion> lastOpinionByWorld
			= new Dictionary<int, ColonyDiagnostic.DiagnosticResult.Opinion>();
		private static readonly Dictionary<int, string> lastTrappedNameByWorld
			= new Dictionary<int, string>();

		private static void Postfix(TrappedDuplicantDiagnostic __instance,
			ref ColonyDiagnostic.DiagnosticResult __result) {
			if (!ModToggle.IsEnabled) return;
			if (!LoadGate.IsReady) return;
			try {
				int worldID = __instance.worldID;
				var current = __result.opinion;
				var previous = lastOpinionByWorld.TryGetValue(worldID, out var p)
					? p : ColonyDiagnostic.DiagnosticResult.Opinion.Unset;
				lastOpinionByWorld[worldID] = current;

				bool wasBad = previous == ColonyDiagnostic.DiagnosticResult.Opinion.Bad;
				bool isBad = current == ColonyDiagnostic.DiagnosticResult.Opinion.Bad;

				if (!wasBad && isBad) {
					string name = ExtractDupeName(__result);
					lastTrappedNameByWorld[worldID] = name;
					SpeechPipeline.SpeakInterrupt(
						string.Format(STRINGS.ONIACCESS.SPEECH.DUPE_TRAPPED, name));
					BaseScreenHandler.PlaySound("Diagnostic_Active_Bad");
				} else if (wasBad && !isBad) {
					string name = lastTrappedNameByWorld.TryGetValue(worldID, out var n)
						? n : STRINGS.UI.COLONY_DIAGNOSTICS.TRAPPEDDUPLICANTDIAGNOSTIC.STUCK;
					lastTrappedNameByWorld.Remove(worldID);
					SpeechPipeline.SpeakInterrupt(
						string.Format(STRINGS.ONIACCESS.SPEECH.DUPE_UNTRAPPED, name));
					BaseScreenHandler.PlaySound("HUD_Click");
				}
			} catch (System.Exception ex) {
				Log.Warn($"TrappedDuplicantDiagnostic_CheckTrapped_Patch: {ex.Message}");
			}
		}

		private static string ExtractDupeName(ColonyDiagnostic.DiagnosticResult result) {
			var target = result.clickThroughTarget;
			if (target == null || target.second == null)
				return STRINGS.UI.COLONY_DIAGNOSTICS.TRAPPEDDUPLICANTDIAGNOSTIC.STUCK;
			var identity = target.second.GetComponent<MinionIdentity>();
			return identity != null
				? identity.GetProperName()
				: STRINGS.UI.COLONY_DIAGNOSTICS.TRAPPEDDUPLICANTDIAGNOSTIC.STUCK;
		}
	}
}
