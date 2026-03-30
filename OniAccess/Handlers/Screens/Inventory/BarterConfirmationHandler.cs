using System.Collections.Generic;

using HarmonyLib;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Handler for BarterConfirmationScreen (buy/sell confirmation dialog).
	/// Speaks the confirmation prompt and maps Enter to confirm, Escape to cancel.
	/// Speaks transaction result when the server responds.
	///
	/// Lifecycle: patched via BarterConfirmationScreen.OnActivate postfix.
	/// </summary>
	public class BarterConfirmationHandler: BaseScreenHandler {
		private enum State { Confirming, Loading, Result }

		private State _state;

		public BarterConfirmationHandler(KScreen screen) : base(screen) { }

		public override string DisplayName => "";

		public override bool CapturesAllInput => true;

		public override IReadOnlyList<HelpEntry> HelpEntries => null;

		public override void OnActivate() {
			base.OnActivate();
			_state = State.Confirming;

			// Read the confirmation text from the screen
			var t = Traverse.Create(_screen);
			string header = t.Field("panelHeaderLabel").GetValue<LocText>()?.text;
			string itemName = t.Field("itemLabel").GetValue<LocText>()?.text;
			string description = t.Field("transactionDescriptionLabel").GetValue<LocText>()?.text;
			string cost = t.Field("confirmButtonFilamentLabel").GetValue<LocText>()?.text;

			string message = header;
			if (!string.IsNullOrEmpty(itemName))
				message += ", " + itemName;
			if (!string.IsNullOrEmpty(description))
				message += ", " + description;
			if (!string.IsNullOrEmpty(cost))
				message += ", " + cost;

			SpeechPipeline.SpeakInterrupt(message);
		}

		public override bool Tick() {
			if (base.Tick()) return true;

			if (_state == State.Confirming) {
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) ||
					UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter)) {
					ClickConfirm();
					return true;
				}
			}

			return false;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (e.TryConsume(Action.Escape)) {
				ClickCancel();
				return true;
			}
			return false;
		}

		private void ClickConfirm() {
			var t = Traverse.Create(_screen);
			var button = t.Field("confirmButton").GetValue<KButton>();
			if (button != null) {
				_state = State.Loading;
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.INVENTORY.TRANSACTION_LOADING);
				button.SignalClick(KKeyCode.None);
			}
		}

		private void ClickCancel() {
			var t = Traverse.Create(_screen);
			var button = t.Field("cancelButton").GetValue<KButton>();
			if (button != null) {
				button.SignalClick(KKeyCode.None);
			}
		}

		/// <summary>
		/// Called from the patch when the result panel is shown.
		/// </summary>
		internal void OnTransactionResult(bool success) {
			_state = State.Result;
			if (success)
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.INVENTORY.TRANSACTION_SUCCESS);
			else
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.INVENTORY.TRANSACTION_FAILED);
		}
	}
}
