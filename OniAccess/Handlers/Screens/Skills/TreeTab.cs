using System.Collections.Generic;

using Database;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Skills {
	/// <summary>
	/// Tab 3: navigable DAG of skills using NavigableGraph.
	/// Up moves to the first prerequisite. Down moves to the first dependent.
	/// Left/Right cycles among siblings from the last Up/Down move.
	/// Enter learns the current skill.
	/// </summary>
	internal class TreeTab: IScreenTab {
		private readonly SkillsScreenHandler _parent;
		private NavigableGraph<Skill> _graph;
		private Tag _lastModel;

		internal TreeTab(SkillsScreenHandler parent) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.RESEARCH.TREE_TAB;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.TREE_UP_DOWN),
			new HelpEntry("Left/Right", STRINGS.ONIACCESS.HELP.TREE_LEFT_RIGHT),
			new HelpEntry("Enter", STRINGS.ONIACCESS.SKILLS.LEARN_HELP),
		}.AsReadOnly();

		public IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			RebuildGraph();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			var model = SkillsHelper.GetDupeModel(_parent.SelectedDupe);
			var roots = SkillsHelper.GetRootSkills(model);
			if (roots.Count > 0) {
				_graph.MoveToWithSiblings(roots[0], roots);
				SpeechPipeline.SpeakQueued(
					SkillsHelper.BuildSkillLabel(roots[0], _parent.SelectedDupe));
			}
		}

		internal void OnTabActivatedAt(Skill skill) {
			RebuildGraph();
			SpeechPipeline.SpeakInterrupt(TabName);
			// Establish sibling context based on the skill's position
			var parents = SkillsHelper.GetParents(skill);
			if (parents.Count > 0) {
				var siblings = SkillsHelper.GetChildren(parents[0]);
				_graph.MoveToWithSiblings(skill, siblings);
			} else {
				var model = SkillsHelper.GetDupeModel(_parent.SelectedDupe);
				var roots = SkillsHelper.GetRootSkills(model);
				_graph.MoveToWithSiblings(skill, roots);
			}
			SpeechPipeline.SpeakQueued(
				SkillsHelper.BuildSkillLabel(skill, _parent.SelectedDupe));
		}

		public void OnTabDeactivated() { }

		public bool HandleInput() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				EnsureGraphCurrent();
				var node = _graph.NavigateDown();
				if (node != null) {
					BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(
						SkillsHelper.BuildSkillLabel(node, _parent.SelectedDupe));
				} else {
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.SKILLS.DEAD_END);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				EnsureGraphCurrent();
				var node = _graph.NavigateUp();
				if (node != null) {
					BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(
						SkillsHelper.BuildSkillLabel(node, _parent.SelectedDupe));
				} else {
					SpeechPipeline.SpeakInterrupt(
						STRINGS.ONIACCESS.RESEARCH.ROOT_NODE);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				EnsureGraphCurrent();
				var node = _graph.CycleSibling(1, out bool wrapped);
				if (node != null) {
					if (wrapped) BaseScreenHandler.PlaySound("HUD_Click");
					else BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(
						SkillsHelper.BuildSkillLabel(node, _parent.SelectedDupe));
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				EnsureGraphCurrent();
				var node = _graph.CycleSibling(-1, out bool wrapped);
				if (node != null) {
					if (wrapped) BaseScreenHandler.PlaySound("HUD_Click");
					else BaseScreenHandler.PlaySound("HUD_Mouseover");
					SpeechPipeline.SpeakInterrupt(
						SkillsHelper.BuildSkillLabel(node, _parent.SelectedDupe));
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				var skill = _graph.Current;
				if (skill != null)
					SkillsHelper.TryLearnSkill(
						skill, _parent.SelectedDupe, _parent.Screen);
				return true;
			}

			return false;
		}

		public bool HandleKeyDown(KButtonEvent e) => false;

		// ========================================
		// Graph management
		// ========================================

		private void RebuildGraph() {
			var model = SkillsHelper.GetDupeModel(_parent.SelectedDupe);
			_lastModel = model;
			_graph = new NavigableGraph<Skill>(
				getParents: skill => SkillsHelper.GetParents(skill),
				getChildren: skill => SkillsHelper.GetChildren(skill),
				getRoots: () => SkillsHelper.GetRootSkills(model));
		}

		private void EnsureGraphCurrent() {
			var model = SkillsHelper.GetDupeModel(_parent.SelectedDupe);
			if (_graph == null || model != _lastModel) {
				RebuildGraph();
				var roots = SkillsHelper.GetRootSkills(model);
				if (roots.Count > 0)
					_graph.MoveToWithSiblings(roots[0], roots);
			}
		}

	}
}
