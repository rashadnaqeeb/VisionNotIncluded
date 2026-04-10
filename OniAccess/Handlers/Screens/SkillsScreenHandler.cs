using System.Collections.Generic;

using Database;

using OniAccess.Handlers.Screens.Skills;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the SkillsScreen. Manages three tabs:
	/// Duplicants (flat dupe list), Skills (categorized skill browser),
	/// and Tree (DAG graph navigation).
	///
	/// Tab cycling via Tab/Shift+Tab. Each tab is a composed object.
	/// Tracks the currently selected duplicant as shared state.
	///
	/// Lifecycle: Show-patch on SkillsScreen.Show(bool).
	/// </summary>
	public class SkillsScreenHandler: TabbedScreenHandler {
		private enum TabId { Duplicants, Skills, Tree }

		private readonly DupeTab _dupeTab;
		private readonly SkillsTab _skillsTab;
		private readonly TreeTab _treeTab;

		private IAssignableIdentity _selectedDupe;

		public SkillsScreenHandler(KScreen screen) : base(screen) {
			_dupeTab = new DupeTab(this);
			_skillsTab = new SkillsTab(this);
			_treeTab = new TreeTab(this);
			SetTabs(_dupeTab, _skillsTab, _treeTab);
		}

		public override string DisplayName => STRINGS.ONIACCESS.SKILLS.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		internal IAssignableIdentity SelectedDupe => _selectedDupe;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			// Pick initial dupe from the screen's selection, or first live minion
			var screen = (SkillsScreen)_screen;
			if (screen.CurrentlySelectedMinion != null)
				_selectedDupe = screen.CurrentlySelectedMinion;
			else if (Components.LiveMinionIdentities.Count > 0)
				_selectedDupe = Components.LiveMinionIdentities.Items[0];

			ActiveTabIndex = (int)TabId.Duplicants;
			_dupeTab.OnTabActivated(announce: false);
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		internal void SelectDupeAndJumpToSkills(IAssignableIdentity dupe) {
			SetSelectedDupe(dupe);
			JumpToSkillsTab();
		}

		internal void JumpToSkillsTab() {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Skills;
			PlaySound("HUD_Mouseover");
			ActivateCurrentTab(announce: true);
		}

		internal void JumpToTreeTab(Skill skill) {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Tree;
			PlaySound("HUD_Mouseover");
			_treeTab.OnTabActivatedAt(skill);
		}

		internal void SetSelectedDupe(IAssignableIdentity dupe) {
			_selectedDupe = dupe;
			// Sync with the game screen
			((SkillsScreen)_screen).CurrentlySelectedMinion = dupe;
		}
	}
}
