using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Schedule {
	/// <summary>
	/// Duplicants tab: flat list of all duplicants with type-ahead search.
	/// Left/Right cycles schedule assignment.
	/// </summary>
	internal class DupesTab: BaseMenuHandler, IScreenTab {
		private readonly ScheduleScreenHandler _parent;

		internal DupesTab(ScheduleScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.SKILLS.DUPES_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry>(MenuHelpEntries) {
				new HelpEntry("Left/Right", STRINGS.ONIACCESS.SCHEDULE.HELP_CHANGE_SCHEDULE),
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
			var dupes = GetDupeList();
			if (dupes.Count > 0)
				SpeechPipeline.SpeakQueued(
					WidgetSpeech.ComposeLabel(ScheduleHelper.BuildDupeLabel(dupes[0])));
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount => GetDupeList().Count;

		public override string GetItemLabel(int index) {
			var dupes = GetDupeList();
			if (index < 0 || index >= dupes.Count) return null;
			return dupes[index].GetProperName();
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			var dupes = GetDupeList();
			if (CurrentIndex < 0 || CurrentIndex >= dupes.Count) return;
			string label = ScheduleHelper.BuildDupeLabel(dupes[CurrentIndex]);
			if (!string.IsNullOrEmpty(parentContext))
				label = parentContext + ", " + label;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(label));
		}

		protected override void AdjustCurrentItem(int direction, int stepLevel) {
			var dupes = GetDupeList();
			if (CurrentIndex < 0 || CurrentIndex >= dupes.Count) return;

			var mi = dupes[CurrentIndex];
			var schedulable = mi.GetComponent<Schedulable>();
			var schedules = ScheduleManager.Instance.GetSchedules();
			if (schedules.Count <= 1) return;

			var currentSchedule = ScheduleManager.Instance.GetSchedule(schedulable);
			int currentIdx = schedules.IndexOf(currentSchedule);
			if (currentIdx < 0) return;

			int newIdx = (currentIdx + direction + schedules.Count) % schedules.Count;
			var targetSchedule = schedules[newIdx];

			currentSchedule.Unassign(schedulable);
			targetSchedule.Assign(schedulable);

			ScheduleHelper.PlayHoverSound();
			SpeechPipeline.SpeakInterrupt(targetSchedule.name);
		}

		// ========================================
		// Data
		// ========================================

		private List<MinionIdentity> GetDupeList() {
			var list = new List<MinionIdentity>();
			foreach (var mi in Components.LiveMinionIdentities.Items) {
				if (mi != null)
					list.Add(mi);
			}
			return list;
		}
	}
}
