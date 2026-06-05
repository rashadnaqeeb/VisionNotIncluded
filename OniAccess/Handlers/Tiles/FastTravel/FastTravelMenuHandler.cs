using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Util;
using OniAccess.Widgets;

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
	public class FastTravelMenuHandler: NavTreeHandler {
		private List<FastTravelPoint> _points = new List<FastTravelPoint>();

		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.FAST_TRAVEL;
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		// Type-ahead searches the bookmarks; the "Create new" command row is excluded.
		protected override SearchScope SearchScope => SearchScope.Roots;

		public FastTravelMenuHandler() : base(null) {
			Nav.SearchFilter = n => n.RoleKey != NavRoles.Button;
			var help = new List<HelpEntry>();
			help.AddRange(DrillNavHelpEntries);
			help.Add(new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM));
			help.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			HelpEntries = help.AsReadOnly();
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>(_points.Count + 1);
			foreach (var pt in _points) {
				var point = pt;
				roots.Add(new MenuNode(
					() => string.Format(STRINGS.ONIACCESS.FAST_TRAVEL.ENTRY,
						point.Name, GridCoordinates.Format(point.Cell)),
					children: () => BuildActions(point),
					activate: () => { JumpToPoint(point); return true; },
					searchText: () => point.Name,
					contextLabel: () => point.Name));
			}
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.FAST_TRAVEL.CREATE_NEW,
				activate: () => { OpenCreatePrompt(); return true; },
				roleKey: NavRoles.Button));
			return roots;
		}

		private IReadOnlyList<NavItem> BuildActions(FastTravelPoint point) {
			return new List<NavItem> {
				new MenuNode(
					() => (string)STRINGS.ONIACCESS.FAST_TRAVEL.RENAME,
					activate: () => { OpenRenamePrompt(point); return true; }),
				new MenuNode(
					() => (string)STRINGS.ONIACCESS.FAST_TRAVEL.RELOCATE,
					activate: () => { RelocatePoint(point); return true; }),
				new MenuNode(
					() => (string)STRINGS.ONIACCESS.FAST_TRAVEL.DELETE,
					activate: () => { DeletePoint(point); return true; }),
			};
		}

		// Enter on a bookmark jumps to it; Right drills into its actions.
		protected override bool ShouldDrillOnActivate() => false;

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
				Nav.ClampToTree();
			}
			_search.Clear();
			SuppressSearchThisFrame();
			SpeechPipeline.SpeakInterrupt(DisplayName);
			AnnounceCurrent(interrupt: false);
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

		private void RefreshPoints() {
			int worldId = ClusterManager.Instance != null
				? ClusterManager.Instance.activeWorldId
				: 0;
			_points = FastTravelStorage.GetForWorld(worldId);
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void JumpToPoint(FastTravelPoint point) {
			// Skip the "closed" announcement Close() speaks — TeleportCursorTo's
			// destination-cell speech is the relevant audible feedback here.
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
			// The cursor handler may be buried under a build/tool handler when
			// the menu was opened from build mode; walk the stack to find it.
			for (int i = HandlerStack.Count - 1; i >= 0; i--) {
				if (HandlerStack.GetAt(i) is TileCursorHandler cursorHandler) {
					cursorHandler.TeleportCursorTo(point.Cell);
					return;
				}
			}
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
			RefreshPoints();
			Nav.SetPath(new[] { Nav.Path[0] });
			Nav.ClampToTree();
			AnnounceCurrent();
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
				Nav.ClampToTree();
				return;
			}
			Nav.SetPath(_pendingFocusLevel == 1
				? new[] { newIndex, 0 }
				: new[] { newIndex });
		}
	}
}
