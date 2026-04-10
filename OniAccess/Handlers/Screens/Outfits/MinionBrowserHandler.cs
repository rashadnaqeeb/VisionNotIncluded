using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Handler for MinionBrowserScreen (Duplicants tab of the Supply Closet).
	/// Two tabs: List (duplicant grid) and Detail (outfit type cycler, composition,
	/// action buttons).
	///
	/// MinionBrowserScreen extends KMonoBehaviour (not KScreen), so this handler
	/// bypasses ContextDetector. Harmony patches on OnCmpEnable/OnCmpDisable
	/// push and pop it directly on the HandlerStack.
	/// </summary>
	public class MinionBrowserHandler: TabbedScreenHandler {
		private enum TabId { List, Detail }

		private readonly MinionDupeListTab _listTab;
		private readonly MinionDetailTab _detailTab;
		private readonly MinionBrowserScreen _browserScreen;

		public MinionBrowserHandler(MinionBrowserScreen screen) : base(screen: null) {
			_browserScreen = screen;
			_listTab = new MinionDupeListTab(this);
			_detailTab = new MinionDetailTab(this);
			SetTabs(_listTab, _detailTab);
		}

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.DUPLICANT_BROWSER;

		public override bool CapturesAllInput => true;

		internal MinionBrowserScreen BrowserScreen => _browserScreen;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.HANDLERS.DUPLICANT_BROWSER);

			ActiveTabIndex = (int)TabId.List;
			_listTab.OnTabActivated(announce: false);
		}

		// ========================================
		// INPUT
		// ========================================

		protected override bool HandleTabKey() {
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToListOnDupe();
			} else {
				int dir = InputUtil.ShiftHeld() ? -1 : 1;
				CycleTab(dir);
			}
			return true;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			if (!e.TryConsume(Action.Escape)) return false;

			// Escape from detail tab returns to list
			if (ActiveTabIndex == (int)TabId.Detail) {
				JumpToListOnDupe();
				return true;
			}

			// Dismiss via LockerNavigator so it updates its navigation history
			if (LockerNavigator.Instance != null
				&& LockerNavigator.Instance.isActiveAndEnabled)
				LockerNavigator.Instance.PopScreen();
			return true;
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		/// <summary>
		/// Switch to detail tab with the given dupe loaded.
		/// Called by MinionDupeListTab when a dupe is activated.
		/// </summary>
		internal void JumpToDetailTab(MinionBrowserScreen.GridItem item) {
			if (ActiveTabIndex == (int)TabId.Detail) return;

			_detailTab.LoadDupe(item);
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Detail;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		/// <summary>
		/// Switch from detail tab to list, landing on the last-viewed dupe.
		/// </summary>
		private void JumpToListOnDupe() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.List;
			PlaySound("HUD_Mouseover");
			var dupe = _detailTab.CurrentGridItem;
			_listTab.OnTabActivatedOnDupe(announce: true, dupe);
		}
	}
}
