using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers.Tiles;
using OniAccess.Input;
using OniAccess.Speech;
using UnityEngine;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Handler for CopySettingsTool. Sits on top of TileCursorHandler.
	/// CapturesAllInput = false so arrow keys fall through to TileCursorHandler.
	/// Space applies settings and stays in tool, Enter applies and exits,
	/// Escape cancels.
	/// </summary>
	public class CopySettingsHandler: IAccessHandler {
		private readonly Util.GameEventSubscription _toolChangedSub = new Util.GameEventSubscription();

		public string DisplayName => BuildActivationName();
		public bool CapturesAllInput => false;

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.Return),
		};
		public IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Space", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.APPLY_SETTINGS),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.APPLY_AND_EXIT),
			new HelpEntry("Escape", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CANCEL_TOOL),
		}.AsReadOnly();
		public IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		public void OnActivate() {
			if (Game.Instance != null)
				_toolChangedSub.Subscribe(Game.Instance, 1174281782, OnActiveToolChanged);
			SpeechPipeline.SpeakInterrupt(DisplayName);
		}

		public void OnDeactivate() {
			_toolChangedSub.Unsubscribe();
		}

		private void OnActiveToolChanged(object data) {
			if (data is SelectTool) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.CANCELED);
				BaseScreenHandler.PlaySound("Tile_Cancel");
				HandlerStack.Pop();
			}
		}

		public bool Tick() {
			if (InputUtil.AnyModifierHeld()) return false;
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) {
				TryApply(exitAfter: false);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				TryApply(exitAfter: true);
				return true;
			}
			return false;
		}

		public bool HandleKeyDown(KButtonEvent e) {
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.CANCELED);
				BaseScreenHandler.PlaySound("Tile_Cancel");
				ExitTool();
				return true;
			}
			return false;
		}

		private void TryApply(bool exitAfter) {
			var tool = CopySettingsTool.Instance;
			if (tool == null) {
				ExitTool();
				return;
			}

			int cell = TileCursor.Instance.Cell;
			var sourceGO = Traverse.Create(tool)
				.Field("sourceGameObject").GetValue<GameObject>();

			// The source can be destroyed while the tool is armed. Unity keeps the
			// managed wrapper alive, so component lookups on it throw instead of
			// returning null. The game guards its own drag paths the same way.
			KPrefabID targetId = null;
			bool success = false;
			if (sourceGO != null) {
				targetId = CopyBuildingSettings.ResolveTarget(
					CopyBuildingSettings.ResolveLayer(sourceGO), cell);
				var sourceId = sourceGO.GetComponent<KPrefabID>();
				var sourceSettings = sourceGO.GetComponent<CopyBuildingSettings>();
				success = targetId != null && sourceId != null && sourceSettings != null
					&& targetId.gameObject != sourceGO
					&& CopyBuildingSettings.ApplyCopy(targetId, sourceGO, sourceId, sourceSettings);
			}
			if (success) {
				var targetGO = targetId.gameObject;
				if (FarmCopyFailed(sourceGO, targetGO)) {
					BaseScreenHandler.PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.TOOLS.COPY_SETTINGS_FAILED);
				} else {
					BaseScreenHandler.PlaySound("HUD_Click");
					string targetName = targetGO?.GetComponent<KSelectable>()?.GetName();
					string appliedText = targetName != null
						? targetName + ", " + (string)STRINGS.UI.COPIED_SETTINGS
						: (string)STRINGS.UI.COPIED_SETTINGS;
					SpeechPipeline.SpeakInterrupt(appliedText);
				}
			} else {
				BaseScreenHandler.PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.COPY_SETTINGS_NO_TARGET);
			}

			if (exitAfter) {
				SpeechPipeline.SpeakQueued(
					(string)STRINGS.ONIACCESS.TOOLS.DONE);
				ExitTool();
			}
		}

		private static void ExitTool() {
			Screens.DetailsScreenHandler.SuppressNextActivation = true;
			HandlerStack.Pop();
			SelectTool.Instance.Activate();
		}

		private static string BuildActivationName() {
			var tool = CopySettingsTool.Instance;
			if (tool == null)
				return (string)STRINGS.UI.USERMENUACTIONS.COPY_BUILDING_SETTINGS.NAME;

			var sourceGO = Traverse.Create(tool)
				.Field("sourceGameObject").GetValue<GameObject>();
			if (sourceGO == null)
				return (string)STRINGS.UI.USERMENUACTIONS.COPY_BUILDING_SETTINGS.NAME;

			var sel = sourceGO.GetComponent<KSelectable>();
			string name = sel?.GetName();
			if (string.IsNullOrEmpty(name))
				return (string)STRINGS.UI.USERMENUACTIONS.COPY_BUILDING_SETTINGS.NAME;

			return string.Format(
				(string)STRINGS.ONIACCESS.TOOLS.COPY_SETTINGS_ACTIVATION, name);
		}

		private static bool FarmCopyFailed(GameObject sourceGO, GameObject targetGO) {
			if (targetGO == null) return false;
			var destPlot = targetGO.GetComponent<PlantablePlot>();
			if (destPlot == null) return false;
			var sourcePlot = sourceGO.GetComponent<PlantablePlot>();
			if (sourcePlot == null) return false;
			return GetEffectiveSeedTag(sourcePlot) != GetEffectiveSeedTag(destPlot);
		}

		private static Tag GetEffectiveSeedTag(PlantablePlot plot) {
			if (plot.Occupant != null) {
				var seedProducer = plot.Occupant.GetComponent<SeedProducer>();
				if (seedProducer != null)
					return TagManager.Create(seedProducer.seedInfo.seedId);
			}
			return plot.requestedEntityTag;
		}
	}
}
