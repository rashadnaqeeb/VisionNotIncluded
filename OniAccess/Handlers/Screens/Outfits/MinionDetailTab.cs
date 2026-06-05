using System.Collections.Generic;

using HarmonyLib;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Tab 2 of MinionBrowserHandler: detail panel for a selected duplicant.
	///
	/// Items:
	/// - Index 0: outfit type cycler (Left/Right cycles, Enter is no-op)
	/// - Index 1..N: outfit slot composition lines
	/// - Edit button (always shown; speaks tooltip when disabled)
	/// - Change Outfit button (hidden for JoyResponse)
	/// </summary>
	internal class MinionDetailTab: BaseMenuHandler, IScreenTab {
		private readonly MinionBrowserHandler _parent;
		private readonly List<DetailItem> _items = new List<DetailItem>();
		private MinionBrowserScreen.GridItem _gridItem;
		private int _outfitTypeIndex;

		internal MinionDetailTab(MinionBrowserHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.WARDROBE.DETAIL_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => null;

		internal MinionBrowserScreen.GridItem CurrentGridItem => _gridItem;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			CurrentIndex = 0;
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			SpeakCurrentItemQueued();
		}

		public void OnTabDeactivated() { }

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// LOADING
		// ========================================

		internal void LoadDupe(MinionBrowserScreen.GridItem item) {
			_gridItem = item;

			// Select this dupe on the game screen so the preview updates
			try {
				var screen = _parent.BrowserScreen;
				Traverse.Create(screen).Method("SelectMinion",
					new System.Type[] { typeof(MinionBrowserScreen.GridItem) })
					.GetValue(item);
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab.LoadDupe: failed to select minion: {ex.Message}");
			}

			// Read the current outfit type from the screen
			ReadOutfitTypeFromScreen();
			RebuildItems();
			CurrentIndex = 0;
		}

		private void ReadOutfitTypeFromScreen() {
			try {
				var screen = _parent.BrowserScreen;
				var outfitType = Traverse.Create(screen)
					.Field<ClothingOutfitUtility.OutfitType>("currentOutfitType").Value;
				_outfitTypeIndex = OutfitHelper.IndexOfAllOutfitType(outfitType);
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab: failed to read currentOutfitType: {ex.Message}");
				_outfitTypeIndex = 0;
			}
		}

		private void RebuildItems() {
			_items.Clear();
			if (_gridItem == null) return;

			var outfitType = OutfitHelper.GetAllOutfitType(_outfitTypeIndex);

			// Type cycler at index 0
			_items.Add(new DetailItem {
				text = OutfitHelper.GetAllOutfitTypeLabel(outfitType)
			});

			// Outfit name from the game's description panel
			// Skipped for JoyResponse — the composition line already shows the facade
			if (outfitType != ClothingOutfitUtility.OutfitType.JoyResponse)
				AddOutfitName();

			// Outfit composition
			if (outfitType == ClothingOutfitUtility.OutfitType.JoyResponse) {
				var composition = OutfitHelper.GetJoyResponseComposition(_gridItem);
				foreach (string line in composition)
					_items.Add(new DetailItem { text = line });
			} else {
				var outfitOpt = _gridItem.GetClothingOutfitTarget(outfitType);
				if (outfitOpt.HasValue) {
					var composition = OutfitHelper.GetOutfitComposition(outfitOpt.Unwrap());
					foreach (string line in composition)
						_items.Add(new DetailItem { text = line });
				}
			}

			// Edit button (always present, may be disabled)
			AddEditButton(outfitType);

			// Change Outfit button (hidden for JoyResponse)
			if (outfitType != ClothingOutfitUtility.OutfitType.JoyResponse)
				AddChangeOutfitButton();
		}

		// ========================================
		// BaseMenuHandler overrides
		// ========================================

		public override int ItemCount => _items.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].text;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_items.Count == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakInterrupt(Widgets.WidgetSpeech.ComposeLabel(text));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			var item = _items[CurrentIndex];

			switch (item.action) {
				case DetailAction.Edit:
					ActivateButton("editButton");
					break;
				case DetailAction.ChangeOutfit:
					ActivateButton("changeOutfitButton");
					break;
			}
		}

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (CurrentIndex != 0) return;

			int newIndex = ((_outfitTypeIndex + direction) % OutfitHelper.AllOutfitTypeCount
				+ OutfitHelper.AllOutfitTypeCount) % OutfitHelper.AllOutfitTypeCount;

			// Call the game's cycler to keep UI in sync — only update mod
			// state if the game-side call succeeds
			try {
				_parent.BrowserScreen.Cycler.GoTo(newIndex);
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab: failed to cycle outfit type: {ex.Message}");
				return;
			}

			_outfitTypeIndex = newIndex;
			RebuildItems();
			PlaySound("HUD_Click");
			SpeakCurrentItem();
		}

		// ========================================
		// BUTTONS
		// ========================================

		private void AddOutfitName() {
			try {
				var panel = Traverse.Create(_parent.BrowserScreen)
					.Field<OutfitDescriptionPanel>("outfitDescriptionPanel").Value;
				if (panel == null) return;

				string name = panel.outfitNameLabel?.text;
				if (!string.IsNullOrEmpty(name))
					_items.Add(new DetailItem { text = name });

				// JoyResponse shows the facade name in the description label
				if (panel.outfitDescriptionLabel != null
					&& panel.outfitDescriptionLabel.gameObject.activeSelf) {
					string desc = panel.outfitDescriptionLabel.text;
					if (!string.IsNullOrEmpty(desc))
						_items.Add(new DetailItem { text = desc });
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab: failed to read outfit name: {ex.Message}");
			}
		}

		private void AddEditButton(ClothingOutfitUtility.OutfitType outfitType) {
			var screen = _parent.BrowserScreen;
			try {
				var button = Traverse.Create(screen).Field<KButton>("editButton").Value;
				if (button == null) return;

				string label = Traverse.Create(screen)
					.Field<LocText>("editButtonText").Value?.text;
				if (string.IsNullOrEmpty(label))
					label = (string)STRINGS.UI.MINION_BROWSER_SCREEN.BUTTON_EDIT_OUTFIT_ITEMS;

				_items.Add(new DetailItem { text = label, action = DetailAction.Edit });
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab: failed to add edit button: {ex.Message}");
			}
		}

		private void AddChangeOutfitButton() {
			var screen = _parent.BrowserScreen;
			try {
				var button = Traverse.Create(screen)
					.Field<KButton>("changeOutfitButton").Value;
				if (button == null || !button.gameObject.activeSelf) return;

				string label = button.GetComponentInChildren<LocText>()?.text;
				if (string.IsNullOrEmpty(label))
					label = (string)STRINGS.UI.MINION_BROWSER_SCREEN.BUTTON_CHANGE_OUTFIT;

				_items.Add(new DetailItem { text = label, action = DetailAction.ChangeOutfit });
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab: failed to add change outfit button: {ex.Message}");
			}
		}

		private void ActivateButton(string fieldName) {
			var screen = _parent.BrowserScreen;
			try {
				var button = Traverse.Create(screen)
					.Field<KButton>(fieldName).Value;
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
					return;
				}

				button.SignalClick(KKeyCode.None);
				PlaySound("HUD_Click");
			} catch (System.Exception ex) {
				Util.Log.Warn($"MinionDetailTab.ActivateButton({fieldName}): {ex.Message}");
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private void SpeakCurrentItemQueued() {
			if (_items.Count == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(Widgets.WidgetSpeech.ComposeLabel(text));
		}

		private enum DetailAction { None, Edit, ChangeOutfit }

		private struct DetailItem {
			internal string text;
			internal DetailAction action;
		}
	}
}
