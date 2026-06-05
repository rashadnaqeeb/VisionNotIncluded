using System.Collections.Generic;

using Database;

using OniAccess.Input;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Schedule {
	/// <summary>
	/// Schedules tab: 2D grid of timetable rows x hour blocks.
	/// Handles navigation, painting, reordering, and an inline options submenu.
	/// Does not extend BaseMenuHandler — the 2D cursor and custom key routing
	/// conflict with 1D list navigation.
	/// </summary>
	internal class SchedulesTab: IScreenTab {
		private readonly ScheduleScreenHandler _parent;

		// 2D cursor
		private int _row;
		private int _col;
		private int _lastSpokenScheduleIndex = -1;

		// Brush
		private string _brushGroupId;
		private int _paintCounter;

		// Inline options submenu
		private enum OptionId { Rename, Alarm, Duplicate, DeleteSchedule, AddRow, DeleteRow }
		private bool _inOptions;
		private int _optionIndex;
		private List<(OptionId id, string label)> _currentOptions = new List<(OptionId, string)>();

		// Rename
		private readonly TextEditHelper _renameHelper = new TextEditHelper();

		public string TabName => (string)STRINGS.ONIACCESS.SCHEDULE.SCHEDULES_TAB;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Left/Right", STRINGS.ONIACCESS.SCHEDULE.HELP_NAVIGATE_BLOCKS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.SCHEDULE.HELP_JUMP_BLOCK),
			new HelpEntry("1/2/3/4", STRINGS.ONIACCESS.SCHEDULE.HELP_SELECT_BRUSH),
			new HelpEntry("Space", STRINGS.ONIACCESS.SCHEDULE.HELP_PAINT),
			new HelpEntry("Shift+Left/Right", STRINGS.ONIACCESS.SCHEDULE.HELP_PAINT_MOVE),
			new HelpEntry("Shift+Home/End", STRINGS.ONIACCESS.SCHEDULE.HELP_PAINT_RANGE),
			new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.SCHEDULE.HELP_REORDER_SCHEDULE),
			new HelpEntry("Ctrl+Left/Right", STRINGS.ONIACCESS.SCHEDULE.HELP_ROTATE),
			new HelpEntry("Enter", STRINGS.ONIACCESS.SCHEDULE.HELP_OPTIONS),
		}.AsReadOnly();

		public IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		internal SchedulesTab(ScheduleScreenHandler parent) {
			_parent = parent;
		}

		// ========================================
		// ROW MODEL
		// ========================================

		private struct GridRow {
			public bool IsAddButton;
			public global::Schedule Schedule;
			public int TimetableIndex;
			public int ScheduleIndex;
		}

		private int TotalRows {
			get {
				int count = 0;
				foreach (var s in ScheduleManager.Instance.GetSchedules())
					count += ScheduleHelper.GetTimetableCount(s);
				return count + 1;
			}
		}

		private GridRow GetRow(int flatRow) {
			var schedules = ScheduleManager.Instance.GetSchedules();
			int accumulated = 0;
			for (int si = 0; si < schedules.Count; si++) {
				int ttCount = ScheduleHelper.GetTimetableCount(schedules[si]);
				if (flatRow < accumulated + ttCount) {
					return new GridRow {
						IsAddButton = false,
						Schedule = schedules[si],
						TimetableIndex = flatRow - accumulated,
						ScheduleIndex = si,
					};
				}
				accumulated += ttCount;
			}
			return new GridRow { IsAddButton = true, ScheduleIndex = schedules.Count };
		}

		private int FindScheduleStartRow(int scheduleIndex) {
			var schedules = ScheduleManager.Instance.GetSchedules();
			int row = 0;
			for (int i = 0; i < scheduleIndex && i < schedules.Count; i++)
				row += ScheduleHelper.GetTimetableCount(schedules[i]);
			return row;
		}

		// ========================================
		// TAB LIFECYCLE
		// ========================================

		public void OnTabActivated(bool announce) {
			_inOptions = false;
			_lastSpokenScheduleIndex = -1;

			// Restore brush from game's paint selection, default to Work
			var screen = _parent.ScheduleScreen;
			string selectedPaint = screen != null ? screen.SelectedPaint : null;
			if (selectedPaint != null && System.Array.IndexOf(ScheduleHelper.BrushGroupIds, selectedPaint) >= 0)
				_brushGroupId = selectedPaint;
			else
				_brushGroupId = ScheduleHelper.BrushGroupIds[0];

			// Start at current game hour on first schedule
			_row = 0;
			try {
				_col = ScheduleManager.GetCurrentHour();
			} catch (System.Exception ex) {
				Util.Log.Warn($"SchedulesTab.OnTabActivated: GetCurrentHour failed: {ex.Message}");
				_col = 0;
			}

			ClampCursor();

			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);

			// Speak opening state
			var gr = GetRow(_row);
			if (!gr.IsAddButton) {
				string opening = WidgetSpeech.ComposeLabel(BuildFullCellAnnouncement(gr, forceScheduleName: true));
				string brushName = ScheduleHelper.GetGroupName(_brushGroupId);
				opening += ". " + string.Format(STRINGS.ONIACCESS.SCHEDULE.BRUSH_ACTIVE, brushName);
				SpeechPipeline.SpeakQueued(opening);
				_lastSpokenScheduleIndex = gr.ScheduleIndex;
			}
		}

		public void OnTabDeactivated() {
			if (_renameHelper.IsEditing)
				_renameHelper.Cancel();
			_inOptions = false;
		}

		// ========================================
		// INPUT ROUTING
		// ========================================

		public bool HandleInput() {
			if (_renameHelper.HandleTick())
				return false;

			if (_inOptions)
				return HandleOptionsInput();

			return HandleGridInput();
		}

		public bool HandleKeyDown(KButtonEvent e) {
			if (_renameHelper.HandleKeyDown(e))
				return true;
			// Intercept Escape during options
			if (_inOptions && e.TryConsume(Action.Escape)) {
				ExitOptions();
				return true;
			}
			return false;
		}

		// ========================================
		// OPTIONS INPUT
		// ========================================

		private bool HandleOptionsInput() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				NavigateOption(1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				NavigateOption(-1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				ActivateOption();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				ExitOptions();
				return true;
			}
			// Consume other keys while in options
			return true;
		}

		private void ExitOptions() {
			_inOptions = false;
			ScheduleHelper.PlayHoverSound();
			SpeakCurrentCell();
		}

		private List<(OptionId id, string label)> BuildOptionsList(GridRow gr) {
			var options = new List<(OptionId, string)>();
			if (gr.IsAddButton) return options;

			options.Add((OptionId.Rename, (string)STRINGS.ONIACCESS.SCHEDULE.OPTIONS_RENAME));

			string alarmLabel = gr.Schedule.alarmActivated
				? (string)STRINGS.UI.SCHEDULESCREEN.ALARM_TITLE_ENABLED
				: (string)STRINGS.UI.SCHEDULESCREEN.ALARM_TITLE_DISABLED;
			options.Add((OptionId.Alarm, alarmLabel));

			options.Add((OptionId.Duplicate, (string)STRINGS.ONIACCESS.SCHEDULE.OPTIONS_DUPLICATE));
			options.Add((OptionId.DeleteSchedule, (string)STRINGS.ONIACCESS.SCHEDULE.OPTIONS_DELETE_SCHEDULE));
			options.Add((OptionId.AddRow, (string)STRINGS.ONIACCESS.SCHEDULE.OPTIONS_ADD_ROW));

			if (ScheduleHelper.GetTimetableCount(gr.Schedule) > 1)
				options.Add((OptionId.DeleteRow, (string)STRINGS.ONIACCESS.SCHEDULE.OPTIONS_DELETE_ROW));

			return options;
		}

		private void NavigateOption(int direction) {
			if (_currentOptions.Count == 0) return;

			int next = _optionIndex + direction;
			if (next < 0) {
				_optionIndex = _currentOptions.Count - 1;
				ScheduleHelper.PlayWrapSound();
			} else if (next >= _currentOptions.Count) {
				_optionIndex = 0;
				ScheduleHelper.PlayWrapSound();
			} else {
				_optionIndex = next;
				ScheduleHelper.PlayHoverSound();
			}
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(_currentOptions[_optionIndex].label));
		}

		private void ActivateOption() {
			var gr = GetRow(_row);
			if (gr.IsAddButton) return;
			if (_optionIndex >= _currentOptions.Count) return;

			switch (_currentOptions[_optionIndex].id) {
				case OptionId.Rename: BeginRename(gr); break;
				case OptionId.Alarm: ToggleAlarm(gr); break;
				case OptionId.Duplicate: DuplicateSchedule(gr); break;
				case OptionId.DeleteSchedule: DeleteSchedule(gr); break;
				case OptionId.AddRow: AddTimetableRow(gr); break;
				case OptionId.DeleteRow: DeleteTimetableRow(gr); break;
			}
		}

		// ========================================
		// OPTION ACTIONS
		// ========================================

		private void BeginRename(GridRow gr) {
			var screen = _parent.ScheduleScreen;
			if (screen == null) return;

			int si = gr.ScheduleIndex;
			_renameHelper.Begin(
				() => ScheduleHelper.GetEntryInputField(screen, si),
				initialText: gr.Schedule.name,
				onEnd: () => { _inOptions = false; SpeakCurrentCell(); });
		}

		private void ToggleAlarm(GridRow gr) {
			gr.Schedule.alarmActivated = !gr.Schedule.alarmActivated;
			string newState = gr.Schedule.alarmActivated
				? (string)STRINGS.UI.SCHEDULESCREEN.ALARM_TITLE_ENABLED
				: (string)STRINGS.UI.SCHEDULESCREEN.ALARM_TITLE_DISABLED;
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(newState);
		}

		private void DuplicateSchedule(GridRow gr) {
			var newSchedule = ScheduleManager.Instance.DuplicateSchedule(gr.Schedule);
			_inOptions = false;
			// Move cursor to the new schedule (appended at end)
			var schedules = ScheduleManager.Instance.GetSchedules();
			int newStartRow = 0;
			for (int i = 0; i < schedules.Count; i++) {
				if (schedules[i] == newSchedule) break;
				newStartRow += ScheduleHelper.GetTimetableCount(schedules[i]);
			}
			_row = newStartRow;
			_lastSpokenScheduleIndex = -1;
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(newSchedule.name);
			SpeakCurrentCell();
		}

		private void DeleteSchedule(GridRow gr) {
			var schedules = ScheduleManager.Instance.GetSchedules();
			if (schedules.Count <= 1) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.SCHEDULE.CANNOT_DELETE_LAST);
				return;
			}
			ScheduleManager.Instance.DeleteSchedule(gr.Schedule);
			_inOptions = false;
			_row = 0;
			_lastSpokenScheduleIndex = -1;
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.SCHEDULE.SCHEDULE_DELETED);
			SpeakCurrentCell();
		}

		private void AddTimetableRow(GridRow gr) {
			// Duplicate the current timetable row's blocks
			var blocks = gr.Schedule.GetBlocks();
			int srcStart = gr.TimetableIndex * 24;
			var newBlocks = new List<ScheduleBlock>();
			for (int i = 0; i < 24; i++)
				newBlocks.Add(new ScheduleBlock(blocks[srcStart + i].name, blocks[srcStart + i].GroupId));

			int insertIdx = gr.TimetableIndex + 1;
			gr.Schedule.InsertTimetable(insertIdx, newBlocks);
			_inOptions = false;
			// Move cursor to the new row
			_row = FindScheduleStartRow(gr.ScheduleIndex) + insertIdx;
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.SCHEDULE.TIMETABLE_ROW_ADDED);
			SpeakCurrentCell();
		}

		private void DeleteTimetableRow(GridRow gr) {
			if (ScheduleHelper.GetTimetableCount(gr.Schedule) <= 1) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.SCHEDULE.CANNOT_DELETE_LAST_ROW);
				return;
			}
			gr.Schedule.RemoveTimetable(gr.TimetableIndex);
			_inOptions = false;
			// Clamp row after removal
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.SCHEDULE.TIMETABLE_ROW_DELETED);
			SpeakCurrentCell();
		}

		// ========================================
		// GRID INPUT
		// ========================================

		private bool HandleGridInput() {
			bool ctrlHeld = InputUtil.CtrlHeld();
			bool shiftHeld = InputUtil.ShiftHeld();

			// Number keys: select brush
			if (TrySelectBrush()) return true;

			// Up/Down
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				if (ctrlHeld) ReorderSchedule(-1);
				else if (shiftHeld) ShiftTimetableRow(true);
				else NavigateRow(-1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				if (ctrlHeld) ReorderSchedule(1);
				else if (shiftHeld) ShiftTimetableRow(false);
				else NavigateRow(1);
				return true;
			}

			// Left/Right
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				if (ctrlHeld) RotateBlocks(true);
				else if (shiftHeld) PaintAndMove(-1);
				else NavigateCol(-1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				if (ctrlHeld) RotateBlocks(false);
				else if (shiftHeld) PaintAndMove(1);
				else NavigateCol(1);
				return true;
			}

			// Home/End
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Home)) {
				if (shiftHeld) PaintRange(0);
				else { _col = 0; ScheduleHelper.PlayHoverSound(); SpeakCurrentCell(includeRowContext: false); }
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.End)) {
				if (shiftHeld) PaintRange(23);
				else { _col = 23; ScheduleHelper.PlayHoverSound(); SpeakCurrentCell(includeRowContext: false); }
				return true;
			}

			// Space: paint current cell
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) {
				PaintCurrentCell();
				return true;
			}

			// Enter: open options or activate Add button
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				var gr = GetRow(_row);
				if (gr.IsAddButton) {
					AddNewSchedule();
				} else {
					_currentOptions = BuildOptionsList(gr);
					_inOptions = true;
					_optionIndex = 0;
					if (_currentOptions.Count > 0)
						SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(_currentOptions[0].label));
				}
				return true;
			}

			return false;
		}

		// ========================================
		// NAVIGATION
		// ========================================

		private void NavigateRow(int direction) {
			int total = TotalRows;
			int newRow = _row + direction;
			if (newRow < 0 || newRow >= total) return;

			_row = newRow;
			_paintCounter = 0;
			ScheduleHelper.PlayHoverSound();
			SpeakCurrentCell();
		}

		private void NavigateCol(int direction) {
			var gr = GetRow(_row);
			if (gr.IsAddButton) return;
			_paintCounter = 0;

			int newCol = _col + direction;
			if (newCol < 0) {
				_col = 23;
				ScheduleHelper.PlayWrapSound();
			} else if (newCol > 23) {
				_col = 0;
				ScheduleHelper.PlayWrapSound();
			} else {
				_col = newCol;
				ScheduleHelper.PlayHoverSound();
			}
			SpeakCurrentCell(includeRowContext: false);
		}

		// ========================================
		// BRUSH SELECTION
		// ========================================

		private bool TrySelectBrush() {
			int digit = InputUtil.GetDigitKeyDown();
			if (digit >= 1 && digit <= 4) {
				int i = digit - 1;
				_brushGroupId = ScheduleHelper.BrushGroupIds[i];
				string name = ScheduleHelper.GetGroupName(_brushGroupId);
				SpeechPipeline.SpeakInterrupt(name);

				// Sync the game's paint selection
				var screen = _parent.ScheduleScreen;
				if (screen != null) {
					screen.SelectedPaint = _brushGroupId;
					screen.RefreshAllPaintButtons();
				}
				return true;
			}
			return false;
		}

		// ========================================
		// PAINTING
		// ========================================

		private void PaintCurrentCell() {
			var gr = GetRow(_row);
			if (gr.IsAddButton || _brushGroupId == null) return;

			int blockIdx = gr.TimetableIndex * 24 + _col;
			var block = gr.Schedule.GetBlock(blockIdx);
			string groupName = ScheduleHelper.GetGroupName(_brushGroupId);

			if (block.GroupId == _brushGroupId) {
				ScheduleHelper.PlayPaintNoneSound();
				SpeechPipeline.SpeakInterrupt(string.Format(
					STRINGS.ONIACCESS.SCHEDULE.BLOCK_ALREADY, _col, groupName));
				_paintCounter = 0;
				return;
			}

			var group = Db.Get().ScheduleGroups.Get(_brushGroupId);
			gr.Schedule.SetBlockGroup(blockIdx, group);
			ScheduleHelper.PlayPaintSound(_paintCounter);
			_paintCounter = 0;
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.SCHEDULE.BLOCK_LABEL, groupName, _col));
		}

		private void PaintAndMove(int direction) {
			var gr = GetRow(_row);
			if (gr.IsAddButton || _brushGroupId == null) return;

			// Move first
			int newCol = _col + direction;
			if (newCol < 0)
				_col = 23;
			else if (newCol > 23)
				_col = 0;
			else
				_col = newCol;

			// Paint the new cell
			int blockIdx = gr.TimetableIndex * 24 + _col;
			var group = Db.Get().ScheduleGroups.Get(_brushGroupId);
			gr.Schedule.SetBlockGroup(blockIdx, group);
			ScheduleHelper.PlayPaintSound(_paintCounter);
			_paintCounter++;
			string groupName = ScheduleHelper.GetGroupName(_brushGroupId);
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.SCHEDULE.BLOCK_LABEL, groupName, _col));
		}

		private void PaintRange(int targetCol) {
			var gr = GetRow(_row);
			if (gr.IsAddButton || _brushGroupId == null) return;

			int startCol = System.Math.Min(_col, targetCol);
			int endCol = System.Math.Max(_col, targetCol);

			var group = Db.Get().ScheduleGroups.Get(_brushGroupId);
			for (int c = startCol; c <= endCol; c++) {
				int blockIdx = gr.TimetableIndex * 24 + c;
				gr.Schedule.SetBlockGroup(blockIdx, group);
			}

			string groupName = ScheduleHelper.GetGroupName(_brushGroupId);
			_col = targetCol;
			_paintCounter = 0;
			ScheduleHelper.PlayPaintSound(0);
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.SCHEDULE.PAINTED_RANGE, groupName, startCol, endCol));
		}

		// ========================================
		// REORDERING
		// ========================================

		private void ReorderSchedule(int direction) {
			var gr = GetRow(_row);
			if (gr.IsAddButton) return;

			var schedules = ScheduleManager.Instance.GetSchedules();
			int si = gr.ScheduleIndex;
			int newSi = si + direction;
			if (newSi < 0 || newSi >= schedules.Count) return;

			// Swap directly in the live list
			var schedule = schedules[si];
			schedules.RemoveAt(si);
			schedules.Insert(newSi, schedule);

			// ScheduleManager has no public reorder API. GetSchedules() returns the
			// live list so we mutate it directly, then raise onSchedulesChanged via
			// its backing field (field-like events compile to a backing delegate of
			// the same name). If this fails, the reorder succeeded but the game UI
			// will not refresh — state corruption the user cannot recover from.
			try {
				var del = HarmonyLib.Traverse.Create(ScheduleManager.Instance)
					.Field<System.Action<List<global::Schedule>>>("onSchedulesChanged").Value;
				del?.Invoke(schedules);
			} catch (System.Exception ex) {
				Util.Log.Error($"SchedulesTab.ReorderSchedule: onSchedulesChanged invoke failed: {ex.Message}");
			}

			// Update cursor to follow the schedule
			_row = FindScheduleStartRow(newSi) + gr.TimetableIndex;
			_lastSpokenScheduleIndex = -1;
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			string dirLabel = direction < 0
				? (string)STRINGS.ONIACCESS.SCHEDULE.MOVED_UP
				: (string)STRINGS.ONIACCESS.SCHEDULE.MOVED_DOWN;
			SpeechPipeline.SpeakInterrupt($"{schedule.name}, {dirLabel}");
		}

		private void ShiftTimetableRow(bool up) {
			var gr = GetRow(_row);
			if (gr.IsAddButton) return;

			int ttCount = ScheduleHelper.GetTimetableCount(gr.Schedule);
			if (ttCount <= 1) return;

			bool moved = gr.Schedule.ShiftTimetable(up, gr.TimetableIndex);
			if (!moved) return;

			// Update cursor to follow the row
			_row += up ? -1 : 1;
			ClampCursor();
			var newGr = GetRow(_row);
			ScheduleHelper.PlayShiftSound(up);
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.SCHEDULE.ROW_LABEL, newGr.TimetableIndex + 1));
		}

		private void RotateBlocks(bool directionLeft) {
			var gr = GetRow(_row);
			if (gr.IsAddButton) return;

			gr.Schedule.RotateBlocks(directionLeft, gr.TimetableIndex);
			// Announce the block now under the cursor
			SpeakCurrentCell(includeRowContext: false);
		}

		// ========================================
		// ADD NEW SCHEDULE
		// ========================================

		private void AddNewSchedule() {
			ScheduleManager.Instance.AddDefaultSchedule(alarmOn: false, useDefaultName: false);
			var schedules = ScheduleManager.Instance.GetSchedules();
			var newSchedule = schedules[schedules.Count - 1];

			// Move cursor to the new schedule (last one before the Add button)
			_row = TotalRows - 2;
			_lastSpokenScheduleIndex = -1;
			ClampCursor();
			ScheduleHelper.PlayClickSound();
			SpeechPipeline.SpeakInterrupt(newSchedule.name);
			SpeakCurrentCell();
		}

		// ========================================
		// SPEECH
		// ========================================

		private void SpeakCurrentCell(bool includeRowContext = true) {
			ClampCursor();
			var gr = GetRow(_row);

			if (gr.IsAddButton) {
				_lastSpokenScheduleIndex = -1;
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(
					(string)STRINGS.ONIACCESS.SCHEDULE.ADD_SCHEDULE));
				return;
			}

			string announcement = WidgetSpeech.ComposeLabel(
				BuildFullCellAnnouncement(gr, forceScheduleName: false, includeRowContext));
			SpeechPipeline.SpeakInterrupt(announcement);
			_lastSpokenScheduleIndex = gr.ScheduleIndex;
		}

		private string BuildFullCellAnnouncement(GridRow gr, bool forceScheduleName, bool includeRowContext = true) {
			var parts = new List<string>();

			// Schedule name when crossing boundary
			bool scheduleChanged = forceScheduleName || gr.ScheduleIndex != _lastSpokenScheduleIndex;
			if (scheduleChanged) {
				int ttCount = ScheduleHelper.GetTimetableCount(gr.Schedule);
				if (ttCount > 1)
					parts.Add(string.Format(
						STRINGS.ONIACCESS.SCHEDULE.SCHEDULE_ROW,
						gr.Schedule.name, gr.TimetableIndex + 1));
				else
					parts.Add(gr.Schedule.name);
			} else if (includeRowContext) {
				// Same schedule, but multi-row: show row number
				int ttCount = ScheduleHelper.GetTimetableCount(gr.Schedule);
				if (ttCount > 1)
					parts.Add(string.Format(
						STRINGS.ONIACCESS.SCHEDULE.ROW_LABEL, gr.TimetableIndex + 1));
			}

			// Cell
			parts.Add(ScheduleHelper.BuildCellLabel(gr.Schedule, gr.TimetableIndex, _col));

			// Warnings
			string warnings = ScheduleHelper.BuildWarnings(gr.Schedule);
			if (warnings != null)
				parts.Add(warnings);

			return string.Join(". ", parts);
		}

		// ========================================
		// UTILITIES
		// ========================================

		private void ClampCursor() {
			int total = TotalRows;
			if (_row >= total) _row = total - 1;
			if (_row < 0) _row = 0;
			if (_col > 23) _col = 23;
			if (_col < 0) _col = 0;
		}
	}
}
