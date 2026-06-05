using System.Collections.Generic;

using OniAccess.Input;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers {
	/// <summary>
	/// Abstract base for 2D table screen handlers. Provides shared infrastructure:
	/// row list with world dividers, 2D cursor navigation, cell speech with
	/// row/column deduplication, sort cycling, and sound effects.
	///
	/// Subclasses implement BuildRowList, GetColumnCount, GetColumnName,
	/// GetCellValue, and GetRowLabel to describe their specific table.
	/// </summary>
	public abstract class BaseTableHandler: BaseScreenHandler, ISearchable {
		protected enum TableRowKind {
			Toolbar,
			ColumnHeader,
			WorldDivider,
			Minion,
			StoredMinion,
			Default
		}

		protected struct RowEntry {
			public TableRowKind Kind;
			public IAssignableIdentity Identity;
			public int WorldId;
		}

		// 2D cursor
		protected int _row, _col;
		protected int _lastSpokenRow = -1;
		protected int _lastSpokenCol = -1;

		// Row list, rebuilt on every navigation event
		protected List<RowEntry> _rows = new List<RowEntry>();

		// Sort state
		protected int _sortColumn = -1;
		protected bool _sortAscending;

		// World filter
		private int _worldFilter = -1;
		private List<int> _filteredWorldIds = new List<int>();

		// Type-ahead search (columns)
		protected readonly TypeAheadSearch _search = new TypeAheadSearch();
		private int _searchSuppressFrame = -1;

		public override bool CapturesAllInput => true;

		protected BaseTableHandler(KScreen screen) : base(screen) { }

		// ========================================
		// ABSTRACT MEMBERS
		// ========================================

		protected abstract void BuildRowList();
		protected abstract int GetColumnCount(TableRowKind kind);
		protected abstract string GetColumnName(int col);
		protected abstract string GetCellValue(RowEntry row);
		protected abstract string GetRowLabel(RowEntry row);

		// ========================================
		// VIRTUAL MEMBERS
		// ========================================

		protected virtual bool ColumnWraps(TableRowKind kind) => kind != TableRowKind.Toolbar;
		protected virtual void OnEnterPressed(RowEntry row) { }
		protected virtual bool IsColumnSortable(int col) => true;

		protected const int StoredMinionWorldId = 255;

		protected virtual string GetWorldName(int worldId) {
			if (worldId == StoredMinionWorldId) return STRINGS.ONIACCESS.TABLE.STORED;
			var world = ClusterManager.Instance.GetWorld(worldId);
			return world != null ? world.GetProperName() : worldId.ToString();
		}

		protected virtual bool IsRowSkipped(TableRowKind kind) => kind == TableRowKind.WorldDivider;

		protected virtual bool HandleModifiedUpDown(int direction) => false;
		protected virtual bool HandleModifiedLeftRight(int direction) => false;
		protected virtual void OnTableActivate() { }
		protected virtual string GetSearchableColumnName(int col) => GetColumnName(col);

		protected virtual int FindInitialRow() {
			int activeWorldId = ClusterManager.Instance.activeWorldId;
			int fallback = -1;
			for (int i = 0; i < _rows.Count; i++) {
				var kind = _rows[i].Kind;
				if (kind == TableRowKind.Toolbar
					|| kind == TableRowKind.ColumnHeader
					|| kind == TableRowKind.WorldDivider)
					continue;
				if (_rows[i].WorldId == activeWorldId)
					return i;
				if (fallback < 0)
					fallback = i;
			}
			return fallback >= 0 ? fallback : 0;
		}

		// ========================================
		// SHARED QUERIES
		// ========================================

		protected static List<IAssignableIdentity> GetLiveMinionsForWorld(int worldId) {
			var result = new List<IAssignableIdentity>();
			foreach (var mi in Components.LiveMinionIdentities.Items) {
				if (mi != null && mi.GetMyWorldId() == worldId)
					result.Add(mi);
			}
			return result;
		}

		protected static List<StoredMinionIdentity> GetStoredMinions() {
			var result = new List<StoredMinionIdentity>();
			foreach (var storage in Components.MinionStorages.Items) {
				foreach (var info in storage.GetStoredMinionInfo()) {
					if (info.serializedMinion != null) {
						var smi = info.serializedMinion.Get<StoredMinionIdentity>();
						if (smi != null) result.Add(smi);
					}
				}
			}
			return result;
		}

		// ========================================
		// WORLD FILTER
		// ========================================

		protected void RebuildRows() {
			BuildRowList();
			RebuildFilteredWorldIds();
			if (_worldFilter != -1)
				ApplyWorldFilter();
		}

		private void RebuildFilteredWorldIds() {
			_filteredWorldIds.Clear();
			foreach (var row in _rows) {
				if (row.Kind != TableRowKind.Minion
					&& row.Kind != TableRowKind.StoredMinion
					&& row.Kind != TableRowKind.WorldDivider)
					continue;
				if (!_filteredWorldIds.Contains(row.WorldId))
					_filteredWorldIds.Add(row.WorldId);
			}
		}

		private void ApplyWorldFilter() {
			_rows.RemoveAll(row => {
				if (row.Kind == TableRowKind.Toolbar
					|| row.Kind == TableRowKind.ColumnHeader
					|| row.Kind == TableRowKind.Default)
					return false;
				return row.WorldId != _worldFilter;
			});
		}

		private void CycleWorldFilter(int direction) {
			int savedFilter = _worldFilter;
			_worldFilter = -1;
			RebuildRows();

			if (_filteredWorldIds.Count <= 1) {
				_worldFilter = savedFilter;
				return;
			}

			// Build cycle: [world ids...] + [-1 for All]
			var cycle = new List<int>(_filteredWorldIds);
			cycle.Add(-1);

			int currentIndex = cycle.IndexOf(savedFilter);
			if (currentIndex < 0) currentIndex = cycle.Count - 1;

			int newIndex = (currentIndex + direction + cycle.Count) % cycle.Count;
			_worldFilter = cycle[newIndex];

			if (_worldFilter != -1)
				ApplyWorldFilter();

			string name = _worldFilter == -1
				? STRINGS.ONIACCESS.TABLE.ALL_WORLDS
				: GetWorldName(_worldFilter);

			_row = FindFirstDataRow();
			_lastSpokenRow = -1;
			_lastSpokenCol = -1;

			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(
				name + ", " + BuildCellParts(forceFullContext: true)));
		}

		private int FindFirstDataRow() {
			for (int i = 0; i < _rows.Count; i++) {
				var kind = _rows[i].Kind;
				if (kind != TableRowKind.Toolbar
					&& kind != TableRowKind.ColumnHeader
					&& !IsRowSkipped(kind))
					return i;
			}
			return 0;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			OnTableActivate();
			_sortColumn = -1;
			_sortAscending = false;
			_worldFilter = -1;
			RebuildRows();
			if (_filteredWorldIds.Count > 1)
				_worldFilter = ClusterManager.Instance.activeWorldId;
			RebuildRows();
			_row = FindInitialRow();
			_col = 0;
			_lastSpokenRow = -1;
			_lastSpokenCol = -1;
			_search.Clear();
			SuppressSearchThisFrame();
			base.OnActivate();
			SpeechPipeline.SpeakQueued(
				WidgetSpeech.ComposeLabel(BuildCellParts(forceFullContext: true)));
		}

		public override void OnDeactivate() {
			_search.Clear();
			base.OnDeactivate();
		}

		// ========================================
		// SPEECH
		// ========================================

		protected void SpeakCell() {
			if (_row < 0 || _row >= _rows.Count) return;
			SpeechPipeline.SpeakInterrupt(
				WidgetSpeech.ComposeLabel(BuildCellParts(forceFullContext: false)));
		}

		protected string BuildCellParts(bool forceFullContext) {
			var row = _rows[_row];
			var parts = new List<string>();

			if (forceFullContext || _row != _lastSpokenRow) {
				string rowLabel = GetRowLabel(row);
				if (rowLabel != null)
					parts.Add(rowLabel);
			}

			if (forceFullContext || _col != _lastSpokenCol) {
				string colName = GetColumnName(_col);
				if (colName != null)
					parts.Add(colName);
			}

			parts.Add(GetCellValue(row));

			_lastSpokenRow = _row;
			_lastSpokenCol = _col;

			return string.Join(", ", parts);
		}

		// ========================================
		// NAVIGATION
		// ========================================

		protected void NavigateRow(int direction) {
			RebuildRows();
			int newRow = _row + direction;

			if (newRow < 0 || newRow >= _rows.Count) return;

			if (IsRowSkipped(_rows[newRow].Kind)) {
				int beyondDivider = newRow + direction;
				if (beyondDivider < 0 || beyondDivider >= _rows.Count) return;
				while (beyondDivider >= 0 && beyondDivider < _rows.Count
					&& IsRowSkipped(_rows[beyondDivider].Kind)) {
					beyondDivider += direction;
				}
				if (beyondDivider < 0 || beyondDivider >= _rows.Count) return;
				_row = beyondDivider;
				ClampCol();
				PlaySound("HUD_Mouseover");
				_lastSpokenRow = -1;
				_lastSpokenCol = -1;
				var landedKind = _rows[beyondDivider].Kind;
				if (landedKind == TableRowKind.Minion || landedKind == TableRowKind.StoredMinion) {
					string worldName = GetWorldName(_rows[beyondDivider].WorldId);
					SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(
						worldName + ", " + BuildCellParts(forceFullContext: true)));
				} else {
					SpeechPipeline.SpeakInterrupt(
						WidgetSpeech.ComposeLabel(BuildCellParts(forceFullContext: true)));
				}
				return;
			}

			_row = newRow;
			ClampCol();
			PlaySound("HUD_Mouseover");
			SpeakCell();
		}

		protected void NavigateCol(int direction) {
			if (_rows.Count == 0 || _row < 0 || _row >= _rows.Count) return;
			var row = _rows[_row];
			int maxCol = GetColumnCount(row.Kind) - 1;
			int newCol = _col + direction;

			if (ColumnWraps(row.Kind)) {
				if (newCol < 0) {
					_col = maxCol;
					PlaySound("HUD_Click");
				} else if (newCol > maxCol) {
					_col = 0;
					PlaySound("HUD_Click");
				} else {
					_col = newCol;
					PlaySound("HUD_Mouseover");
				}
			} else {
				if (newCol < 0 || newCol > maxCol) return;
				_col = newCol;
				PlaySound("HUD_Mouseover");
			}

			SpeakCell();
		}

		protected void NavigateHome() {
			RebuildRows();
			for (int i = 0; i < _rows.Count; i++) {
				var kind = _rows[i].Kind;
				if (kind != TableRowKind.Toolbar
					&& kind != TableRowKind.ColumnHeader
					&& !IsRowSkipped(kind)) {
					_row = i;
					PlaySound("HUD_Mouseover");
					SpeakCell();
					return;
				}
			}
		}

		protected void NavigateEnd() {
			RebuildRows();
			for (int i = _rows.Count - 1; i >= 0; i--) {
				if (!IsRowSkipped(_rows[i].Kind)) {
					_row = i;
					PlaySound("HUD_Mouseover");
					SpeakCell();
					return;
				}
			}
		}

		void ClampCol() {
			int maxCol = GetColumnCount(_rows[_row].Kind) - 1;
			if (_col > maxCol)
				_col = maxCol;
			if (_col < 0)
				_col = 0;
		}

		// ========================================
		// SORT
		// ========================================

		protected void CycleSort() {
			if (!IsColumnSortable(_col)) return;

			string colName = GetColumnName(_col);

			if (_sortColumn != _col) {
				_sortColumn = _col;
				_sortAscending = false;
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeMessage(
					string.Format(STRINGS.ONIACCESS.TABLE.SORT_DESC_FMT, colName)));
			} else if (!_sortAscending) {
				_sortAscending = true;
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeMessage(
					string.Format(STRINGS.ONIACCESS.TABLE.SORT_ASC_FMT, colName)));
			} else {
				_sortColumn = -1;
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeMessage(
					string.Format(STRINGS.ONIACCESS.TABLE.SORT_CLEARED_FMT, colName)));
			}

			RebuildRows();
		}

		// ========================================
		// SEARCH
		// ========================================

		private void SuppressSearchThisFrame() {
			_searchSuppressFrame = UnityEngine.Time.frameCount;
		}

		protected bool TryRouteToSearch(bool ctrlHeld, bool altHeld) {
			if (UnityEngine.Time.frameCount == _searchSuppressFrame)
				return false;
			if (ctrlHeld || altHeld) return false;

			for (var k = UnityEngine.KeyCode.A; k <= UnityEngine.KeyCode.Z; k++) {
				if (UnityEngine.Input.GetKeyDown(k)) {
					char c = (char)('a' + (k - UnityEngine.KeyCode.A));
					_search.AddChar(c);
					_search.Search(SearchItemCount, GetSearchLabel, SearchMoveTo);
					return true;
				}
			}

			return false;
		}

		// ISearchable

		public int SearchItemCount {
			get {
				if (_rows.Count == 0) return 0;
				return GetColumnCount(_rows[_row].Kind);
			}
		}

		public string GetSearchLabel(int index) {
			if (_rows.Count == 0) return null;
			string name = GetSearchableColumnName(index);
			if (name == null) return null;
			return TextFilter.FilterForSpeech(name);
		}

		public void SearchMoveTo(int index) {
			if (_rows.Count == 0) return;
			int maxCol = GetColumnCount(_rows[_row].Kind) - 1;
			if (index < 0 || index > maxCol) return;
			_col = index;
			SpeakCell();
		}

		// ========================================
		// HELP
		// ========================================

		protected static readonly List<HelpEntry> TableNavHelpEntries = new List<HelpEntry> {
			new HelpEntry("Arrows", STRINGS.ONIACCESS.TABLE.NAVIGATE_TABLE),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.TABLE.JUMP_FIRST_LAST),
			new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.TABLE.SWITCH_WORLD),
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
		};

		protected static readonly HelpEntry TableSortHelpEntry =
			new HelpEntry("Enter", STRINGS.ONIACCESS.TABLE.SORT_COLUMN);

		// ========================================
		// TICK
		// ========================================

		public override bool Tick() {
			if (base.Tick()) return true;

			bool ctrlHeld = InputUtil.CtrlHeld();
			bool altHeld = InputUtil.AltHeld();

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)) {
				bool shiftHeld = InputUtil.ShiftHeld();
				CycleWorldFilter(shiftHeld ? -1 : 1);
				return true;
			}

			if (TryRouteToSearch(ctrlHeld, altHeld))
				return true;

			if (_search.HasBuffer && UnityEngine.Input.anyKeyDown)
				_search.Clear();

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				if (ctrlHeld) {
					if (!HandleModifiedUpDown(1)) return true;
				} else {
					NavigateRow(-1);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				if (ctrlHeld) {
					if (!HandleModifiedUpDown(-1)) return true;
				} else {
					NavigateRow(1);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				if (ctrlHeld) {
					if (!HandleModifiedLeftRight(-1)) return true;
				} else {
					NavigateCol(-1);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				if (ctrlHeld) {
					if (!HandleModifiedLeftRight(1)) return true;
				} else {
					NavigateCol(1);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Home)) {
				NavigateHome();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.End)) {
				NavigateEnd();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				if (_row >= 0 && _row < _rows.Count) {
					var row = _rows[_row];
					if (row.Kind == TableRowKind.ColumnHeader)
						CycleSort();
					else
						OnEnterPressed(row);
				}
				return true;
			}

			return false;
		}
	}
}
