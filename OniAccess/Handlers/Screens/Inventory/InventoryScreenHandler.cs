using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Handler for KleiInventoryScreen (blueprint gallery in the Supply Closet).
	/// Two tabs: Gallery (NestedMenuHandler) and Detail (flat reader + barter).
	/// Tab cycling via Tab/Shift+Tab.
	///
	/// Lifecycle: Show-patch on KleiInventoryScreen.OnShow(bool).
	/// </summary>
	public class InventoryScreenHandler: TabbedScreenHandler {
		private enum TabId { Gallery, Detail }

		private readonly GalleryTab _galleryTab;
		private readonly DetailTab _detailTab;

		public InventoryScreenHandler(KScreen screen) : base(screen) {
			_galleryTab = new GalleryTab(this);
			_detailTab = new DetailTab(this);
			SetTabs(_galleryTab, _detailTab);
		}

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.INVENTORY;

		public override bool CapturesAllInput => true;

		internal KleiInventoryScreen InventoryScreen => _screen as KleiInventoryScreen;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			// Deactivate the game's built-in search field
			try {
				var field = HarmonyLib.Traverse.Create(_screen).Field("searchField")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn($"InventoryScreenHandler: failed to deactivate search field: {ex.Message}");
			}

			// Ensure InventoryOrganization is initialized
			InventoryOrganization.Initialize();

			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.HANDLERS.INVENTORY);

			ActiveTabIndex = (int)TabId.Gallery;
			_galleryTab.OnTabActivated(announce: false);
		}

		// ========================================
		// INPUT
		// ========================================

		protected override bool HandleTabKey() {
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToGalleryOnPermit();
			} else {
				int dir = InputUtil.ShiftHeld() ? -1 : 1;
				CycleTab(dir);
			}
			return true;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			// Let the active tab handle Escape first (e.g., exit filter mode,
			// clear search). If consumed, don't close the screen.
			if (base.HandleKeyDown(e)) return true;

			if (!e.TryConsume(Action.Escape)) return false;

			// Escape from detail tab returns to gallery instead of closing
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToGalleryOnPermit();
				return true;
			}

			// Dismiss via LockerNavigator so it updates its navigation history.
			// PopScreen calls SetActive(false) → OnCmpDisable patch pops our handler.
			LockerNavigator.Instance?.PopScreen();
			return true;
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		/// <summary>
		/// Switch to detail tab with the given permit loaded.
		/// Called by GalleryTab when a leaf item is activated.
		/// </summary>
		internal void JumpToDetailTab(Database.PermitResource permit) {
			if (ActiveTabIndex == (int)TabId.Detail) return;
			_detailTab.LoadPermit(permit);
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Detail;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		/// <summary>
		/// Switch from detail tab to gallery, landing on the last-viewed permit.
		/// </summary>
		private void JumpToGalleryOnPermit() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Gallery;
			PlaySound("HUD_Mouseover");
			var permit = _detailTab.CurrentPermit;
			_galleryTab.OnTabActivatedOnPermit(announce: true, permit: permit);
		}
	}
}
