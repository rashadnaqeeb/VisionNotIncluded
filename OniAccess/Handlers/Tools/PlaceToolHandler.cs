using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers.Tiles;
using OniAccess.Input;
using OniAccess.Speech;
using UnityEngine;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Handler for PlaceTool (cargo lander placement from rockets).
	/// Sits on top of TileCursorHandler.
	/// CapturesAllInput = false so arrow keys fall through to TileCursorHandler.
	/// Space/Enter confirms placement, Escape cancels.
	/// </summary>
	public class PlaceToolHandler: IAccessHandler {
		private readonly Util.GameEventSubscription _toolChangedSub = new Util.GameEventSubscription();

		public string DisplayName => BuildActivationName();
		public bool CapturesAllInput => false;

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.Return),
		};
		public IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Space/Enter", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CONFIRM_TOOL),
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
			if ((UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)
				|| UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return))
				&& !InputUtil.AnyModifierHeld()) {
				TryConfirm();
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

		private void TryConfirm() {
			var tool = PlaceTool.Instance;
			if (tool == null) {
				ExitTool();
				return;
			}

			int cell = TileCursor.Instance.Cell;
			var source = Traverse.Create(tool)
				.Field("source").GetValue<Placeable>();
			if (source == null) {
				ExitTool();
				return;
			}

			if (!source.IsValidPlaceLocation(cell, out string reason)) {
				BaseScreenHandler.PlaySound("Negative");
				string message = string.IsNullOrEmpty(reason)
					? (string)STRINGS.ONIACCESS.TOOLS.PLACE_INVALID
					: reason;
				SpeechPipeline.SpeakInterrupt(message);
				return;
			}

			HandlerStack.Pop();
			var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
			tool.OnLeftClickDown(pos);
			tool.OnLeftClickUp(pos);
			BaseScreenHandler.PlaySound("HUD_Click");
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.BUILD_MENU.PLACED);
		}

		private static void ExitTool() {
			HandlerStack.Pop();
			SelectTool.Instance.Activate();
		}

		private static string BuildActivationName() {
			var tool = PlaceTool.Instance;
			if (tool == null)
				return Strings.Get("STRINGS.UI.TOOLS.PLACE.TOOLNAME");

			var source = Traverse.Create(tool)
				.Field("source").GetValue<Placeable>();
			if (source == null)
				return Strings.Get("STRINGS.UI.TOOLS.PLACE.TOOLNAME");

			var sel = source.gameObject.GetComponent<KSelectable>();
			string name = sel?.GetName();
			if (string.IsNullOrEmpty(name))
				return Strings.Get("STRINGS.UI.TOOLS.PLACE.TOOLNAME");

			return string.Format(
				(string)STRINGS.ONIACCESS.TOOLS.PLACE_ACTIVATION, name);
		}
	}
}
