using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Research {
	/// <summary>
	/// Tree tab: navigable DAG of technologies using NavigableGraph.
	/// Up moves to the first prerequisite. Down moves to the first dependent.
	/// Left/Right cycles among siblings from the last Up/Down move.
	/// Enter queues the current tech for research.
	/// </summary>
	internal class TreeTab: IScreenTab {
		private readonly ResearchScreenHandler _parent;
		private readonly NavigableGraph<Tech> _graph;

		internal TreeTab(ResearchScreenHandler parent) {
			_parent = parent;
			_graph = new NavigableGraph<Tech>(
				getParents: tech => tech.requiredTech,
				getChildren: tech => (IReadOnlyList<Tech>)tech.unlockedTech,
				getRoots: ResearchHelper.GetRootTechs);
		}

		public string TabName => (string)STRINGS.ONIACCESS.RESEARCH.TREE_TAB;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.TREE_UP_DOWN),
			new HelpEntry("Left/Right", STRINGS.ONIACCESS.HELP.TREE_LEFT_RIGHT),
			new HelpEntry("Enter", STRINGS.ONIACCESS.RESEARCH.QUEUE_CANCEL_HELP),
		}.AsReadOnly();

		public IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			// Default to first root node with root sibling context
			var roots = ResearchHelper.GetRootTechs();
			if (roots.Count > 0) {
				_graph.MoveToWithSiblings(roots[0], roots);
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(roots[0])));
			}
		}

		/// <summary>
		/// Enter the tree focused on a specific tech (from Space in Browse/Queue).
		/// No sibling context until the first Up or Down.
		/// </summary>
		internal void OnTabActivatedAt(Tech tech) {
			SpeechPipeline.SpeakInterrupt(TabName);
			_graph.MoveTo(tech);
			SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(tech)));
		}

		public void OnTabDeactivated() { }

		public bool HandleInput() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				var node = _graph.NavigateDown();
				if (node != null) {
					BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(node)));
				} else {
					SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.RESEARCH.DEAD_END);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				var node = _graph.NavigateUp();
				if (node != null) {
					BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(node)));
				} else {
					SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.RESEARCH.ROOT_NODE);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				var node = _graph.CycleSibling(1, out bool wrapped);
				if (node != null) {
					if (wrapped) BaseScreenHandler.PlaySound("HUD_Click");
					else BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(node)));
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				var node = _graph.CycleSibling(-1, out bool wrapped);
				if (node != null) {
					if (wrapped) BaseScreenHandler.PlaySound("HUD_Click");
					else BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(ResearchHelper.BuildTechLabel(node)));
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				var tech = _graph.Current;
				if (tech != null && !tech.IsComplete()) {
					ResearchHelper.PlayClickSound();
					global::Research.Instance.SetActiveResearch(tech, clearQueue: true);
					SpeechPipeline.SpeakInterrupt(
						string.Format(STRINGS.ONIACCESS.RESEARCH.QUEUED, tech.Name));
				} else if (tech != null) {
					ResearchHelper.PlayRejectSound();
					SpeechPipeline.SpeakInterrupt(
						tech.Name + ", " + STRINGS.ONIACCESS.RESEARCH.COMPLETED);
				}
				return true;
			}

			return false;
		}

		public bool HandleKeyDown(KButtonEvent e) => false;

	}
}
