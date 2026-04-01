using System.Collections.Generic;
using HarmonyLib;

using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for confirmation dialogs (ConfirmDialogScreen).
	/// Per locked decision: confirmation dialogs are treated as a vertical list.
	/// Focus starts on the dialog message text (Label widget), then buttons below.
	///
	/// ConfirmDialogScreen inherits KModalScreen (not KButtonMenu), so we manually
	/// find the message text and confirm/cancel buttons via Traverse and child walks.
	/// </summary>
	public class ConfirmDialogHandler: BaseWidgetHandler {
		private string _dialogTitle;
		private string _displayNameOverride;
		private bool _firstDiscovery = true;

		public override string DisplayName => _displayNameOverride ?? _dialogTitle
			?? (string)STRINGS.UI.FRONTEND.SAVESCREEN.CONFIRMNAME;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public ConfirmDialogHandler(KScreen screen, string displayNameOverride = null) : base(screen) {
			_displayNameOverride = displayNameOverride;
			HelpEntries = BuildHelpEntries();
		}

		public override void OnActivate() {
			_firstDiscovery = true;
			TryExtractTitle(_screen);
			base.OnActivate();
		}

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			// PopupConfirmDialog sets popupMessage.text after StartScreen returns,
			// which is after our Activate postfix fires. On the first call the
			// LocText still holds stale prefab text. Defer to let the caller set
			// the real message before we read it.
			if (_firstDiscovery) {
				_firstDiscovery = false;
				return false;
			}

			// Find the dialog message text via popupMessage field or child LocText
			string messageText = null;

			// Try popupMessage first (ConfirmDialogScreen's main message)
			var popupMessage = Traverse.Create(screen).Field("popupMessage")
				.GetValue<LocText>();
			if (popupMessage != null && !string.IsNullOrEmpty(popupMessage.text)) {
				messageText = popupMessage.text;
			}

			// If no popupMessage, search for a child LocText with content.
			// Skip any LocText that matches the already-extracted dialog title
			// (spoken as DisplayName) to avoid a redundant, confusing Label.
			if (string.IsNullOrEmpty(messageText)) {
				var locTexts = screen.GetComponentsInChildren<LocText>(false);
				foreach (var lt in locTexts) {
					if (lt != null && !string.IsNullOrEmpty(lt.text)
						&& lt.text != _dialogTitle) {
						messageText = lt.text;
						break;
					}
				}
			}

			// Add message as a Label widget (readable, not clickable)
			if (!string.IsNullOrEmpty(messageText)) {
				_widgets.Add(new LabelWidget {
					Label = messageText,
					GameObject = screen.gameObject
				});
			}

			// Text input fields (e.g., rename popup). Add before buttons so
			// the tab order is: message → input → actions.
			var inputFields = screen.GetComponentsInChildren<KInputTextField>(false);
			foreach (var field in inputFields) {
				if (field == null || !field.gameObject.activeInHierarchy) continue;
				_widgets.Add(new TextInputWidget {
					Label = field.text,
					Component = field,
					GameObject = field.gameObject
				});
			}

			// Find confirm/cancel buttons. ConfirmDialogScreen stores these as
			// GameObject fields (not KButton), so get as GameObject first, then
			// GetComponent<KButton>. InfoDialogScreen uses button panels instead.
			var screenTraverse = Traverse.Create(screen);
			bool foundNamedButtons = false;

			foundNamedButtons |= TryAddButtonField(screenTraverse, "confirmButton", (string)STRINGS.UI.CONFIRMDIALOG.OK);
			foundNamedButtons |= TryAddButtonField(screenTraverse, "cancelButton", (string)STRINGS.UI.FRONTEND.NEWGAMESETTINGS.BUTTONS.CANCEL);
			foundNamedButtons |= TryAddButtonField(screenTraverse, "configurableButton", null);

			// If no named buttons found, walk children for any KButton instances.
			// Covers InfoDialogScreen and other dialog types that add buttons
			// dynamically to leftButtonPanel/rightButtonPanel.
			if (!foundNamedButtons) {
				var kbuttons = screen.GetComponentsInChildren<KButton>(false);
				foreach (var kb in kbuttons) {
					if (kb == null || !kb.gameObject.activeInHierarchy
						|| !kb.isInteractable) continue;

					string label = GetButtonLabel(kb, null);
					if (string.IsNullOrEmpty(label)) continue;

					_widgets.Add(new ButtonWidget {
						Label = label,
						Component = kb,
						GameObject = kb.gameObject
					});
				}
			}

			Util.Log.Debug($"ConfirmDialogHandler.DiscoverWidgets: {_widgets.Count} widgets");
			return true;
		}

		/// <summary>
		/// Try to add a button from a named GameObject field on the screen.
		/// ConfirmDialogScreen stores confirmButton/cancelButton as GameObject,
		/// not KButton, so we get the GameObject first then GetComponent.
		/// Returns true if a button was successfully added.
		/// </summary>
		private bool TryAddButtonField(Traverse screenTraverse, string fieldName, string fallback) {
			try {
				var go = screenTraverse.Field(fieldName)
					.GetValue<UnityEngine.GameObject>();
				if (go == null || !go.activeInHierarchy) return false;

				var kb = go.GetComponent<KButton>();
				if (kb == null || !kb.isInteractable) return false;

				string label = GetButtonLabel(kb, fallback);
				if (string.IsNullOrEmpty(label)) return false;

				_widgets.Add(new ButtonWidget {
					Label = label,
					Component = kb,
					GameObject = go
				});
				return true;
			} catch (System.Exception ex) {
				Util.Log.Error($"ConfirmDialogHandler.TryAddButtonField: {ex.Message}");
				return false;
			}
		}


		/// <summary>
		/// Try to extract a title from the dialog's titleText or header field.
		/// If found, use it as the DisplayName instead of the generic "Confirm".
		/// Tries titleText first (ConfirmDialogScreen), then header (InfoDialogScreen).
		/// </summary>
		private void TryExtractTitle(KScreen screen) {
			try {
				var titleText = Traverse.Create(screen).Field("titleText")
					.GetValue<LocText>();
				if (titleText != null && !string.IsNullOrEmpty(titleText.text)) {
					_dialogTitle = titleText.text;
					return;
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ConfirmDialogHandler.TryExtractTitle(titleText): {ex.Message}");
			}
			try {
				var header = Traverse.Create(screen).Field("header")
					.GetValue<LocText>();
				if (header != null && !string.IsNullOrEmpty(header.text)) {
					_dialogTitle = header.text;
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ConfirmDialogHandler.TryExtractTitle(header): {ex.Message}");
			}
		}
	}
}
