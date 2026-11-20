using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers.Tiles;
using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Tools {
	/// <summary>
	/// Handler for MoveToLocationTool. Sits on top of TileCursorHandler
	/// (the game pops DetailsScreenHandler when the tool activates).
	/// CapturesAllInput = false so arrow keys fall through to TileCursorHandler.
	/// Space/Enter confirms the destination, Escape cancels.
	///
	/// On exit, sets DetailsScreenHandler.SuppressNextActivation so the
	/// details screen reopens silently (the player already knows which
	/// entity they're looking at).
	/// </summary>
	public class MoveToLocationHandler: IAccessHandler {
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
				PlayCancelSound();
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
				PlayCancelSound();
				ExitTool();
				return true;
			}
			return false;
		}

		private void TryConfirm() {
			var tool = MoveToLocationTool.Instance;
			if (tool == null) {
				ExitTool();
				return;
			}

			// The game distinguishes a target that no longer exists from a cell it
			// simply cannot reach; CanMoveTo returns false for both.
			if (GetTargetGO(tool) == null) {
				BaseScreenHandler.PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.MOVE_TO_NO_TARGET);
				return;
			}

			int cell = TileCursor.Instance.Cell;
			if (!tool.CanMoveTo(cell)) {
				BaseScreenHandler.PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.MOVE_TO_UNREACHABLE);
				return;
			}

			Traverse.Create(tool).Method("SetMoveToLocation", cell).GetValue(cell);
			PlayConfirmSound();
			ExitTool();
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.TOOLS.MOVE_TO_CONFIRMED);
		}

		private static void ExitTool() {
			Screens.DetailsScreenHandler.SuppressNextActivation = true;
			HandlerStack.Pop();
			SelectTool.Instance.Activate();
		}

		private static string BuildActivationName() {
			var tool = MoveToLocationTool.Instance;
			if (tool == null)
				return Strings.Get("STRINGS.UI.TOOLS.MOVETOLOCATION.TOOLNAME");

			var targetGO = GetTargetGO(tool);
			string entityName = targetGO != null
				? targetGO.GetComponent<KSelectable>()?.GetName()
				: null;

			if (string.IsNullOrEmpty(entityName))
				return Strings.Get("STRINGS.UI.TOOLS.MOVETOLOCATION.TOOLNAME");
			return string.Format(
				(string)STRINGS.ONIACCESS.TOOLS.MOVE_TO_ACTIVATION, entityName);
		}

		/// <summary>
		/// The object being relocated, or null once it has been destroyed.
		/// </summary>
		private static UnityEngine.GameObject GetTargetGO(MoveToLocationTool tool) {
			var nav = Traverse.Create(tool).Field("targetNavigator").GetValue<Navigator>();
			if (nav != null) return nav.gameObject;
			var movable = Traverse.Create(tool).Field("targetMovable").GetValue<Movable>();
			if (movable != null) return movable.gameObject;
			return null;
		}

		private static void PlayConfirmSound() {
			BaseScreenHandler.PlaySound("HUD_Click");
		}

		private static void PlayCancelSound() {
			BaseScreenHandler.PlaySound("Tile_Cancel");
		}
	}
}
