using Database;
using HarmonyLib;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Handler for JoyResponseDesignerScreen (restyle overjoyed response in Supply Closet).
	/// Flat BaseMenuHandler: gallery items (balloon artist facades) + Apply button.
	///
	/// JoyResponseDesignerScreen extends KMonoBehaviour (not KScreen), so this
	/// handler bypasses ContextDetector. Harmony patches on OnCmpEnable/
	/// OnCmpDisable push and pop it directly on the HandlerStack.
	/// </summary>
	public class JoyResponseDesignerHandler: BaseMenuHandler {
		private readonly JoyResponseDesignerScreen _designerScreen;

		public JoyResponseDesignerHandler(JoyResponseDesignerScreen screen) : base(screen: null) {
			_designerScreen = screen;
		}

		internal JoyResponseDesignerScreen DesignerScreen => _designerScreen;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.JOY_RESPONSE_DESIGNER;

		public override bool CapturesAllInput => true;

		private static readonly System.Collections.Generic.List<HelpEntry> _helpEntries =
			new System.Collections.Generic.List<HelpEntry> {
				new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			};

		public override System.Collections.Generic.IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.HANDLERS.JOY_RESPONSE_DESIGNER);

			// Navigate to the initially selected item if set
			if (_designerScreen.Config.initalSelectedItem.IsSome()) {
				var initial = _designerScreen.Config.initalSelectedItem.Unwrap();
				var items = _designerScreen.joyResponseCategories[0].items;
				for (int i = 0; i < items.Length; i++) {
					if (items[i].Equals(initial)) {
						CurrentIndex = i;
						break;
					}
				}
			}

			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(Widgets.WidgetSpeech.ComposeLabel(label));
			}
		}

		// ========================================
		// LIST DESCRIPTION
		// ========================================

		public override int ItemCount {
			get {
				var items = _designerScreen.joyResponseCategories[0].items;
				return items.Length + 1; // gallery items + Apply button
			}
		}

		public override string GetItemLabel(int index) {
			var items = _designerScreen.joyResponseCategories[0].items;

			if (index < items.Length) {
				var galleryItem = items[index];
				string name = galleryItem.GetName();

				var permitOpt = galleryItem.GetPermitResource();
				if (permitOpt.IsSome()) {
					var permit = permitOpt.Unwrap();
					string rarity = permit.Rarity.GetLocStringName();
					if (permit.IsOwnableOnServer()) {
						int count = PermitItems.GetOwnedCount(permit);
						if (count > 0)
							name += ", " + rarity;
						else
							name += ", " + rarity + ", "
								+ (string)STRINGS.ONIACCESS.INVENTORY.UNOWNED;
					} else {
						name += ", " + rarity;
					}

					if (!string.IsNullOrEmpty(permit.Description)
						&& !permit.Description.Equals("n/a", System.StringComparison.OrdinalIgnoreCase))
						name += ", " + permit.Description;
				}

				// Mark the currently selected item
				var selected = Traverse.Create(_designerScreen)
					.Field<JoyResponseDesignerScreen.GalleryItem>("selectedGalleryItem").Value;
				if (selected != null && selected.Equals(galleryItem))
					name += ", " + (string)STRINGS.ONIACCESS.OUTFIT_DESIGNER.SELECTED;

				return name;
			}

			// Apply button
			var button = Traverse.Create(_designerScreen)
				.Field<KButton>("primaryButton").Value;
			if (button != null)
				return button.GetComponentInChildren<LocText>().text;

			return null;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			string label = GetItemLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				SpeechPipeline.SpeakInterrupt(Widgets.WidgetSpeech.ComposeLabel(label));
		}

		// ========================================
		// ACTIVATION
		// ========================================

		protected override void ActivateCurrentItem() {
			var items = _designerScreen.joyResponseCategories[0].items;

			if (CurrentIndex < items.Length) {
				var galleryItem = items[CurrentIndex];
				_designerScreen.SelectGalleryItem(galleryItem);
				PlaySound("HUD_Click");
				SpeakCurrentItem();
				return;
			}

			// Apply button — same disabled-button-with-tooltip pattern as OutfitDesignerHandler
			var button = Traverse.Create(_designerScreen)
				.Field<KButton>("primaryButton").Value;
			if (button == null) return;

			if (!button.isInteractable) {
				PlaySound("Negative");
				var tooltip = button.gameObject.GetComponent<ToolTip>();
				if (tooltip != null) {
					string reason = Widgets.WidgetOps.ReadAllTooltipText(tooltip);
					if (!string.IsNullOrEmpty(reason)) {
						SpeechPipeline.SpeakInterrupt(reason);
						return;
					}
				}
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE);
				return;
			}

			button.SignalClick(KKeyCode.None);
			PlaySound("HUD_Click");
		}

		// ========================================
		// ESCAPE
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			if (!e.TryConsume(Action.Escape)) return false;

			LockerNavigator.Instance?.PopScreen();
			return true;
		}
	}
}
