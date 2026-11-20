using System;
using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

using STRINGS;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the ReportScreen (daily reports). Standalone tree handler.
	///
	/// Level 0: section headers + Colony Summary action
	/// Level 1: visible stat categories within each section
	/// Level 2: per-entity context entries, or note breakdowns when no context entries exist
	///
	/// Tab/Shift+Tab cycles between report days. Type-ahead always searches the stat
	/// level. Data is read live from ReportManager on every call.
	///
	/// Lifecycle: OnShow-patch on ReportScreen.OnShow(bool).
	/// </summary>
	public class ReportScreenHandler: NavTreeHandler {
		private int _currentDay;

		// Section structure derived from ReportGroups. This is static metadata
		// (the dictionary is initialized once and never modified), so it is
		// built once in OnActivate rather than re-queried per frame.
		private List<SectionInfo> _sections;

		private readonly List<ReportManager.ReportType> _visibleTypesScratch =
			new List<ReportManager.ReportType>();
		private readonly List<ReportManager.ReportEntry.Note> _notesScratch =
			new List<ReportManager.ReportEntry.Note>();

		public ReportScreenHandler(KScreen screen) : base(screen) { }

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.REPORT.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		protected override int StartDepth => 1;

		// Type-ahead always searches the stat categories (level 1).
		protected override int SearchFixedDepth => 1;

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry>(DrillNavHelpEntries) {
			new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.REPORT.HELP_CYCLE),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			_currentDay = ReportManager.Instance.TodaysReport.day;
			BuildSections();
			base.OnActivate();
			// base.OnActivate interrupts with DisplayName; queue title and first item after it
			SpeechPipeline.SpeakQueued(TextFilter.FilterForSpeech(GetCycleTitle()));
			AnnounceCurrent(interrupt: false);
		}

		// ========================================
		// SECTION / DATA HELPERS
		// ========================================

		private struct SectionInfo {
			public string name;
			public List<ReportManager.ReportType> types;
		}

		private void BuildSections() {
			_sections = new List<SectionInfo>();
			SectionInfo current = default;
			current.types = null;

			foreach (var kvp in ReportManager.Instance.ReportGroups) {
				if (kvp.Value.isHeader) {
					if (current.types != null)
						_sections.Add(current);
					current = new SectionInfo {
						name = kvp.Value.stringKey,
						types = new List<ReportManager.ReportType>()
					};
				} else {
					current.types?.Add(kvp.Key);
				}
			}
			if (current.types != null)
				_sections.Add(current);
		}

		private ReportManager.DailyReport GetReport() {
			return ReportManager.Instance.FindReport(_currentDay)
				?? ReportManager.Instance.TodaysReport;
		}

		private List<ReportManager.ReportType> GetVisibleTypes(int sectionIndex) {
			_visibleTypesScratch.Clear();
			if (sectionIndex < 0 || sectionIndex >= _sections.Count)
				return _visibleTypesScratch;
			var report = GetReport();
			var section = _sections[sectionIndex];
			foreach (var type in section.types) {
				var entry = report.GetEntry(type);
				var group = ReportManager.Instance.ReportGroups[type];
				if (entry.accumulate != 0f || group.reportIfZero)
					_visibleTypesScratch.Add(type);
			}
			return _visibleTypesScratch;
		}

		/// <summary>
		/// Collect and sort notes from an entry into _notesScratch.
		/// </summary>
		private void CollectNotes(ReportManager.ReportEntry entry, ReportManager.ReportGroup group) {
			_notesScratch.Clear();
			entry.CollectNotes(_notesScratch);
			ReportManager.ReportEntry.Sort(_notesScratch, group.posNoteOrder);
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>(_sections.Count + 1);
			for (int s = 0; s < _sections.Count; s++) {
				int section = s;
				roots.Add(new MenuNode(
					() => _sections[section].name,
					children: () => BuildStats(section)));
			}
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.REPORT.COLONY_SUMMARY,
				activate: () => { ActivateColonySummary(); return true; }));
			return roots;
		}

		private IReadOnlyList<NavItem> BuildStats(int sectionIndex) {
			// Copy out of the shared scratch list before building nodes.
			var types = new List<ReportManager.ReportType>(GetVisibleTypes(sectionIndex));
			var list = new List<NavItem>(types.Count);
			foreach (var t in types) {
				var type = t;
				var group = ReportManager.Instance.ReportGroups[type];
				list.Add(new MenuNode(
					() => BuildStatLabel(GetReport().GetEntry(type), group),
					children: () => BuildLevel2(type),
					searchText: () => group.stringKey,
					contextLabel: () => group.stringKey));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildLevel2(ReportManager.ReportType reportType) {
			var entry = GetReport().GetEntry(reportType);
			var group = ReportManager.Instance.ReportGroups[reportType];
			var list = new List<NavItem>();
			if (entry.contextEntries.Count > 0) {
				for (int i = 0; i < entry.contextEntries.Count; i++) {
					var ctx = entry.contextEntries[i];
					list.Add(new MenuNode(() => BuildContextLabel(ctx, group)));
				}
			} else {
				CollectNotes(entry, group);
				foreach (var n in _notesScratch) {
					var note = n;
					list.Add(new MenuNode(() => BuildNoteLabel(note, group)));
				}
			}
			return list;
		}

		// ========================================
		// CYCLE NAVIGATION (Tab/Shift+Tab)
		// ========================================

		protected override void NavigateTabForward() {
			int maxDay = ReportManager.Instance.TodaysReport.day;
			if (_currentDay >= maxDay) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.REPORT.NO_LATER_CYCLE);
				return;
			}
			_currentDay++;
			OnCycleChanged();
		}

		protected override void NavigateTabBackward() {
			if (ReportManager.Instance.FindReport(_currentDay - 1) == null) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.REPORT.NO_EARLIER_CYCLE);
				return;
			}
			_currentDay--;
			OnCycleChanged();
		}

		private void OnCycleChanged() {
			Nav.ClampToTree();
			PlaySound("HUD_Mouseover");
			SpeechPipeline.SpeakInterrupt(TextFilter.FilterForSpeech(GetCycleTitle()));
			AnnounceCurrent(interrupt: false);
		}

		private string GetCycleTitle() {
			int todayDay = ReportManager.Instance.TodaysReport.day;
			if (_currentDay == todayDay)
				return string.Format(UI.ENDOFDAYREPORT.DAY_TITLE_TODAY, _currentDay);
			if (_currentDay == todayDay - 1)
				return string.Format(UI.ENDOFDAYREPORT.DAY_TITLE_YESTERDAY, _currentDay);
			return string.Format(UI.ENDOFDAYREPORT.DAY_TITLE, _currentDay);
		}

		// ========================================
		// COLONY SUMMARY
		// ========================================

		private void ActivateColonySummary() {
			try {
				var data = RetireColonyUtility.GetCurrentColonyRetiredColonyData();
				MainMenu.ActivateRetiredColoniesScreenFromData(
					PauseScreen.Instance.transform.parent.gameObject, data);
			} catch (Exception ex) {
				Util.Log.Error($"ReportScreenHandler.ActivateColonySummary failed: {ex.Message}");
				PlaySound("Negative");
			}
		}

		// ========================================
		// LABEL BUILDING
		// ========================================

		/// <summary>
		/// Format a value using the group's formatfn, falling back to ToString
		/// when formatfn is null (critters, chores, level-ups, etc.).
		/// </summary>
		private static string FormatValue(float value, ReportManager.ReportGroup group) {
			return group.formatfn != null ? group.formatfn(value) : value.ToString();
		}

		private string BuildStatLabel(
			ReportManager.ReportEntry entry,
			ReportManager.ReportGroup group) {

			var parts = new List<string>(4);
			parts.Add(group.stringKey);

			// Stats that only accumulate in one direction (critters, time, level-ups,
			// etc.) are totals/counts, not deltas. Show plain values without
			// net/added/removed framing. Only use delta framing when both positive
			// and negative values exist.
			bool isDelta = entry.Positive != 0f && entry.Negative != 0f;

			if (group.groupFormatfn != null) {
				// Mirror game's per-column denominator logic (ReportScreenEntryRow.SetLine).
				// When context entries exist, use their count. Otherwise count notes per sign.
				int ctxCount = entry.contextEntries.Count;
				float addedDenom, removedDenom, netDenom;
				if (ctxCount > 0) {
					addedDenom = removedDenom = netDenom = ctxCount;
				} else {
					int posCount = 0, negCount = 0;
					var notes = new List<ReportManager.ReportEntry.Note>();
					entry.CollectNotes(notes);
					foreach (var note in notes) {
						if (note.value > 0f) posCount++;
						else if (note.value < 0f) negCount++;
					}
					addedDenom = Math.Max(posCount, 1f);
					removedDenom = Math.Max(negCount, 1f);
					netDenom = Math.Max(posCount + negCount, 1f);
				}

				if (isDelta) {
					parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.NET,
						group.groupFormatfn(entry.Net, netDenom)));
					parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.ADDED,
						group.groupFormatfn(entry.Positive, addedDenom)));
					parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.REMOVED,
						group.groupFormatfn(0f - entry.Negative, removedDenom)));
				} else {
					parts.Add(group.groupFormatfn(entry.Net, netDenom));
				}
			} else if (isDelta) {
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.NET,
					FormatValue(entry.Net, group)));
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.ADDED,
					FormatValue(entry.Positive, group)));
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.REMOVED,
					FormatValue(0f - entry.Negative, group)));
			} else {
				parts.Add(FormatValue(entry.Net, group));
			}

			return string.Join(", ", parts);
		}

		private string BuildContextLabel(
			ReportManager.ReportEntry contextEntry,
			ReportManager.ReportGroup group) {

			var parts = new List<string>(8);
			parts.Add(contextEntry.context);

			parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.NET,
				FormatValue(contextEntry.Net, group)));
			if (contextEntry.Positive != 0f)
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.ADDED,
					FormatValue(contextEntry.Positive, group)));
			if (contextEntry.Negative != 0f)
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.REMOVED,
					FormatValue(0f - contextEntry.Negative, group)));

			CollectNotes(contextEntry, group);
			foreach (var note in _notesScratch) {
				parts.Add(string.Format(STRINGS.ONIACCESS.REPORT.NOTE,
					note.note, FormatValue(note.value, group)));
			}

			return string.Join(", ", parts);
		}

		private string BuildNoteLabel(
			ReportManager.ReportEntry.Note note,
			ReportManager.ReportGroup group) {
			return string.Format(STRINGS.ONIACCESS.REPORT.NOTE,
				note.note, FormatValue(note.value, group));
		}
	}
}
