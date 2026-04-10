using System.Collections.Generic;

using OniAccess.Handlers.Screens.Schedule;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the ScheduleScreen. Manages two tabs:
	/// Schedules (2D timetable grid with painting and options)
	/// and Duplicants (flat dupe list with schedule reassignment).
	///
	/// Tab cycling via Tab/Shift+Tab. Each tab is a composed object.
	///
	/// Lifecycle: OnShow-patch on ScheduleScreen.OnShow(bool).
	/// </summary>
	public class ScheduleScreenHandler: TabbedScreenHandler {
		private enum TabId { Schedules, Duplicants }

		private readonly SchedulesTab _schedulesTab;
		private readonly DupesTab _dupesTab;

		public ScheduleScreenHandler(KScreen screen) : base(screen) {
			_schedulesTab = new SchedulesTab(this);
			_dupesTab = new DupesTab(this);
			SetTabs(_schedulesTab, _dupesTab);
		}

		public override string DisplayName => STRINGS.ONIACCESS.SCHEDULE.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			ActiveTabIndex = (int)TabId.Schedules;
			_schedulesTab.OnTabActivated(announce: false);
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		internal ScheduleScreen ScheduleScreen => _screen as ScheduleScreen;
	}
}
