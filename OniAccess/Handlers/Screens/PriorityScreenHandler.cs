using System.Collections.Generic;
using System.Linq;

using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// 2D grid handler for the JobsTableScreen (duplicant priority management).
	///
	/// Builds a virtual table from live game state on every navigation event.
	/// _choreGroups holds a filtered snapshot of Db ChoreGroup resources, which are
	/// immutable database objects — safe to cache for the screen's lifetime.
	/// </summary>
	public class PriorityScreenHandler: BaseTableHandler {
		List<ChoreGroup> _choreGroups;

		public override string DisplayName => STRINGS.ONIACCESS.PRIORITY_SCREEN.HANDLER_NAME;

		public PriorityScreenHandler(KScreen screen) : base(screen) { }

		// ========================================
		// HELP
		// ========================================

		static readonly List<HelpEntry> _helpEntries = new List<HelpEntry>(TableNavHelpEntries) {
			TableSortHelpEntry,
			new HelpEntry("0-5", STRINGS.ONIACCESS.PRIORITY_SCREEN.SET_PRIORITY),
			new HelpEntry("Shift+0-5", STRINGS.ONIACCESS.PRIORITY_SCREEN.SET_COLUMN),
			new HelpEntry("Ctrl+Left/Right", STRINGS.ONIACCESS.PRIORITY_SCREEN.ADJUST_ROW),
			new HelpEntry("Shift+Up/Down", STRINGS.ONIACCESS.PRIORITY_SCREEN.ADJUST_CELL),
			new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.PRIORITY_SCREEN.ADJUST_COLUMN),
			new HelpEntry("Ctrl+R", STRINGS.ONIACCESS.PRIORITY_SCREEN.RESET),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// TABLE SETUP
		// ========================================

		protected override void OnTableActivate() {
			_choreGroups = Db.Get().ChoreGroups.resources
				.Where(g => g.userPrioritizable)
				.ToList();
		}

		// ========================================
		// ROW LIST BUILDING
		// ========================================

		protected override void BuildRowList() {
			_rows.Clear();
			bool showDividers = DlcManager.FeatureClusterSpaceEnabled();

			_rows.Add(new RowEntry { Kind = TableRowKind.Toolbar });
			_rows.Add(new RowEntry { Kind = TableRowKind.ColumnHeader });

			var worldIds = ClusterManager.Instance.GetWorldIDsSorted();
			foreach (int worldId in worldIds) {
				var world = ClusterManager.Instance.GetWorld(worldId);
				if (world == null || !world.IsDiscovered) continue;

				var minions = GetLiveMinionsForWorld(worldId);
				if (minions.Count == 0) continue;

				if (showDividers)
					_rows.Add(new RowEntry { Kind = TableRowKind.WorldDivider, WorldId = worldId });

				if (_sortColumn >= 0 && _sortColumn < _choreGroups.Count) {
					var group = _choreGroups[_sortColumn];
					minions.Sort((a, b) => {
						var ca = ((MinionIdentity)a).GetComponent<ChoreConsumer>();
						var cb = ((MinionIdentity)b).GetComponent<ChoreConsumer>();
						bool disA = ca.IsChoreGroupDisabled(group);
						bool disB = cb.IsChoreGroupDisabled(group);
						if (disA != disB) return disA ? 1 : -1;
						int cmp = ca.GetPersonalPriority(group).CompareTo(cb.GetPersonalPriority(group));
						if (!_sortAscending) cmp = -cmp;
						if (cmp != 0) return cmp;
						return a.GetProperName().CompareTo(b.GetProperName());
					});
				}

				foreach (var minion in minions) {
					_rows.Add(new RowEntry { Kind = TableRowKind.Minion, Identity = minion, WorldId = worldId });
				}
			}

			var stored = GetStoredMinions();
			if (stored.Count > 0) {
				if (showDividers)
					_rows.Add(new RowEntry { Kind = TableRowKind.WorldDivider, WorldId = StoredMinionWorldId });
				foreach (var smi in stored) {
					_rows.Add(new RowEntry { Kind = TableRowKind.StoredMinion, Identity = smi, WorldId = StoredMinionWorldId });
				}
			}

			_rows.Add(new RowEntry { Kind = TableRowKind.Default });
		}

		// ========================================
		// TABLE SHAPE
		// ========================================

		protected override int GetColumnCount(TableRowKind kind) {
			return kind == TableRowKind.Toolbar ? 2 : _choreGroups.Count;
		}

		protected override bool ColumnWraps(TableRowKind kind) => kind != TableRowKind.Toolbar;

		protected override string GetColumnName(int col) {
			if (_rows[_row].Kind == TableRowKind.Toolbar)
				return null;
			if (col >= 0 && col < _choreGroups.Count)
				return _choreGroups[col].Name;
			return null;
		}

		protected override string GetSearchableColumnName(int col) {
			if (col >= 0 && col < _choreGroups.Count)
				return _choreGroups[col].Name;
			return null;
		}

		protected override string GetRowLabel(RowEntry row) {
			switch (row.Kind) {
				case TableRowKind.Toolbar:
					return (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.TOOLBAR;
				case TableRowKind.ColumnHeader:
					return null;
				case TableRowKind.Minion:
				case TableRowKind.StoredMinion:
					return row.Identity.GetProperName();
				case TableRowKind.Default:
					return (string)STRINGS.UI.JOBSCREEN_DEFAULT;
				default:
					return null;
			}
		}

		protected override string GetCellValue(RowEntry row) {
			switch (row.Kind) {
				case TableRowKind.Toolbar:
					if (_col == 0) return STRINGS.UI.JOBSSCREEN.RESET_SETTINGS;
					if (_col == 1) return (Game.Instance.advancedPersonalPriorities
						? (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.PROXIMITY_ON
						: (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.PROXIMITY_OFF)
						+ ", " + STRINGS.UI.JOBSSCREEN.TOGGLE_ADVANCED_MODE_TOOLTIP;
					return "";

				case TableRowKind.ColumnHeader:
					if (_col >= 0 && _col < _choreGroups.Count) {
						var group = _choreGroups[_col];
						// ". " between the errands (and before the list) so the Alt+Up/Down
						// reviewer steps one task per line rather than reading the whole
						// category's errand list as a single run-on.
						var choreNames = string.Join(". ", group.choreTypes.Select(ct => ct.Name));
						return group.description + ". "
							+ string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.AFFECTED_ERRANDS, choreNames);
					}
					return "";

				case TableRowKind.Minion: {
						var manager = GetPriorityManager(row);
						var group = _choreGroups[_col];
						if (manager.IsChoreGroupDisabled(group)) {
							string traitName = GetDisablingTraitName(row.Identity, group);
							return string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.DISABLED_TRAIT, traitName);
						}
						int priority = manager.GetPersonalPriority(group);
						int skill = manager.GetAssociatedSkillLevel(group);
						return GetPriorityName(priority) + ", "
							+ string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.SKILL, skill);
					}

				case TableRowKind.StoredMinion: {
						var manager = GetPriorityManager(row);
						var group = _choreGroups[_col];
						if (manager.IsChoreGroupDisabled(group)) {
							string traitName = GetDisablingTraitName(row.Identity, group);
							return string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.DISABLED_TRAIT, traitName);
						}
						return GetPriorityName(manager.GetPersonalPriority(group));
					}

				case TableRowKind.Default: {
						var group = _choreGroups[_col];
						return GetPriorityName(Immigration.Instance.GetPersonalPriority(group));
					}

				default:
					return "";
			}
		}

		// ========================================
		// ENTER / TOOLBAR
		// ========================================

		protected override void OnEnterPressed(RowEntry row) {
			if (row.Kind == TableRowKind.Toolbar)
				ActivateToolbarItem();
		}

		void ActivateToolbarItem() {
			if (_col == 0)
				ActivateReset();
			else if (_col == 1) {
				PlaySound("HUD_Click_Open");
				Game.Instance.advancedPersonalPriorities = !Game.Instance.advancedPersonalPriorities;
				SpeechPipeline.SpeakInterrupt(Game.Instance.advancedPersonalPriorities
					? (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.PROXIMITY_ON
					: (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.PROXIMITY_OFF);
			}
		}

		void ActivateReset() {
			PlaySound("HUD_Click_Open");
			HarmonyLib.Traverse.Create(_screen).Method("OnResetSettingsClicked").GetValue();
			SpeechPipeline.SpeakInterrupt((string)STRINGS.UI.JOBSSCREEN.RESET_SETTINGS);
		}

		// ========================================
		// CTRL+ARROW OVERRIDES
		// ========================================

		protected override bool HandleModifiedUpDown(int direction) {
			AdjustColumn(direction);
			return true;
		}

		protected override bool HandleModifiedLeftRight(int direction) {
			AdjustRow(direction);
			return true;
		}

		// ========================================
		// PRIORITY MANAGER ACCESS
		// ========================================

		IPersonalPriorityManager GetPriorityManager(RowEntry row) {
			switch (row.Kind) {
				case TableRowKind.Minion:
					return ((MinionIdentity)row.Identity).GetComponent<ChoreConsumer>();
				case TableRowKind.StoredMinion:
					return (StoredMinionIdentity)row.Identity;
				case TableRowKind.Default:
					return Immigration.Instance;
				default:
					return null;
			}
		}

		static string GetPriorityName(int value) {
			switch (value) {
				case 0: return STRINGS.UI.JOBSSCREEN.PRIORITY.DISABLED;
				case 1: return STRINGS.UI.JOBSSCREEN.PRIORITY.VERYLOW;
				case 2: return STRINGS.UI.JOBSSCREEN.PRIORITY.LOW;
				case 3: return STRINGS.UI.JOBSSCREEN.PRIORITY.STANDARD;
				case 4: return STRINGS.UI.JOBSSCREEN.PRIORITY.HIGH;
				case 5: return STRINGS.UI.JOBSSCREEN.PRIORITY.VERYHIGH;
				default: return value.ToString();
			}
		}

		string GetDisablingTraitName(IAssignableIdentity identity, ChoreGroup group) {
			if (identity is MinionIdentity mi) {
				var traits = mi.GetComponent<Klei.AI.Traits>();
				if (traits != null && traits.IsChoreGroupDisabled(group, out Klei.AI.Trait disablingTrait))
					return disablingTrait.Name;
			}
			if (identity is StoredMinionIdentity smi) {
				if (smi.IsChoreGroupDisabled(group))
					return STRINGS.ONIACCESS.STATES.DISABLED;
			}
			return STRINGS.ONIACCESS.STATES.DISABLED;
		}

		// ========================================
		// EDITING
		// ========================================

		void SetCellPriority(int value) {
			if (_row < 0 || _row >= _rows.Count) return;
			var row = _rows[_row];

			if (row.Kind == TableRowKind.Toolbar || row.Kind == TableRowKind.ColumnHeader) return;

			if (row.Kind == TableRowKind.StoredMinion) {
				PlaySound("Negative");
				string msg = string.Format(
					STRINGS.UI.JOBSSCREEN.CANNOT_ADJUST_PRIORITY,
					row.Identity.GetProperName(),
					((StoredMinionIdentity)row.Identity).GetStorageReason());
				SpeechPipeline.SpeakInterrupt(TextFilter.FilterForSpeech(msg));
				return;
			}

			var manager = GetPriorityManager(row);
			var group = _choreGroups[_col];
			if (manager.IsChoreGroupDisabled(group)) {
				PlaySound("Negative");
				string traitName = GetDisablingTraitName(row.Identity, group);
				SpeechPipeline.SpeakInterrupt(
					string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.DISABLED_TRAIT, traitName));
				return;
			}

			PlaySound("HUD_Click");
			manager.SetPersonalPriority(group, value);
			SpeakCell();
		}

		void AdjustRow(int delta) {
			if (_row < 0 || _row >= _rows.Count) return;
			var row = _rows[_row];
			if (row.Kind == TableRowKind.Toolbar || row.Kind == TableRowKind.ColumnHeader
				|| row.Kind == TableRowKind.WorldDivider || row.Kind == TableRowKind.StoredMinion) return;

			var manager = GetPriorityManager(row);
			foreach (var group in _choreGroups) {
				if (manager.IsChoreGroupDisabled(group)) continue;
				int current = manager.GetPersonalPriority(group);
				int newVal = UnityEngine.Mathf.Clamp(current + delta, 0, 5);
				manager.SetPersonalPriority(group, newVal);
			}

			PlaySound(delta > 0 ? "HUD_Click" : "HUD_Click_Deselect");
			string announcement = delta > 0
				? (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.ROW_INCREASED
				: (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.ROW_DECREASED;
			_lastSpokenRow = -1;
			_lastSpokenCol = -1;
			SpeechPipeline.SpeakInterrupt(announcement);
		}

		void SetColumnPriority(int value) {
			if (_col < 0 || _col >= _choreGroups.Count) return;
			var group = _choreGroups[_col];

			foreach (var row in _rows) {
				if (row.Kind != TableRowKind.Minion && row.Kind != TableRowKind.Default) continue;
				var manager = GetPriorityManager(row);
				if (manager.IsChoreGroupDisabled(group)) continue;
				manager.SetPersonalPriority(group, value);
			}

			PlaySound("HUD_Click");
			_lastSpokenRow = -1;
			_lastSpokenCol = -1;
			SpeechPipeline.SpeakInterrupt(
				_choreGroups[_col].Name + ", " + string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.COLUMN_SET, GetPriorityName(value)));
		}

		void AdjustCell(int delta) {
			if (_row < 0 || _row >= _rows.Count) return;
			var row = _rows[_row];

			if (row.Kind == TableRowKind.Toolbar || row.Kind == TableRowKind.ColumnHeader
				|| row.Kind == TableRowKind.WorldDivider) return;

			if (row.Kind == TableRowKind.StoredMinion) {
				PlaySound("Negative");
				string msg = string.Format(
					STRINGS.UI.JOBSSCREEN.CANNOT_ADJUST_PRIORITY,
					row.Identity.GetProperName(),
					((StoredMinionIdentity)row.Identity).GetStorageReason());
				SpeechPipeline.SpeakInterrupt(TextFilter.FilterForSpeech(msg));
				return;
			}

			var manager = GetPriorityManager(row);
			var group = _choreGroups[_col];
			if (manager.IsChoreGroupDisabled(group)) {
				PlaySound("Negative");
				string traitName = GetDisablingTraitName(row.Identity, group);
				SpeechPipeline.SpeakInterrupt(
					string.Format(STRINGS.ONIACCESS.PRIORITY_SCREEN.DISABLED_TRAIT, traitName));
				return;
			}

			PlaySound(delta > 0 ? "HUD_Click" : "HUD_Click_Deselect");
			int current = manager.GetPersonalPriority(group);
			int newVal = UnityEngine.Mathf.Clamp(current + delta, 0, 5);
			manager.SetPersonalPriority(group, newVal);
			SpeakCell();
		}

		void AdjustColumn(int delta) {
			if (_col < 0 || _col >= _choreGroups.Count) return;
			var group = _choreGroups[_col];

			foreach (var row in _rows) {
				if (row.Kind != TableRowKind.Minion && row.Kind != TableRowKind.Default) continue;
				var manager = GetPriorityManager(row);
				if (manager.IsChoreGroupDisabled(group)) continue;
				int current = manager.GetPersonalPriority(group);
				int newVal = UnityEngine.Mathf.Clamp(current + delta, 0, 5);
				manager.SetPersonalPriority(group, newVal);
			}

			PlaySound(delta > 0 ? "HUD_Click" : "HUD_Click_Deselect");
			string announcement = delta > 0
				? (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.COLUMN_INCREASED
				: (string)STRINGS.ONIACCESS.PRIORITY_SCREEN.COLUMN_DECREASED;
			_lastSpokenRow = -1;
			_lastSpokenCol = -1;
			SpeechPipeline.SpeakInterrupt(announcement);
		}

		// ========================================
		// TICK
		// ========================================

		public override bool Tick() {
			if (InputUtil.ShiftHeld()) {
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
					AdjustCell(1);
					return true;
				}
				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
					AdjustCell(-1);
					return true;
				}
			}

			if (base.Tick()) return true;

			bool ctrlHeld = InputUtil.CtrlHeld();
			bool shiftHeld = InputUtil.ShiftHeld();

			if (ctrlHeld && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R)) {
				ActivateReset();
				return true;
			}

			if (!ctrlHeld) {
				int digit = InputUtil.GetDigitKeyDown();
				if (digit >= 0 && digit <= 5) {
					if (shiftHeld)
						SetColumnPriority(digit);
					else
						SetCellPriority(digit);
					return true;
				}
			}

			return false;
		}
	}
}
