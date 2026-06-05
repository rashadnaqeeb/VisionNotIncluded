using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Research {
	/// <summary>
	/// Queue tab: flat read-only list of the current research queue.
	/// First entry is the global point inventory (always present).
	/// Remaining entries are queued TechInstances in tier order.
	/// Enter cancels the selected tech. Space jumps to Tree tab.
	/// </summary>
	internal class QueueTab: BaseMenuHandler, IScreenTab {
		private readonly ResearchScreenHandler _parent;

		internal QueueTab(ResearchScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.RESEARCH.QUEUE_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry>(MenuHelpEntries) {
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Enter", STRINGS.ONIACCESS.RESEARCH.CANCEL_HELP),
			}.AsReadOnly();

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(GetCurrentLabel()));
			else
				SpeechPipeline.SpeakQueued(STRINGS.ONIACCESS.RESEARCH.QUEUE_EMPTY);
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) {
				var queue = global::Research.Instance.GetResearchQueue();
				// Index 0 is the point inventory row, queue starts at index 1
				int queueIndex = CurrentIndex - 1;
				if (queueIndex >= 0 && queueIndex < queue.Count) {
					_parent.JumpToTreeTab(queue[queueIndex].tech);
					return true;
				}
			}
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount {
			get {
				// +1 for the point inventory header row
				return 1 + global::Research.Instance.GetResearchQueue().Count;
			}
		}

		public override string GetItemLabel(int index) {
			if (index == 0)
				return BuildPointInventoryLabel();
			var queue = global::Research.Instance.GetResearchQueue();
			int queueIndex = index - 1;
			if (queueIndex < 0 || queueIndex >= queue.Count) return null;
			return queue[queueIndex].tech.Name;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			string label = GetCurrentLabel();
			if (label == null) return;
			if (!string.IsNullOrEmpty(parentContext))
				label = parentContext + ", " + label;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(label));
		}

		protected override void ActivateCurrentItem() {
			ResearchHelper.PlayClickSound();
			if (CurrentIndex == 0) {
				// Point inventory row — no action
				SpeakCurrentItem();
				return;
			}

			var queue = global::Research.Instance.GetResearchQueue();
			int queueIndex = CurrentIndex - 1;
			if (queueIndex < 0 || queueIndex >= queue.Count) return;

			var tech = queue[queueIndex].tech;
			bool hadOthers = queue.Count > 1;

			global::Research.Instance.CancelResearch(tech);

			string message = string.Format(STRINGS.ONIACCESS.RESEARCH.CANCELED, tech.Name);
			if (hadOthers)
				message += ", " + STRINGS.ONIACCESS.RESEARCH.QUEUE_CLEARED;

			// Adjust cursor if it now points past the end
			int maxIndex = ItemCount - 1;
			if (CurrentIndex > maxIndex)
				CurrentIndex = System.Math.Max(0, maxIndex);

			SpeechPipeline.SpeakInterrupt(message);
		}

		// ========================================
		// Helpers
		// ========================================

		string GetCurrentLabel() {
			if (CurrentIndex == 0)
				return BuildPointInventoryLabel();

			var queue = global::Research.Instance.GetResearchQueue();
			int queueIndex = CurrentIndex - 1;
			if (queueIndex < 0 || queueIndex >= queue.Count) {
				if (queue.Count == 0)
					return (string)STRINGS.ONIACCESS.RESEARCH.QUEUE_EMPTY;
				return null;
			}

			bool isActive = queueIndex == 0;
			return ResearchHelper.BuildQueuedTechLabel(queue[queueIndex], isActive);
		}

		static string BuildPointInventoryLabel() {
			string points = ResearchHelper.BuildPointInventoryString();
			return points ?? (string)STRINGS.ONIACCESS.RESEARCH.NO_BANKED_POINTS;
		}
	}
}
