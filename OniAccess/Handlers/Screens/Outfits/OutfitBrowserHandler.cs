using System.Collections.Generic;

using HarmonyLib;

using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Handler for OutfitBrowserScreen (wardrobe in the Supply Closet).
	/// Two tabs: Gallery (flat outfit list with type selector) and Detail
	/// (slot composition + action buttons).
	///
	/// OutfitBrowserScreen extends KMonoBehaviour (not KScreen), so this
	/// handler bypasses ContextDetector. Harmony patches on OnCmpEnable/
	/// OnCmpDisable push and pop it directly on the HandlerStack.
	/// </summary>
	public class OutfitBrowserHandler : TabbedScreenHandler {
		private enum TabId { Gallery, Detail }

		private readonly OutfitGalleryTab _galleryTab;
		private readonly OutfitDetailTab _detailTab;
		private readonly OutfitBrowserScreen _browserScreen;

		public OutfitBrowserHandler(OutfitBrowserScreen screen) : base(screen: null) {
			_browserScreen = screen;
			_galleryTab = new OutfitGalleryTab(this);
			_detailTab = new OutfitDetailTab(this);
			SetTabs(_galleryTab, _detailTab);
		}

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.WARDROBE;

		public override bool CapturesAllInput => true;

		internal OutfitBrowserScreen BrowserScreen => _browserScreen;

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_PANEL),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			// Deactivate the game's search field so it doesn't capture keystrokes
			try {
				var searchBar = Traverse.Create(_browserScreen)
					.Field("categoriesAndSearchBar").GetValue<object>();
				if (searchBar != null) {
					var field = Traverse.Create(searchBar)
						.Field<KInputTextField>("searchTextField").Value;
					if (field != null)
						field.DeactivateInputField();
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"OutfitBrowserHandler: failed to deactivate search field: {ex.Message}");
			}

			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.HANDLERS.WARDROBE);

			// If the browser was opened locked to a specific outfit type,
			// set the gallery to that type
			if (_browserScreen.Config.onlyShowOutfitType.HasValue)
				_galleryTab.SetOutfitType(
					_browserScreen.Config.onlyShowOutfitType.Unwrap());

			ActiveTabIndex = (int)TabId.Gallery;
			_galleryTab.OnTabActivated(announce: false);
		}

		// ========================================
		// INPUT
		// ========================================

		protected override bool HandleTabKey() {
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToGalleryOnOutfit();
			} else {
				int dir = InputUtil.ShiftHeld() ? -1 : 1;
				CycleTab(dir);
			}
			return true;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			if (!e.TryConsume(Action.Escape)) return false;

			// Escape from detail tab returns to gallery
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToGalleryOnOutfit();
				return true;
			}

			// Dismiss via LockerNavigator so it updates its navigation history.
			// Guard: PopScreen crashes if the history is empty (e.g., if the
			// screen was already popped by a re-configure cycle).
			if (LockerNavigator.Instance != null
				&& LockerNavigator.Instance.isActiveAndEnabled)
				LockerNavigator.Instance.PopScreen();
			return true;
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		/// <summary>
		/// Switch to detail tab with the given outfit loaded.
		/// Called by OutfitGalleryTab when an outfit is activated.
		/// </summary>
		internal void JumpToDetailTab(ClothingOutfitTarget outfit) {
			if (ActiveTabIndex == (int)TabId.Detail) return;

			// Select the outfit on the game screen so the preview updates
			_browserScreen.state.SelectedOutfitOpt = outfit;

			_detailTab.LoadOutfit(outfit);
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Detail;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		/// <summary>
		/// Switch from detail tab to gallery, landing on the last-viewed outfit.
		/// </summary>
		private void JumpToGalleryOnOutfit() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Gallery;
			PlaySound("HUD_Mouseover");
			var outfit = _detailTab.CurrentOutfit;
			_galleryTab.OnTabActivatedOnOutfit(announce: true, outfit: outfit);
		}

		/// <summary>
		/// Click the "New outfit" button on the game screen.
		/// Opens the OutfitDesignerScreen for a blank outfit.
		/// </summary>
		internal void ActivateNewOutfit(ClothingOutfitUtility.OutfitType outfitType) {
			var addButton = _browserScreen.gameObject
				.transform.Find("InventoryColumn")
				?.Find("AddOutfitButtonGridItem");
			if (addButton != null) {
				var toggle = addButton.GetComponent<MultiToggle>();
				if (toggle != null) {
					toggle.onClick?.Invoke();
					PlaySound("HUD_Click_Open");
					return;
				}
			}

			// Fallback: open the designer directly via config
			try {
				var config = new OutfitDesignerScreenConfig(
					ClothingOutfitTarget.ForNewTemplateOutfit(outfitType),
					_browserScreen.Config.minionPersonality,
					_browserScreen.Config.targetMinionInstance,
					null);
				config.ApplyAndOpenScreen();
				PlaySound("HUD_Click_Open");
			} catch (System.Exception ex) {
				Util.Log.Error($"OutfitBrowserHandler.ActivateNewOutfit: {ex.Message}");
			}
		}
	}
}
