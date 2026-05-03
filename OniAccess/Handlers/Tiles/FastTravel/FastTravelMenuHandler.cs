using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.FastTravel {
	/// <summary>
	/// Modal menu for named fast-travel points on the active world.
	///
	/// Level 0: bookmark rows for the current world (sorted alphabetically) plus
	/// a "Create new" row at the end. Enter on a bookmark jumps the cursor and
	/// closes the menu. Right drills into per-bookmark actions.
	///
	/// Level 1: Rename, Relocate, Delete. Enter activates the action.
	///
	/// Persistence: FastTravelStorage owns the YAML file in the colony folder.
	/// CRUD writes immediately; the snapshot held here is rebuilt on every
	/// activation and after each modification.
	/// </summary>
	public class FastTravelMenuHandler: NestedMenuHandler {
		private const int LeafRename = 0;
		private const int LeafRelocate = 1;
		private const int LeafDelete = 2;

		private List<FastTravelPoint> _points = new List<FastTravelPoint>();

		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.FAST_TRAVEL;
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 0;

		public FastTravelMenuHandler() : base(null) {
			var help = new List<HelpEntry>();
			help.AddRange(NestedNavHelpEntries);
			help.Add(new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM));
			help.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			HelpEntries = help.AsReadOnly();
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			RefreshPoints();
			if (_pendingFocusId != null) {
				ApplyPendingFocus();
				_pendingFocusId = null;
				_pendingFocusLevel = 0;
			} else {
				ClampIndices();
			}
			_search.Clear();
			SuppressSearchThisFrame();
			SpeechPipeline.SpeakInterrupt(DisplayName);
			SpeakCurrentItemQueued();
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.TOOLTIP.CLOSED);
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}

		// SpeakCurrentItem in NestedMenuHandler always interrupts. On open we
		// want the title to land first and the focused item to follow, so we
		// queue the item instead of interrupting.
		private void SpeakCurrentItemQueued() {
			var indices = new int[] { GetIndex(0), GetIndex(1) };
			int count = GetItemCount(Level, indices);
			if (count == 0) return;
			string label = GetItemLabel(Level, indices);
			if (string.IsNullOrWhiteSpace(label)) return;
			SpeechPipeline.SpeakQueued(label);
		}

		private void RefreshPoints() {
			int worldId = ClusterManager.Instance != null
				? ClusterManager.Instance.activeWorldId
				: 0;
			_points = FastTravelStorage.GetForWorld(worldId);
		}

		private void ClampIndices() {
			int level0Count = _points.Count + 1;
			int idx0 = GetIndex(0);
			if (idx0 >= level0Count) SetIndex(0, level0Count - 1);
			if (idx0 < 0) SetIndex(0, 0);

			// If we were drilled into a bookmark that no longer exists, go back.
			if (Level > 0 && GetIndex(0) >= _points.Count) {
				Level = 0;
			}
		}

		// ========================================
		// LEVEL DESCRIPTION
		// ========================================

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return _points.Count + 1;
			if (level == 1) {
				if (indices[0] < 0 || indices[0] >= _points.Count) return 0;
				return 3;
			}
			return 0;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0) {
				int i = indices[0];
				if (i == _points.Count) return (string)STRINGS.ONIACCESS.FAST_TRAVEL.CREATE_NEW;
				if (i < 0 || i >= _points.Count) return null;
				var point = _points[i];
				string coords = GridCoordinates.Format(point.Cell);
				return string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.ENTRY, point.Name, coords);
			}
			if (level == 1) {
				switch (indices[1]) {
					case LeafRename: return (string)STRINGS.ONIACCESS.FAST_TRAVEL.RENAME;
					case LeafRelocate: return (string)STRINGS.ONIACCESS.FAST_TRAVEL.RELOCATE;
					case LeafDelete: return (string)STRINGS.ONIACCESS.FAST_TRAVEL.DELETE;
				}
			}
			return null;
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level >= 1 && indices[0] >= 0 && indices[0] < _points.Count)
				return _points[indices[0]].Name;
			return null;
		}

		protected override bool ShouldDrillOnActivate() => false;

		// ========================================
		// LEAF ACTIVATION
		// ========================================

		protected override void ActivateLeafItem(int[] indices) {
			if (Level == 0) {
				int i = indices[0];
				if (i == _points.Count) {
					OpenCreatePrompt();
					return;
				}
				if (i < 0 || i >= _points.Count) return;
				JumpToPoint(_points[i]);
				return;
			}
			if (Level == 1) {
				if (indices[0] < 0 || indices[0] >= _points.Count) return;
				var target = _points[indices[0]];
				switch (indices[1]) {
					case LeafRename:
						OpenRenamePrompt(target);
						return;
					case LeafRelocate:
						RelocatePoint(target);
						return;
					case LeafDelete:
						DeletePoint(target);
						return;
				}
			}
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void JumpToPoint(FastTravelPoint point) {
			// Skip the "closed" announcement Close() speaks — TeleportCursorTo's
			// destination-cell speech is the relevant audible feedback here.
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
			if (HandlerStack.ActiveHandler is TileCursorHandler cursorHandler)
				cursorHandler.TeleportCursorTo(point.Cell);
		}

		private void OpenCreatePrompt() {
			int worldId = ClusterManager.Instance.activeWorldId;
			int cell = TileCursor.Instance.Cell;
			string prompt = (string)STRINGS.ONIACCESS.FAST_TRAVEL.CREATE_PROMPT;
			HandlerStack.Push(new TextPromptHandler(prompt, "", name => {
				if (string.IsNullOrWhiteSpace(name)) return;
				var added = FastTravelStorage.Add(name, worldId, cell);
				SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.CREATED, added.Name));
				// On reactivation OnActivate refreshes _points; seek to the new entry.
				_pendingFocusId = added.Id;
			}));
		}

		private void OpenRenamePrompt(FastTravelPoint target) {
			string targetId = target.Id;
			string prompt = (string)STRINGS.ONIACCESS.FAST_TRAVEL.RENAME_PROMPT;
			HandlerStack.Push(new TextPromptHandler(prompt, target.Name, name => {
				if (string.IsNullOrWhiteSpace(name)) return;
				FastTravelStorage.Rename(targetId, name);
				SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.RENAMED, name));
				_pendingFocusId = targetId;
				_pendingFocusLevel = 1;
			}));
		}

		private void RelocatePoint(FastTravelPoint target) {
			int newCell = TileCursor.Instance.Cell;
			FastTravelStorage.Relocate(target.Id, newCell);
			SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.RELOCATED, target.Name));
			RefreshPoints();
		}

		private void DeletePoint(FastTravelPoint target) {
			FastTravelStorage.Remove(target.Id);
			SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.DELETED, target.Name));
			Level = 0;
			RefreshPoints();
			ClampIndices();
			SpeakCurrentItem();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override int GetSearchItemCount(int[] indices) => _points.Count;

		protected override string GetSearchItemLabel(int flatIndex) {
			if (flatIndex < 0 || flatIndex >= _points.Count) return null;
			return _points[flatIndex].Name;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			outIndices[0] = flatIndex;
		}

		// ========================================
		// FOCUS RETENTION ACROSS PROMPTS
		// ========================================

		// Set inside Create/Rename onConfirm callbacks so OnActivate (fired
		// when the prompt pops) can land on the affected entry instead of
		// resetting position.
		private string _pendingFocusId;
		private int _pendingFocusLevel = 0;

		private void ApplyPendingFocus() {
			int newIndex = -1;
			for (int i = 0; i < _points.Count; i++) {
				if (_points[i].Id == _pendingFocusId) {
					newIndex = i;
					break;
				}
			}
			if (newIndex < 0) {
				ClampIndices();
				return;
			}
			SetIndex(0, newIndex);
			Level = _pendingFocusLevel;
			if (Level == 1) SetIndex(1, LeafRename);
		}
	}
}
