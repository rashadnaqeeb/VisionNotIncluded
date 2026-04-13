using System.Collections.Generic;

using OniAccess.Input;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for PrinterceptorScreen (Hijacked Headquarters "Choose Blueprint" catalog).
	/// Two tabs: Catalog (nested menu of Critters and Plants) and Details (name,
	/// description, cost, wallet, Print button).
	///
	/// Lifecycle: Show-patch on PrinterceptorScreen.Show(bool). Screen is a
	/// singleton that calls Show(false) during OnActivate init.
	/// </summary>
	public class PrinterceptorScreenHandler: TabbedScreenHandler {
		private enum TabId { Catalog, Details }

		private readonly PrinterceptorCatalogTab _catalogTab;
		private readonly PrinterceptorDetailsTab _detailsTab;

		private Tag _selectedTag;

		public PrinterceptorScreenHandler(KScreen screen) : base(screen) {
			_catalogTab = new PrinterceptorCatalogTab(this);
			_detailsTab = new PrinterceptorDetailsTab(this);
			SetTabs(_catalogTab, _detailsTab);
		}

		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.PRINTERCEPTOR;

		public override bool CapturesAllInput => true;

		internal PrinterceptorScreen PrinterceptorScreen => _screen as PrinterceptorScreen;

		internal Tag SelectedTag => _selectedTag;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			ActiveTabIndex = (int)TabId.Catalog;
			_catalogTab.OnTabActivated(announce: false);
		}

		// ========================================
		// INPUT
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			// Escape from details tab returns to catalog instead of closing
			if (ActiveTabIndex == (int)TabId.Details && e.TryConsume(Action.Escape)) {
				JumpToCatalogTab();
				return true;
			}
			return false;
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		internal void SetSelectedEntity(Tag tag) {
			_selectedTag = tag;
		}

		internal void SwitchToDetailsTab(bool announce) {
			if (ActiveTabIndex == (int)TabId.Details) return;
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Details;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce);
		}

		private void JumpToCatalogTab() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Catalog;
			PlaySound("HUD_Mouseover");
			_catalogTab.OnTabActivated(announce: true);
		}
	}
}
