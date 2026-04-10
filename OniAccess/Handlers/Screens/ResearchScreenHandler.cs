using System.Collections.Generic;

using OniAccess.Handlers.Screens.Research;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the ResearchScreen. Manages three tabs:
	/// Browse (categorized tech list), Queue (current research queue),
	/// and Tree (DAG graph navigation).
	///
	/// Tab cycling via Tab/Shift+Tab. Each tab is a composed object
	/// extending the appropriate base class for its navigation pattern.
	///
	/// Lifecycle: Show-patch on ResearchScreen.OnShow(bool).
	/// </summary>
	public class ResearchScreenHandler: TabbedScreenHandler {
		private enum TabId { Browse, Queue, Tree }

		private readonly BrowseTab _browseTab;
		private readonly QueueTab _queueTab;
		private readonly TreeTab _treeTab;

		public ResearchScreenHandler(KScreen screen) : base(screen) {
			_browseTab = new BrowseTab(this);
			_queueTab = new QueueTab(this);
			_treeTab = new TreeTab(this);
			SetTabs(_browseTab, _queueTab, _treeTab);
		}

		public override string DisplayName => STRINGS.ONIACCESS.RESEARCH.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			ActiveTabIndex = (int)TabId.Browse;
			_browseTab.OnTabActivated(announce: false);

			string points = ResearchHelper.BuildPointInventoryString();
			if (points != null)
				SpeechPipeline.SpeakQueued(points);
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		/// <summary>
		/// Jump to the Tree tab focused on a specific tech.
		/// Called by Browse and Queue tabs when the player presses Space.
		/// </summary>
		internal void JumpToTreeTab(Tech tech) {
			DeactivateCurrentTab();
			ActiveTabIndex = (int)TabId.Tree;
			PlaySound("HUD_Mouseover");
			_treeTab.OnTabActivatedAt(tech);
		}
	}
}
