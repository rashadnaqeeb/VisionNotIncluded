using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Research {
	/// <summary>
	/// Browse tab: two-level NestedMenuHandler.
	/// Level 0 = buckets (Available, Locked, Completed).
	/// Level 1 = techs within the selected bucket.
	/// Type-ahead search across all techs at level 1.
	/// Enter queues research. Space jumps to Tree tab.
	/// </summary>
	internal class BrowseTab: NestedMenuHandler, IScreenTab {
		private readonly ResearchScreenHandler _parent;

		internal BrowseTab(ResearchScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.RESEARCH.BROWSE_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => NestedNavHelpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			ResetState();
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
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space) && Level == 1) {
				var techs = ResearchHelper.GetTechsInBucket(GetIndex(0));
				int idx = GetIndex(1);
				if (idx >= 0 && idx < techs.Count) {
					_parent.JumpToTreeTab(techs[idx]);
					return true;
				}
			}
			// base.Tick() handles Up/Down/Home/End/Enter/Left/Right/Search.
			// Tab was already consumed by the parent so GetKeyDown(Tab) is false.
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// NestedMenuHandler abstracts
		// ========================================

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 1;
		protected override int StartLevel => 1;

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return 3;
			return ResearchHelper.GetTechsInBucket(indices[0]).Count;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0)
				return ResearchHelper.GetBucketName(indices[0]);
			var techs = ResearchHelper.GetTechsInBucket(indices[0]);
			if (indices[1] < 0 || indices[1] >= techs.Count) return null;
			return ResearchHelper.BuildTechLabel(techs[indices[1]]);
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level >= 1)
				return ResearchHelper.GetBucketName(indices[0]);
			return null;
		}

		protected override void ActivateLeafItem(int[] indices) {
			var techs = ResearchHelper.GetTechsInBucket(indices[0]);
			if (indices[1] < 0 || indices[1] >= techs.Count) return;
			var tech = techs[indices[1]];

			if (tech.IsComplete()) {
				ResearchHelper.PlayRejectSound();
				SpeechPipeline.SpeakInterrupt(
					tech.Name + ", " + STRINGS.ONIACCESS.RESEARCH.COMPLETED);
				return;
			}

			ResearchHelper.PlayClickSound();
			global::Research.Instance.SetActiveResearch(tech, clearQueue: true);
			SpeechPipeline.SpeakInterrupt(
				string.Format(STRINGS.ONIACCESS.RESEARCH.QUEUED, tech.Name));
		}

		// ========================================
		// Search across all techs (flat, spanning all buckets)
		// ========================================

		protected override int GetSearchItemCount(int[] indices) {
			return ResearchHelper.GetAllTechs().Count;
		}

		protected override string GetSearchItemLabel(int flatIndex) {
			var all = ResearchHelper.GetAllTechs();
			if (flatIndex < 0 || flatIndex >= all.Count) return null;
			return ResearchHelper.BuildSearchLabel(all[flatIndex]);
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			var all = ResearchHelper.GetAllTechs();
			if (flatIndex < 0 || flatIndex >= all.Count) return;
			var tech = all[flatIndex];

			int bucket = tech.IsComplete() ? 2 : tech.ArePrerequisitesComplete() ? 0 : 1;
			var techs = ResearchHelper.GetTechsInBucket(bucket);
			for (int i = 0; i < techs.Count; i++) {
				if (techs[i] == tech) {
					outIndices[0] = bucket;
					outIndices[1] = i;
					return;
				}
			}
			Util.Log.Warn($"BrowseTab.MapSearchIndex: tech '{tech.Name}' not found in bucket {bucket}");
		}

		// ========================================
		// Speech
		// ========================================

		public override void SpeakCurrentItem(string parentContext = null) {
			if (Level == 0) {
				base.SpeakCurrentItem(parentContext);
				return;
			}
			var techs = ResearchHelper.GetTechsInBucket(GetIndex(0));
			int idx = GetIndex(1);
			if (idx < 0 || idx >= techs.Count) return;
			string label = ResearchHelper.BuildTechLabel(techs[idx]);
			if (!string.IsNullOrEmpty(parentContext))
				label = parentContext + ", " + label;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(label));
		}
	}
}
