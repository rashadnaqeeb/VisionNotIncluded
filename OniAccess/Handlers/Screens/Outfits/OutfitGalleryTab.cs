using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Gallery tab for the wardrobe screen: flat list of outfits with an
	/// outfit-type selector as the first item.
	///
	/// Index 0 = outfit type selector (Clothing / AtmoSuit / JetSuit).
	///           Left/Right cycles between types.
	/// Index 1 = "None" entry (only when picking for a dupe; clears the outfit).
	/// Index 1..N (or 2..N) = outfit entries for the current type.
	/// Last = "New outfit" entry (opens the outfit designer).
	///
	/// Enter on "None" jumps to the detail tab with no outfit selected.
	/// Enter on an outfit jumps to the detail tab.
	/// Enter on "New outfit" clicks the game's add button.
	/// Type-ahead searches all items including the type selector and "New outfit".
	/// </summary>
	internal class OutfitGalleryTab: BaseMenuHandler, IScreenTab {
		private readonly OutfitBrowserHandler _parent;
		private List<ClothingOutfitTarget> _outfits;
		private ClothingOutfitUtility.OutfitType _outfitType;
		private int _outfitTypeIndex;

		internal OutfitGalleryTab(OutfitBrowserHandler parent) : base(screen: null) {
			_parent = parent;
			_outfitType = ClothingOutfitUtility.OutfitType.Clothing;
			_outfitTypeIndex = 0;
		}

		private bool HasNoneEntry =>
			_parent.BrowserScreen.Config.isPickingOutfitForDupe;

		public string TabName => (string)STRINGS.ONIACCESS.WARDROBE.GALLERY_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => ListNavHelpEntries;

		internal ClothingOutfitUtility.OutfitType CurrentOutfitType => _outfitType;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			_outfits = null;
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(label));
			}
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		/// <summary>
		/// Reactivate the gallery tab, landing on the outfit that was
		/// previously being viewed in the detail tab.
		/// </summary>
		internal void OnTabActivatedOnOutfit(bool announce, ClothingOutfitTarget? outfit) {
			_outfits = null;
			if (outfit.HasValue && !NavigateToOutfit(outfit.Value)) {
				// Outfit not found (may have been deleted) — clamp cursor
				if (CurrentIndex >= ItemCount && ItemCount > 0)
					CurrentIndex = ItemCount - 1;
			}
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(label));
			}
		}

		// ========================================
		// BaseMenuHandler overrides
		// ========================================

		// +1 for type selector at top, +1 for "New outfit" at bottom,
		// +1 for "None" entry when picking for a dupe
		public override int ItemCount => GetOutfits().Count + 2 + (HasNoneEntry ? 1 : 0);

		public override string GetItemLabel(int index) {
			if (index == 0)
				return OutfitHelper.GetOutfitTypeLabel(_outfitType);

			int offset = 1;

			if (HasNoneEntry) {
				if (index == offset)
					return KleiItemsUI.GetNoneOutfitName(_outfitType);
				offset++;
			}

			var outfits = GetOutfits();
			int outfitIndex = index - offset;

			if (outfitIndex >= 0 && outfitIndex < outfits.Count)
				return OutfitHelper.GetOutfitLabel(outfits[outfitIndex]);

			// Last item: "New outfit"
			if (outfitIndex == outfits.Count)
				return (string)STRINGS.UI.OUTFIT_BROWSER_SCREEN.BUTTON_ADD_OUTFIT;

			return null;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (ItemCount == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= ItemCount) return;
			string text = GetItemLabel(CurrentIndex);
			if (string.IsNullOrEmpty(text)) return;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(text));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex == 0) return; // type selector — no activate

			int offset = 1;

			if (HasNoneEntry) {
				if (CurrentIndex == offset) {
					PlaySound("HUD_Click_Open");
					_parent.JumpToDetailTabNone();
					return;
				}
				offset++;
			}

			var outfits = GetOutfits();
			int outfitIndex = CurrentIndex - offset;

			if (outfitIndex >= 0 && outfitIndex < outfits.Count) {
				PlaySound("HUD_Click_Open");
				_parent.JumpToDetailTab(outfits[outfitIndex]);
				return;
			}

			// "New outfit" — click the game's add button
			if (outfitIndex == outfits.Count) {
				_parent.ActivateNewOutfit(_outfitType);
			}
		}

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (CurrentIndex != 0) return;

			_outfitTypeIndex = (_outfitTypeIndex + direction + OutfitHelper.OutfitTypeCount)
				% OutfitHelper.OutfitTypeCount;
			_outfitType = OutfitHelper.GetOutfitType(_outfitTypeIndex);
			_outfits = null;
			PlaySound("HUD_Click");
			SpeakCurrentItem();
		}

		// ========================================
		// DATA
		// ========================================

		private List<ClothingOutfitTarget> GetOutfits() {
			if (_outfits == null)
				_outfits = OutfitHelper.GetOutfitsForType(_outfitType);
			return _outfits;
		}

		private bool NavigateToOutfit(ClothingOutfitTarget outfit) {
			var outfits = GetOutfits();
			int offset = 1 + (HasNoneEntry ? 1 : 0);
			for (int i = 0; i < outfits.Count; i++) {
				if (outfits[i].Equals(outfit)) {
					CurrentIndex = i + offset;
					_search.Clear();
					SuppressSearchThisFrame();
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Set the outfit type externally (e.g., when the browser is opened
		/// with a locked outfit type from the Duplicants screen).
		/// </summary>
		internal void SetOutfitType(ClothingOutfitUtility.OutfitType type) {
			_outfitType = type;
			_outfitTypeIndex = OutfitHelper.IndexOfOutfitType(type);
			_outfits = null;
		}
	}
}
