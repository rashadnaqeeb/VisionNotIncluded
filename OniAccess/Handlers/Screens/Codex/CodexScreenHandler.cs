using System.Collections.Generic;

using OniAccess.Input;

namespace OniAccess.Handlers.Screens.Codex {
	/// <summary>
	/// Handler for the CodexScreen (in-game Database/Incyclopedia).
	/// Two tabs: Categories (drill tree) and Content (flat reader).
	/// Tab cycling via Tab/Shift+Tab.
	///
	/// Lifecycle: Show-patch on CodexScreen.OnShow(bool).
	/// ChangeArticle postfix resets the content tab.
	/// </summary>
	public class CodexScreenHandler: TabbedScreenHandler {
		private enum TabId { Categories, Content }

		private readonly CategoriesTab _categoriesTab;
		private readonly ContentTab _contentTab;

		public CodexScreenHandler(KScreen screen) : base(screen) {
			_categoriesTab = new CategoriesTab(this);
			_contentTab = new ContentTab(this);
			SetTabs(_categoriesTab, _contentTab);
		}

		public override string DisplayName => STRINGS.UI.CODEX.TITLE;

		public override bool CapturesAllInput => true;

		internal CodexScreen CodexScreen => _screen as CodexScreen;

		internal ContentTab ContentTabRef => _contentTab;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			try {
				var field = HarmonyLib.Traverse.Create(_screen).Field("searchInputField")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn($"CodexScreenHandler: failed to deactivate search field: {ex.Message}");
			}

			ActiveTabIndex = (int)TabId.Categories;
			_categoriesTab.OnTabActivated(announce: false);
		}

		// ========================================
		// INPUT
		// ========================================

		protected override bool HandleTabKey() {
			if (ActiveTabIndex == (int)TabId.Content) {
				JumpToCategoriesOnArticle();
			} else {
				int dir = InputUtil.ShiftHeld() ? -1 : 1;
				CycleTab(dir);
			}
			return true;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			// Escape from content tab returns to categories instead of closing
			if (ActiveTabIndex == (int)TabId.Content && e.TryConsume(Action.Escape)) {
				JumpToCategoriesOnArticle();
				return true;
			}
			return false;
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		/// <summary>
		/// Switch to content tab. Called by CategoriesTab when a leaf entry is activated,
		/// and by OnArticleChanged for external navigations.
		/// </summary>
		internal void JumpToContentTab() {
			if (ActiveTabIndex == (int)TabId.Content) return;
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Content;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		/// <summary>
		/// Switch from content tab to categories, landing on the current article.
		/// </summary>
		private void JumpToCategoriesOnArticle() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Categories;
			PlaySound("HUD_Mouseover");
			string entryId = CodexScreen?.activeEntryID;
			_categoriesTab.OnTabActivatedOnEntry(announce: true, entryId: entryId);
		}

		/// <summary>
		/// Called from the ChangeArticle postfix patch.
		/// When the content tab is active, rebuilds and speaks the new article.
		/// When on the categories tab (external navigation via OpenCodexToEntry),
		/// switches to content tab automatically.
		/// When called from CategoriesTab.ActivateLeafItem, the categories tab is
		/// still active, so this switches to content. The subsequent JumpToContentTab
		/// call is then a no-op since we're already on content.
		/// </summary>
		internal void OnArticleChanged() {
			if (ActiveTabIndex == (int)TabId.Content)
				_contentTab.OnArticleChanged();
			else
				JumpToContentTab();
		}
	}
}
