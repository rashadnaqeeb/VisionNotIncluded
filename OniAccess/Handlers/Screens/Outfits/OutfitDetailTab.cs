using System.Collections.Generic;

using HarmonyLib;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Detail tab for the wardrobe screen: flat list showing an outfit's
	/// slot composition and action buttons.
	///
	/// Items:
	/// - Outfit name
	/// - Per-slot lines ("Top: Classic Shirt", "Gloves: empty", etc.)
	/// - Pick (only when isPickingOutfitForDupe)
	/// - Edit
	/// - Rename (only when CanWriteName)
	/// - Delete (only when CanDelete)
	/// </summary>
	internal class OutfitDetailTab: BaseMenuHandler, IScreenTab {
		private readonly OutfitBrowserHandler _parent;
		private readonly List<DetailItem> _items = new List<DetailItem>();
		private ClothingOutfitTarget? _outfit;

		internal OutfitDetailTab(OutfitBrowserHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.WARDROBE.DETAIL_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => null;

		internal ClothingOutfitTarget? CurrentOutfit => _outfit;

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

		internal void LoadOutfit(ClothingOutfitTarget outfit) {
			_outfit = outfit;
			RebuildItems();
			CurrentIndex = 0;
		}

		private void RebuildItems() {
			_items.Clear();
			if (!_outfit.HasValue) return;

			var outfit = _outfit.Value;

			// Outfit name
			_items.Add(new DetailItem { text = outfit.ReadName() });

			// Slot composition
			var composition = OutfitHelper.GetOutfitComposition(outfit);
			foreach (string line in composition)
				_items.Add(new DetailItem { text = line });

			// Action buttons — read labels from live button text
			var browserScreen = _parent.BrowserScreen;
			if (browserScreen != null) {
				// Pick (only when picking for a dupe)
				if (browserScreen.Config.isPickingOutfitForDupe)
					TryAddButton(browserScreen, "pickOutfitButton", DetailAction.Pick);

				TryAddButton(browserScreen, "editOutfitButton", DetailAction.Edit);

				if (outfit.CanWriteName)
					TryAddButton(browserScreen, "renameOutfitButton", DetailAction.Rename,
						(string)STRINGS.UI.OUTFIT_BROWSER_SCREEN.TOOLTIP_RENAME_OUTFIT);

				if (outfit.CanDelete)
					TryAddButton(browserScreen, "deleteOutfitButton", DetailAction.Delete,
						(string)STRINGS.UI.OUTFIT_BROWSER_SCREEN.TOOLTIP_DELETE_OUTFIT);
			}
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
				SpeechPipeline.SpeakInterrupt(text);
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			var item = _items[CurrentIndex];

			switch (item.action) {
				case DetailAction.Pick:
					ClickBrowserButton("pickOutfitButton");
					break;
				case DetailAction.Edit:
					ClickBrowserButton("editOutfitButton");
					break;
				case DetailAction.Rename:
					ClickBrowserButton("renameOutfitButton");
					break;
				case DetailAction.Delete:
					ClickBrowserButton("deleteOutfitButton");
					break;
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private void ClickBrowserButton(string fieldName) {
			var browserScreen = _parent.BrowserScreen;
			if (browserScreen == null) return;

			var button = Traverse.Create(browserScreen)
				.Field<KButton>(fieldName).Value;
			if (button != null && button.isInteractable) {
				button.SignalClick(KKeyCode.None);
				PlaySound("HUD_Click");
			}
		}

		private void TryAddButton(OutfitBrowserScreen screen, string fieldName, DetailAction action, string fallbackLabel = null) {
			var button = Traverse.Create(screen).Field<KButton>(fieldName).Value;
			if (button == null || !button.isInteractable) return;
			string label = button.GetComponentInChildren<LocText>()?.text;
			if (string.IsNullOrEmpty(label))
				label = fallbackLabel;
			if (string.IsNullOrEmpty(label)) return;
			_items.Add(new DetailItem { text = label, action = action });
		}

		private void SpeakCurrentItemQueued() {
			if (_items.Count == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		private enum DetailAction { None, Pick, Edit, Rename, Delete }

		private struct DetailItem {
			internal string text;
			internal DetailAction action;
		}
	}
}
