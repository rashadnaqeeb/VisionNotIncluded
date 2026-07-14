using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.Sections;
using OniAccess.Handlers.Tiles.ToolProfiles;
using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Non-modal handler for build tool placement. Sits on top of
	/// TileCursorHandler, intercepts build-specific keys (Space, R, Tab,
	/// I, 0-9, Shift+Space) and passes arrows through to the tile cursor.
	///
	/// Handles both regular buildings (single-cell placement via BuildTool)
	/// and utility buildings (straight-line path via UtilityBuildTool/WireBuildTool).
	/// </summary>
	public class BuildToolHandler: BaseScreenHandler {
		public static BuildToolHandler Instance { get; private set; }

		private HashedString _category;
		internal BuildingDef _def;
		private bool _isUtility;

		internal bool SuppressToolEvents { get; set; }

		// Utility placement state
		private int _utilityStartCell = Grid.InvalidCell;
		private int _lastDragCell = Grid.InvalidCell;
		internal bool UtilityStartSet => _utilityStartCell != Grid.InvalidCell;
		internal int UtilityStartCell => _utilityStartCell;

		// Rectangle mode state (1x1 non-utility buildings only)
		private enum RectMode { Off, Filled, Hollow }
		private readonly RectangleSelection _rectSelection = new RectangleSelection();
		private RectMode _rectMode;
		private bool CanUseRectMode => _def.PlacementOffsets.Length == 1 && !_isUtility;
		private ToolProfile _rectProfile;

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.Space, Modifier.Shift),
			new ConsumedKey(KKeyCode.Return),
			new ConsumedKey(KKeyCode.R),
			new ConsumedKey(KKeyCode.R, Modifier.Shift),
			new ConsumedKey(KKeyCode.Tab),
			new ConsumedKey(KKeyCode.I),
			new ConsumedKey(KKeyCode.P, Modifier.Shift),
			new ConsumedKey(KKeyCode.G, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.Alpha0),
			new ConsumedKey(KKeyCode.Alpha1),
			new ConsumedKey(KKeyCode.Alpha2),
			new ConsumedKey(KKeyCode.Alpha3),
			new ConsumedKey(KKeyCode.Alpha4),
			new ConsumedKey(KKeyCode.Alpha5),
			new ConsumedKey(KKeyCode.Alpha6),
			new ConsumedKey(KKeyCode.Alpha7),
			new ConsumedKey(KKeyCode.Alpha8),
			new ConsumedKey(KKeyCode.Alpha9),
			new ConsumedKey(KKeyCode.Keypad0),
			new ConsumedKey(KKeyCode.Keypad1),
			new ConsumedKey(KKeyCode.Keypad2),
			new ConsumedKey(KKeyCode.Keypad3),
			new ConsumedKey(KKeyCode.Keypad4),
			new ConsumedKey(KKeyCode.Keypad5),
			new ConsumedKey(KKeyCode.Keypad6),
			new ConsumedKey(KKeyCode.Keypad7),
			new ConsumedKey(KKeyCode.Keypad8),
			new ConsumedKey(KKeyCode.Keypad9),
		};
		public override IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		public override string DisplayName => BuildMenuData.BuildNameAnnouncement(_def);
		public override bool CapturesAllInput => false;

		/// <summary>
		/// Computes the cell offset from bottom-left corner to the game's
		/// placement origin for a given building and orientation.
		/// </summary>
		internal static CellOffset BottomLeftToOriginOffset(BuildingDef def, Orientation orientation) {
			int minX = 0, minY = 0;
			foreach (var offset in def.PlacementOffsets) {
				var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
				if (rotated.x < minX) minX = rotated.x;
				if (rotated.y < minY) minY = rotated.y;
			}
			return new CellOffset(-minX, -minY);
		}

		/// <summary>
		/// Returns the offset from the rotated input-end cell to the
		/// placement origin for a horizontal flow building.
		/// </summary>
		internal static CellOffset InputEndToOriginOffset(BuildingDef def, Orientation orientation) {
			var inputEnd = BuildMenuData.InputEndOffset(def);
			var rotated = Rotatable.GetRotatedCellOffset(inputEnd, orientation);
			return new CellOffset(-rotated.x, -rotated.y);
		}

		private int GetOriginCell() {
			var orientation = BuildMenuData.GetCurrentOrientation();
			var shift = BuildMenuData.IsHorizontalFlowBuilding(_def)
				? InputEndToOriginOffset(_def, orientation)
				: BottomLeftToOriginOffset(_def, orientation);
			return Grid.OffsetCell(TileCursor.Instance.Cell, shift);
		}

		private static readonly IReadOnlyList<HelpEntry> _singleModeNoRectHelp = new List<HelpEntry> {
			new HelpEntry("Space", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PLACE),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PLACE_AND_EXIT),
			new HelpEntry("R", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_ROTATE),
			new HelpEntry("Shift+R", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_ROTATE_REVERSE),
			new HelpEntry("Tab", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_BUILDING_LIST),
			new HelpEntry("I", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_INFO),
			new HelpEntry("Shift+P", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PORTS),
			new HelpEntry("0-9", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.SET_PRIORITY),
			new HelpEntry("Shift+Space", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_CANCEL_CONSTRUCTION),
			new HelpEntry("Escape", (string)STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		private static readonly IReadOnlyList<HelpEntry> _singleModeHelp = new List<HelpEntry> {
			new HelpEntry("Space", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PLACE),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PLACE_AND_EXIT),
			new HelpEntry("Ctrl+G", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_RECT_MODE),
			new HelpEntry("R", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_ROTATE),
			new HelpEntry("Shift+R", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_ROTATE_REVERSE),
			new HelpEntry("Tab", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_BUILDING_LIST),
			new HelpEntry("I", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_INFO),
			new HelpEntry("Shift+P", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PORTS),
			new HelpEntry("0-9", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.SET_PRIORITY),
			new HelpEntry("Shift+Space", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_CANCEL_CONSTRUCTION),
			new HelpEntry("Escape", (string)STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		private static readonly IReadOnlyList<HelpEntry> _rectModeHelp = new List<HelpEntry> {
			new HelpEntry("Space", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.SET_CORNER),
			new HelpEntry("Shift+Space", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CLEAR_RECT),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CONFIRM_TOOL),
			new HelpEntry("Ctrl+G", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_RECT_MODE),
			new HelpEntry("Tab", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_BUILDING_LIST),
			new HelpEntry("I", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_INFO),
			new HelpEntry("Shift+P", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_PORTS),
			new HelpEntry("0-9", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.SET_PRIORITY),
			new HelpEntry("Escape", (string)STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries =>
			_rectMode != RectMode.Off ? _rectModeHelp
			: CanUseRectMode ? _singleModeHelp
			: _singleModeNoRectHelp;

		public BuildToolHandler(HashedString category, BuildingDef def) {
			_category = category;
			_def = def;
			_isUtility = BuildMenuData.IsUtilityBuilding(def);
			if (CanUseRectMode) {
				_rectProfile = new ToolProfile("BuildRectMode",
					new GlanceComposer(new List<ICellSection> {
						ToolProfileRegistry.Selection,
						GlanceComposer.Building,
						new Tiles.ToolProfiles.Sections.BuildPrioritySection(),
						GlanceComposer.Element,
						new Tiles.ToolProfiles.Sections.BuildExtentSection()
					}.AsReadOnly()));
			}
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			Instance = this;

			if (Game.Instance != null) {
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);
				Game.Instance.Subscribe(1174281782, OnActiveToolChanged);
			}
		}

		/// <summary>
		/// Called by ActionMenuHandler after SelectBuilding returns.
		/// At this point the active tool is known (PrebuildTool or BuildTool).
		/// </summary>
		internal void AnnounceInitialState() {
			string announcement = BuildMenuData.BuildNameAnnouncement(_def);
			if (IsInPrebuildMode()) {
				string error = GetPrebuildError();
				if (!string.IsNullOrEmpty(error))
					announcement = string.Format(STRINGS.ONIACCESS.BUILD_MENU.PREBUILD_ERROR, announcement, error);
				SpeechPipeline.SpeakInterrupt(announcement);
			} else {
				SetupBuildMode();
				SpeechPipeline.SpeakInterrupt(announcement);
				SpeechPipeline.SpeakQueued(BuildMenuData.GetMaterialSummary(_def));
			}
		}

		private void SetupBuildMode() {
			if (TileCursor.Instance != null) {
				var profile = ToolProfileRegistry.Instance.GetProfile(
					_isUtility ? GetUtilityToolType() : typeof(BuildTool));
				TileCursor.Instance.ActiveToolProfile = profile;
			}
		}

		public override void OnDeactivate() {
			Instance = null;

			if (TileCursor.Instance != null)
				TileCursor.Instance.ActiveToolProfile = null;

			if (Game.Instance != null)
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);

			_utilityStartCell = Grid.InvalidCell;
			_rectMode = RectMode.Off;
			_rectSelection.ClearAll();
		}

		private void OnActiveToolChanged(object data) {
			if (SuppressToolEvents) return;

			if (data is SelectTool) {
				if (_def != null && _def.OnePerWorld)
					SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.PLACED);
				else
					SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.CANCELED);
				PlayDeactivateSound();
				QueueOverlayAndPop();
				return;
			}

			if (data is BuildTool || data is UtilityBuildTool || data is WireBuildTool) {
				SetupBuildMode();
				SpeechPipeline.SpeakQueued(BuildMenuData.GetMaterialSummary(_def));
				return;
			}

			if (data is PrebuildTool) {
				if (TileCursor.Instance != null)
					TileCursor.Instance.ActiveToolProfile = null;
				string error = GetPrebuildError();
				if (!string.IsNullOrEmpty(error))
					SpeechPipeline.SpeakInterrupt(error);
				return;
			}

			SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.CANCELED);
			PlayDeactivateSound();
			QueueOverlayAndPop();
			PushToolHandlerFor(data as InterfaceTool);
		}

		private void PushToolHandlerFor(InterfaceTool tool) {
			if (tool == null) return;
			if (tool is CopySettingsTool)
				HandlerStack.Push(new Tools.CopySettingsHandler());
			else if (tool is MoveToLocationTool)
				HandlerStack.Push(new Tools.MoveToLocationHandler());
			else if (tool is PlaceTool)
				HandlerStack.Push(new Tools.PlaceToolHandler());
			else if (tool is DragTool)
				HandlerStack.Push(new Tools.ToolHandler());
		}

		private bool IsInPrebuildMode() =>
			PlayerController.Instance.ActiveTool is PrebuildTool;

		private string GetPrebuildError() {
			var card = PrebuildTool.Instance.GetComponent<PrebuildToolHoverTextCard>();
			return card.errorMessage;
		}

		private bool HandlePrebuildError() {
			if (!IsInPrebuildMode()) return false;
			PlaySound("Negative");
			string error = GetPrebuildError();
			SpeechPipeline.SpeakInterrupt(
				error ?? (string)STRINGS.ONIACCESS.BUILD_MENU.NOT_BUILDABLE);
			return true;
		}

		// ========================================
		// KEY HANDLING
		// ========================================

		public override bool Tick() {
			// Drag sound tracking for utility start or rect mode pending corner
			if (_utilityStartCell != Grid.InvalidCell) {
				int cell = TileCursor.Instance.Cell;
				if (cell != _lastDragCell) {
					_lastDragCell = cell;
					if (IsValidDragTarget(cell))
						PlayDragSound(Grid.GetCellDistance(cell, _utilityStartCell) + 1);
				}
			} else if (_rectMode != RectMode.Off && _rectSelection.PendingFirstCorner != Grid.InvalidCell) {
				int cell = TileCursor.Instance.Cell;
				if (cell != _rectSelection.LastDragCell) {
					_rectSelection.LastDragCell = cell;
					int tileCount = RectangleSelection.TileCountBetween(
						_rectSelection.PendingFirstCorner, cell);
					PlayDragSound(tileCount);
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.G)
				&& InputUtil.CtrlHeld() && !InputUtil.ShiftHeld() && !InputUtil.AltHeld()) {
				ToggleRectMode();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) {
				if (InputUtil.ShiftHeld()) {
					if (_rectMode != RectMode.Off) {
						RectShiftSpace();
					} else if (_isUtility && UtilityStartSet) {
						_utilityStartCell = Grid.InvalidCell;
						_lastDragCell = Grid.InvalidCell;
						SpeechPipeline.SpeakInterrupt(
							(string)STRINGS.ONIACCESS.BUILD_MENU.START_CLEARED);
					} else
						QuickCancel();
				} else if (!InputUtil.AnyModifierHeld()) {
					if (HandlePrebuildError()) {
					} else if (_rectMode != RectMode.Off)
						RectSetCorner();
					else if (_isUtility)
						UtilityPlacement();
					else
						RegularPlacement();
				}
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
				&& !InputUtil.AnyModifierHeld()) {
				if (HandlePrebuildError()) {
				} else if (_rectMode != RectMode.Off)
					RectConfirm();
				else if (_isUtility)
					UtilityPlaceAndExit();
				else
					RegularPlaceAndExit();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R)) {
				if (InputUtil.ShiftHeld()) {
					if (HandlePrebuildError()) {
					} else {
						RotateReverse();
					}
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					if (HandlePrebuildError()) {
					} else {
						Rotate();
					}
					return true;
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)
				&& !InputUtil.AnyModifierHeld()) {
				ReturnToBuildingList();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.I)
				&& !InputUtil.AnyModifierHeld()) {
				OpenInfoPanel();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P)
				&& InputUtil.ShiftHeld() && !InputUtil.CtrlHeld() && !InputUtil.AltHeld()) {
				AnnouncePortLayout();
				return true;
			}

			if (!InputUtil.AnyModifierHeld()) {
				int digit = InputUtil.GetDigitKeyDown();
				if (digit >= 0) {
					SetPriority(digit);
					return true;
				}
			}
			return false;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (e.TryConsume(Action.Escape)) {
				CloseEverything();
				return true;
			}
			if (e.TryConsume(Action.RotateBuilding))
				return true;
			for (int i = (int)Action.Plan1; i <= (int)Action.Plan14; i++) {
				if (e.TryConsume((Action)i))
					return true;
			}
			return false;
		}

		// ========================================
		// REGULAR PLACEMENT
		// ========================================

		private void RegularPlacement() => PlaceRegular(exitAfter: false);
		private void RegularPlaceAndExit() => PlaceRegular(exitAfter: true);

		private void PlaceRegular(bool exitAfter) {
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}
			if (!(PlayerController.Instance.ActiveTool is BuildTool)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.NOT_BUILDABLE);
				return;
			}
			int originCell = GetOriginCell();
			var pos = Grid.CellToPosCBC(originCell, _def.SceneLayer);
			var orientation = BuildMenuData.GetCurrentOrientation();
			// Move the visualizer before validation: IsValidPlaceLocation
			// reads link cells from the visualizer transform (GetCells),
			// and TryBuild checks Grid.PosToCell(visualizer) == cell.
			BuildTool.Instance.visualizer.transform.SetPosition(pos);
			string failReason;
			if (!_def.IsValidPlaceLocation(BuildTool.Instance.visualizer, pos, orientation, out failReason)
				&& !(_def.ReplacementLayer != ObjectLayer.NumLayers
					&& _def.IsValidPlaceLocation(BuildTool.Instance.visualizer, pos, orientation, replace_tile: true, out failReason))) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(failReason ?? (string)STRINGS.ONIACCESS.BUILD_MENU.OBSTRUCTED);
				return;
			}

			bool hasMaterials = HasSufficientMaterials();
			try {
				_buildToolLastDragCell.SetValue(BuildTool.Instance, -1);
			} catch (System.Exception ex) {
				Util.Log.Error($"BuildToolHandler.PlaceRegular: {ex}");
			}
			BuildTool.Instance.OnLeftClickDown(pos);
			BuildTool.Instance.OnLeftClickUp(pos);
			// OnePerWorld buildings auto-dismiss the tool, triggering
			// OnActiveToolChanged which announces "placed" and pops.
			if (_def.OnePerWorld)
				return;

			string announcement = hasMaterials
				? (string)STRINGS.ONIACCESS.BUILD_MENU.PLACED
				: (string)STRINGS.ONIACCESS.BUILD_MENU.PLACED_NO_MATERIAL;
			SpeechPipeline.SpeakInterrupt(announcement);
			if (exitAfter)
				ExitBuildMode();
		}

		private bool HasSufficientMaterials() {
			try {
				var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;
				if (panel.CurrentSelectedElement == null)
					return true;
				return _def.MaterialsAvailable(panel.GetSelectedElementAsList, ClusterManager.Instance.activeWorld)
					|| DebugHandler.InstantBuildMode;
			} catch (Exception ex) {
				Util.Log.Warn($"BuildToolHandler.HasSufficientMaterials: {ex.Message}");
				return true;
			}
		}

		// ========================================
		// UTILITY PLACEMENT
		// ========================================

		private void UtilityPlacement() {
			int cell = TileCursor.Instance.Cell;

			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			if (!UtilityStartSet) {
				if (!IsValidUtilityCell(cell)) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.OBSTRUCTED);
					return;
				}
				_utilityStartCell = cell;
				_lastDragCell = cell;
				PlayDragSound(1);
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.START_SET);
				return;
			}

			int startCol = Grid.CellColumn(_utilityStartCell);
			int startRow = Grid.CellRow(_utilityStartCell);
			int endCol = Grid.CellColumn(cell);
			int endRow = Grid.CellRow(cell);
			bool sameCol = startCol == endCol;
			bool sameRow = startRow == endRow;

			if (!sameCol && !sameRow) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.MUST_BE_STRAIGHT);
				return;
			}

			var path = BuildLinePath(_utilityStartCell, cell);
			var tool = GetActiveUtilityTool();
			if (tool == null) {
				Util.Log.Error("BuildToolHandler.UtilityPlacement: no active utility tool");
				return;
			}

			if (!ValidateUtilityPath(path)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.INVALID_LINE);
				return;
			}

			SimulateUtilityDrag(path, tool);
			_utilityStartCell = Grid.InvalidCell;
			_lastDragCell = Grid.InvalidCell;
			SpeechPipeline.SpeakInterrupt(
				string.Format((string)STRINGS.ONIACCESS.BUILD_MENU.LINE_CELLS, path.Count)
				+ ", " + (string)STRINGS.ONIACCESS.BUILD_MENU.PLACED);
		}

		private void UtilityPlaceAndExit() {
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			var tool = GetActiveUtilityTool();
			if (tool == null) {
				Util.Log.Error("BuildToolHandler.UtilityPlaceAndExit: no active utility tool");
				return;
			}

			if (UtilityStartSet) {
				int startCol = Grid.CellColumn(_utilityStartCell);
				int startRow = Grid.CellRow(_utilityStartCell);
				int endCol = Grid.CellColumn(cell);
				int endRow = Grid.CellRow(cell);
				bool sameCol = startCol == endCol;
				bool sameRow = startRow == endRow;

				if (!sameCol && !sameRow) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.MUST_BE_STRAIGHT);
					return;
				}

				var path = BuildLinePath(_utilityStartCell, cell);
				if (!ValidateUtilityPath(path)) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.INVALID_LINE);
					return;
				}

				SimulateUtilityDrag(path, tool);
				SpeechPipeline.SpeakInterrupt(
					string.Format((string)STRINGS.ONIACCESS.BUILD_MENU.LINE_CELLS, path.Count)
					+ ", " + (string)STRINGS.ONIACCESS.BUILD_MENU.PLACED);
			} else {
				if (!IsValidUtilityCell(cell)) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.OBSTRUCTED);
					return;
				}

				var path = new List<int> { cell };
				SimulateUtilityDrag(path, tool);
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.PLACED);
			}

			ExitBuildMode();
		}

		private static List<int> BuildLinePath(int startCell, int endCell) {
			var path = new List<int>();
			int startCol = Grid.CellColumn(startCell);
			int startRow = Grid.CellRow(startCell);
			int endCol = Grid.CellColumn(endCell);
			int endRow = Grid.CellRow(endCell);

			if (startRow == endRow) {
				int step = startCol <= endCol ? 1 : -1;
				for (int x = startCol; x != endCol + step; x += step)
					path.Add(Grid.XYToCell(x, startRow));
			} else {
				int step = startRow <= endRow ? 1 : -1;
				for (int y = startRow; y != endRow + step; y += step)
					path.Add(Grid.XYToCell(startCol, y));
			}
			return path;
		}

		/// <summary>
		/// Checks whether every cell in a straight line from start to end is
		/// a valid placement location. Uses the same IsValidPlaceLocation
		/// check that the game's TryPlace uses, so the result matches what
		/// will actually happen when the line is placed.
		/// Used by BuildToolSection for live glance feedback.
		/// </summary>
		internal static bool IsUtilityLineValid(int startCell, int endCell) {
			var handler = Instance;
			if (handler == null) return true;
			var path = BuildLinePath(startCell, endCell);
			return handler.ValidateUtilityPath(path);
		}

		private bool ValidateUtilityPath(List<int> path) {
			foreach (int cell in path) {
				if (!IsValidUtilityCell(cell))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Mirrors BaseUtilityBuildTool.CheckValidPathPiece: allows cells
		/// with existing utilities that have KAnimGraphTileVisualizer
		/// (i.e. built conduits/wires), unlike IsValidPlaceLocation which
		/// rejects any occupied cell.
		/// </summary>
		private bool IsValidUtilityCell(int cell) {
			if (_def.BuildLocationRule == BuildLocationRule.NotInTiles) {
				if (Grid.Objects[cell, 9] != null)
					return false;
				if (Grid.HasDoor[cell])
					return false;
			}
			var objLayerGo = Grid.Objects[cell, (int)_def.ObjectLayer];
			if (objLayerGo != null && objLayerGo.GetComponent<KAnimGraphTileVisualizer>() == null)
				return false;
			var tileLayerGo = Grid.Objects[cell, (int)_def.TileLayer];
			if (tileLayerGo != null && tileLayerGo.GetComponent<KAnimGraphTileVisualizer>() == null)
				return false;
			return true;
		}

		private static readonly FieldInfo _buildToolLastDragCell = AccessTools.Field(
			typeof(BuildTool), "lastDragCell");

		private static readonly MethodInfo _onDragTool = AccessTools.Method(
			typeof(BaseUtilityBuildTool), "OnDragTool");

		private void SimulateUtilityDrag(List<int> path, BaseUtilityBuildTool tool) {
			if (path.Count == 0) return;

			var startPos = Grid.CellToPosCCC(path[0], Grid.SceneLayer.Move);
			tool.OnLeftClickDown(startPos);

			for (int i = 1; i < path.Count; i++) {
				try {
					_onDragTool.Invoke(tool, new object[] { path[i], i });
				} catch (Exception ex) {
					Util.Log.Error($"BuildToolHandler.SimulateUtilityDrag: {ex}");
				}
			}

			var endPos = Grid.CellToPosCCC(path[path.Count - 1], Grid.SceneLayer.Move);
			tool.OnLeftClickUp(endPos);
		}

		private BaseUtilityBuildTool GetActiveUtilityTool() {
			var active = PlayerController.Instance.ActiveTool;
			if (active is WireBuildTool wbt) return wbt;
			if (active is UtilityBuildTool ubt) return ubt;
			if (active is BaseUtilityBuildTool bubt) return bubt;
			return null;
		}

		private Type GetUtilityToolType() {
			if (_def.BuildingComplete.GetComponent<Wire>() != null)
				return typeof(WireBuildTool);
			return typeof(UtilityBuildTool);
		}

		// ========================================
		// ROTATION
		// ========================================

		private void Rotate() {
			if (_isUtility || _def.PermittedRotations == PermittedRotations.Unrotatable) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.NOT_ROTATABLE);
				return;
			}
			if (!(PlayerController.Instance.ActiveTool is BuildTool)) return;

			BuildTool.Instance.TryRotate();
			AnnounceRotation();
		}

		private void RotateReverse() {
			if (_isUtility || _def.PermittedRotations == PermittedRotations.Unrotatable) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.NOT_ROTATABLE);
				return;
			}
			if (!(PlayerController.Instance.ActiveTool is BuildTool)) return;

			// R360 cycles through 4 orientations; 3 forward steps = 1 reverse step.
			// All other types are 2-state toggles where forward = reverse.
			int steps = _def.PermittedRotations == PermittedRotations.R360 ? 3 : 1;
			for (int i = 0; i < steps; i++)
				BuildTool.Instance.TryRotate();
			AnnounceRotation();
		}

		private void AnnounceRotation() {
			var orientation = BuildMenuData.GetCurrentOrientation();
			var parts = new List<string> { BuildMenuData.GetOrientationName(orientation, _def) };

			if (PlayerController.Instance.ActiveTool is BuildTool) {
				int originCell = GetOriginCell();
				var pos = Grid.CellToPosCBC(originCell, _def.SceneLayer);
				BuildTool.Instance.visualizer.transform.SetPosition(pos);
				string failReason;
				if (!_def.IsValidPlaceLocation(
						BuildTool.Instance.visualizer, pos, orientation, out failReason)
					&& !(_def.ReplacementLayer != ObjectLayer.NumLayers
						&& _def.IsValidPlaceLocation(BuildTool.Instance.visualizer, pos, orientation, replace_tile: true, out failReason)))
					parts.Add(failReason ?? (string)STRINGS.ONIACCESS.BUILD_MENU.OBSTRUCTED);
			}

			string extent = BuildExtentText(orientation);
			if (extent != null)
				parts.Add(extent);

			SpeechPipeline.SpeakInterrupt(string.Join(", ", parts));
		}

		/// <summary>
		/// Builds an extent description like "extends 2 right, 1 up"
		/// for buildings larger than 1x1. For normal buildings, extents are
		/// relative to the bottom-left corner. For horizontal flow buildings,
		/// extents are relative to the rotated input-end position.
		/// </summary>
		internal static string BuildExtentText(Orientation orientation) {
			var handler = Instance;
			if (handler == null || handler._def == null) return null;
			var offsets = handler._def.PlacementOffsets;
			if (offsets == null || offsets.Length <= 1) return null;

			bool horizontalFlow = BuildMenuData.IsHorizontalFlowBuilding(handler._def);

			int minX = 0, maxX = 0, minY = 0, maxY = 0;
			foreach (var offset in offsets) {
				var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
				if (rotated.x < minX) minX = rotated.x;
				if (rotated.x > maxX) maxX = rotated.x;
				if (rotated.y < minY) minY = rotated.y;
				if (rotated.y > maxY) maxY = rotated.y;
			}

			var parts = new List<string>();

			if (horizontalFlow) {
				var inputEnd = Rotatable.GetRotatedCellOffset(
					BuildMenuData.InputEndOffset(handler._def), orientation);
				int right = maxX - inputEnd.x;
				int left = inputEnd.x - minX;
				int up = maxY - inputEnd.y;
				int down = inputEnd.y - minY;
				if (right > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_RIGHT, right));
				if (left > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_LEFT, left));
				if (up > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_UP, up));
				if (down > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_DOWN, down));
			} else {
				int right = maxX - minX;
				int up = maxY - minY;
				if (right > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_RIGHT, right));
				if (up > 0)
					parts.Add(string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_UP, up));
			}

			if (parts.Count == 0) return null;
			return string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_FORMAT,
				string.Join(", ", parts));
		}

		// ========================================
		// RECTANGLE MODE
		// ========================================

		public bool IsCellSelected(int cell) =>
			_rectMode != RectMode.Off && _rectSelection.IsCellSelected(cell);

		private void ToggleRectMode() {
			if (!CanUseRectMode) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.RECT_MODE_UNAVAILABLE);
				return;
			}

			switch (_rectMode) {
				case RectMode.Off:
					_rectMode = RectMode.Filled;
					if (TileCursor.Instance != null)
						TileCursor.Instance.ActiveToolProfile = _rectProfile;
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.RECT_MODE_ON);
					break;
				case RectMode.Filled:
					_rectMode = RectMode.Hollow;
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.RECT_MODE_HOLLOW);
					break;
				default:
					_rectMode = RectMode.Off;
					_rectSelection.ClearAll();
					SetupBuildMode();
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.BUILD_MENU.RECT_MODE_OFF);
					break;
			}
		}

		private void RectSetCorner() {
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			bool hollow = _rectMode == RectMode.Hollow;
			var result = _rectSelection.SetCorner(cell, out var rect, hollow);
			if (result == RectangleSelection.SetCornerResult.FirstCornerSet) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.CORNER_SET);
				PlayDragSound(1);
			} else {
				int tileCount = hollow
					? RectangleSelection.ComputePerimeter(rect.Cell1, rect.Cell2)
					: RectangleSelection.ComputeArea(rect.Cell1, rect.Cell2);
				PlayDragSound(tileCount);
				SpeechPipeline.SpeakInterrupt(
					RectangleSelection.BuildRectSummary(
						rect.Cell1, rect.Cell2, CountValidPlacements, hollow: hollow));
			}
		}

		private void RectShiftSpace() {
			int cell = TileCursor.Instance.Cell;
			if (_rectSelection.ClearRectAtCursor(cell)) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.RECT_CLEARED);
			} else if (_rectSelection.PendingFirstCorner != Grid.InvalidCell) {
				_rectSelection.ClearAll();
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.SELECTION_CLEARED);
			} else if (!_rectSelection.IsCellWithinBounds(cell)) {
				// The open middle of a hollow rectangle does nothing;
				// quick cancel only fires outside every rectangle
				QuickCancel();
			}
		}

		private void RectConfirm() {
			if (!_rectSelection.HasSelection) {
				// No selection: place single cell and exit (current Enter behavior)
				RegularPlaceAndExit();
				return;
			}

			if (_rectSelection.PendingFirstCorner != Grid.InvalidCell) {
				int cell = TileCursor.Instance.Cell;
				if (!Grid.IsVisible(cell)) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
					return;
				}
				_rectSelection.SetCorner(cell, out _, _rectMode == RectMode.Hollow);
			}

			SubmitBuildRectangles();
		}

		private void SubmitBuildRectangles() {
			if (!(PlayerController.Instance.ActiveTool is BuildTool)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.NOT_BUILDABLE);
				return;
			}

			var orientation = BuildMenuData.GetCurrentOrientation();
			int placed = 0;

			try {
				foreach (var rect in _rectSelection.GetRectangles()) {
					rect.GetBounds(out int minX, out int maxX, out int minY, out int maxY);
					for (int y = minY; y <= maxY; y++) {
						for (int x = minX; x <= maxX; x++) {
							if (rect.Hollow && x != minX && x != maxX && y != minY && y != maxY)
								continue;
							int cell = Grid.XYToCell(x, y);
							if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell))
								continue;

							var pos = Grid.CellToPosCBC(cell, _def.SceneLayer);
							BuildTool.Instance.visualizer.transform.SetPosition(pos);
							if (!_def.IsValidPlaceLocation(
									BuildTool.Instance.visualizer, pos, orientation, out _)
								&& !(_def.ReplacementLayer != ObjectLayer.NumLayers
									&& _def.IsValidPlaceLocation(BuildTool.Instance.visualizer, pos, orientation, replace_tile: true, out _)))
								continue;
							_buildToolLastDragCell.SetValue(BuildTool.Instance, -1);
							BuildTool.Instance.OnLeftClickDown(pos);
							BuildTool.Instance.OnLeftClickUp(pos);
							placed++;
						}
					}
				}
			} catch (Exception ex) {
				Util.Log.Error($"BuildToolHandler.SubmitBuildRectangles: {ex}");
			}

			if (placed == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.NO_VALID_CELLS);
				PlaySound("Negative");
				ExitBuildMode();
				return;
			}

			string priorityText = ReadBuildPriority();
			SpeechPipeline.SpeakInterrupt(
				string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.CONFIRM_BUILD_RECT,
					placed, priorityText));
			ExitBuildMode();
		}

		private int CountValidPlacements(int cell) {
			var orientation = BuildMenuData.GetCurrentOrientation();
			var pos = Grid.CellToPosCBC(cell, _def.SceneLayer);
			BuildTool.Instance.visualizer.transform.SetPosition(pos);
			return (_def.IsValidPlaceLocation(
				BuildTool.Instance.visualizer, pos, orientation, out _)
				|| (_def.ReplacementLayer != ObjectLayer.NumLayers
					&& _def.IsValidPlaceLocation(BuildTool.Instance.visualizer, pos, orientation, replace_tile: true, out _))) ? 1 : 0;
		}

		private static string ReadBuildPriority() {
			try {
				var priority = PlanScreen.Instance.ProductInfoScreen
					.materialSelectionPanel.PriorityScreen
					.GetLastSelectedPriority();
				if (priority.priority_class == PriorityScreen.PriorityClass.topPriority)
					return (string)STRINGS.ONIACCESS.TOOLS.PRIORITY_EMERGENCY;
				return priority.priority_value.ToString();
			} catch (Exception ex) {
				Util.Log.Warn($"BuildToolHandler.ReadBuildPriority: {ex.Message}");
				return 5.ToString();
			}
		}

		// ========================================
		// QUICK CANCEL
		// ========================================

		private void QuickCancel() {
			int cell = TileCursor.Instance.Cell;
			var go = FindMatchingConstruction(cell);
			if (go == null) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.NO_CONSTRUCTION);
				return;
			}

			go.Trigger((int)GameHashes.Cancel);
			PlayCancelSound();
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.BUILD_MENU.CANCEL_CONSTRUCTION);
		}

		private UnityEngine.GameObject FindMatchingConstruction(int cell) {
			int[] layers = {
				(int)ObjectLayer.Building,
				(int)ObjectLayer.FoundationTile,
				(int)ObjectLayer.Backwall,
				(int)ObjectLayer.Wire,
				(int)ObjectLayer.LiquidConduit,
				(int)ObjectLayer.GasConduit,
				(int)ObjectLayer.SolidConduit,
				(int)ObjectLayer.LogicWire,
			};
			foreach (int layer in layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null) continue;
				var buc = go.GetComponent<BuildingUnderConstruction>();
				if (buc != null && buc.Def == _def)
					return go;
			}
			return null;
		}

		// ========================================
		// NAVIGATION
		// ========================================

		private void ReturnToBuildingList() {
			HandlerStack.Replace(new ActionMenuHandler(_category, _def));
		}

		private void OpenInfoPanel() {
			HandlerStack.Push(new BuildInfoHandler(_def));
		}

		// ========================================
		// PORT LAYOUT
		// ========================================

		private enum PortGroup { Gas, Liquid, Solid, Power, Logic, Radbolt }

		private void AnnouncePortLayout() {
			var orientation = BuildMenuData.GetCurrentOrientation();
			var ports = new List<(string label, CellOffset offset, PortGroup group)>();

			CollectConduitPorts(ports);
			CollectSecondaryConduitPorts(ports);
			CollectPowerPorts(ports);
			CollectLogicPorts(ports);
			CollectRadboltPorts(ports);

			if (ports.Count == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.BUILD_MENU.NO_PORTS);
				return;
			}

			// Reference point for offset descriptions: input end for
			// horizontal flow buildings, bottom-left otherwise
			CellOffset refPoint;
			if (BuildMenuData.IsHorizontalFlowBuilding(_def)) {
				refPoint = Rotatable.GetRotatedCellOffset(
					BuildMenuData.InputEndOffset(_def), orientation);
			} else {
				int minX = 0, minY = 0;
				foreach (var offset in _def.PlacementOffsets) {
					var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
					if (rotated.x < minX) minX = rotated.x;
					if (rotated.y < minY) minY = rotated.y;
				}
				refPoint = new CellOffset(minX, minY);
			}

			var groupStrings = new List<string>();
			foreach (var group in ports.Select(p => p.group).Distinct()) {
				var items = ports.Where(p => p.group == group);
				var formatted = items.Select(p =>
					FormatPort(p.label, Rotatable.GetRotatedCellOffset(p.offset, orientation), refPoint));
				groupStrings.Add(string.Join(", ", formatted));
			}

			SpeechPipeline.SpeakInterrupt(
				string.Join(". ", groupStrings) + ".");
		}

		private void CollectConduitPorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			if (_def.InputConduitType != ConduitType.None)
				ports.Add((
					BuildingSection.ConduitInputLabel(_def.InputConduitType),
					_def.UtilityInputOffset,
					ConduitTypeToGroup(_def.InputConduitType)));
			if (_def.OutputConduitType != ConduitType.None)
				ports.Add((
					BuildingSection.ConduitOutputLabel(_def.OutputConduitType),
					_def.UtilityOutputOffset,
					ConduitTypeToGroup(_def.OutputConduitType)));
		}

		private void CollectSecondaryConduitPorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			var go = _def.BuildingComplete;
			for (int ct = 1; ct <= 3; ct++) {
				var conduitType = (ConduitType)ct;
				var group = ConduitTypeToGroup(conduitType);

				int genericInputs = CountGenericSecondary<ISecondaryInput>(go, conduitType);
				int genericOutputs = CountGenericSecondary<ISecondaryOutput>(go, conduitType);
				// Account for primary port in numbering
				if (_def.InputConduitType == conduitType) genericInputs++;
				if (_def.OutputConduitType == conduitType) genericOutputs++;

				int inputOrd = _def.InputConduitType == conduitType ? 1 : 0;
				foreach (var sec in go.GetComponents<ISecondaryInput>()) {
					if (!sec.HasSecondaryConduitType(conduitType)) continue;
					var offset = sec.GetSecondaryConduitOffset(conduitType);
					string semantic = BuildingSection.SemanticInputLabel(sec, conduitType);
					if (semantic != null) {
						ports.Add((semantic, offset, group));
					} else {
						inputOrd++;
						string label = BuildingSection.ConduitInputLabel(conduitType);
						if (genericInputs > 1)
							label = string.Format(
								(string)STRINGS.ONIACCESS.GLANCE.NUMBERED_PORT,
								label, inputOrd);
						ports.Add((label, offset, group));
					}
				}

				int outputOrd = _def.OutputConduitType == conduitType ? 1 : 0;
				foreach (var sec in go.GetComponents<ISecondaryOutput>()) {
					if (!sec.HasSecondaryConduitType(conduitType)) continue;
					var offset = sec.GetSecondaryConduitOffset(conduitType);
					string semantic = BuildingSection.SemanticOutputLabel(sec, conduitType);
					if (semantic != null) {
						ports.Add((semantic, offset, group));
					} else {
						outputOrd++;
						string label = BuildingSection.ConduitOutputLabel(conduitType);
						if (genericOutputs > 1)
							label = string.Format(
								(string)STRINGS.ONIACCESS.GLANCE.NUMBERED_PORT,
								label, outputOrd);
						ports.Add((label, offset, group));
					}
				}
			}
		}

		private static int CountGenericSecondary<T>(
				UnityEngine.GameObject go, ConduitType conduitType) where T : class {
			int count = 0;
			foreach (var comp in go.GetComponents<T>()) {
				if (comp is ISecondaryInput input
					&& input.HasSecondaryConduitType(conduitType)
					&& BuildingSection.SemanticInputLabel(input, conduitType) == null)
					count++;
				else if (comp is ISecondaryOutput output
					&& output.HasSecondaryConduitType(conduitType)
					&& BuildingSection.SemanticOutputLabel(output, conduitType) == null)
					count++;
			}
			return count;
		}

		private void CollectPowerPorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			if (_def.RequiresPowerInput)
				ports.Add((
					(string)STRINGS.ONIACCESS.GLANCE.POWER_INPUT,
					_def.PowerInputOffset, PortGroup.Power));
			if (_def.RequiresPowerOutput)
				ports.Add((
					(string)STRINGS.ONIACCESS.GLANCE.POWER_OUTPUT,
					_def.PowerOutputOffset, PortGroup.Power));
		}

		private void CollectLogicPorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			if (_def.LogicInputPorts != null)
				foreach (var port in _def.LogicInputPorts)
					ports.Add((port.description, port.cellOffset, PortGroup.Logic));
			if (_def.LogicOutputPorts != null)
				foreach (var port in _def.LogicOutputPorts)
					ports.Add((port.description, port.cellOffset, PortGroup.Logic));
			CollectLogicGatePorts(ports);
		}

		private static readonly string[] _gateInputNames = {
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_INPUT_ONE_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_INPUT_TWO_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_INPUT_THREE_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_INPUT_FOUR_NAME,
		};

		private static readonly string[] _gateOutputNames = {
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_OUTPUT_ONE_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_OUTPUT_TWO_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_OUTPUT_THREE_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTI_OUTPUT_FOUR_NAME,
		};

		private static readonly string[] _gateControlNames = {
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTIPLEXER_CONTROL_ONE_NAME,
			(string)STRINGS.UI.LOGIC_PORTS.GATE_MULTIPLEXER_CONTROL_TWO_NAME,
		};

		private void CollectLogicGatePorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			var gate = _def.BuildingComplete.GetComponent<LogicGateBase>();
			if (gate == null) return;

			var inputs = gate.inputPortOffsets;
			if (inputs != null) {
				string singleName = (string)STRINGS.UI.LOGIC_PORTS.GATE_SINGLE_INPUT_ONE_NAME;
				for (int i = 0; i < inputs.Length; i++)
					ports.Add((
						inputs.Length == 1 ? singleName : _gateInputNames[i],
						inputs[i], PortGroup.Logic));
			}

			var outputs = gate.outputPortOffsets;
			if (outputs != null) {
				string singleName = (string)STRINGS.UI.LOGIC_PORTS.GATE_SINGLE_OUTPUT_ONE_NAME;
				for (int i = 0; i < outputs.Length; i++)
					ports.Add((
						outputs.Length == 1 ? singleName : _gateOutputNames[i],
						outputs[i], PortGroup.Logic));
			}

			var controls = gate.controlPortOffsets;
			if (controls != null)
				for (int i = 0; i < controls.Length; i++)
					ports.Add((_gateControlNames[i], controls[i], PortGroup.Logic));
		}

		private void CollectRadboltPorts(
				List<(string label, CellOffset offset, PortGroup group)> ports) {
			if (_def.UseHighEnergyParticleInputPort)
				ports.Add((
					(string)STRINGS.ONIACCESS.GLANCE.RADBOLT_INPUT,
					_def.HighEnergyParticleInputOffset, PortGroup.Radbolt));
			if (_def.UseHighEnergyParticleOutputPort)
				ports.Add((
					(string)STRINGS.ONIACCESS.GLANCE.RADBOLT_OUTPUT,
					_def.HighEnergyParticleOutputOffset, PortGroup.Radbolt));
		}

		private static string FormatPort(string label, CellOffset rotated, CellOffset bottomLeft) {
			string offsetDesc = FormatOffset(rotated, bottomLeft);
			return string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.PORT_AT,
				label, offsetDesc);
		}

		private static string FormatOffset(CellOffset offset, CellOffset bottomLeft) {
			var adjusted = new CellOffset(offset.x - bottomLeft.x, offset.y - bottomLeft.y);
			if (adjusted.x == 0 && adjusted.y == 0)
				return (string)STRINGS.ONIACCESS.SCANNER.HERE;

			var parts = new List<string>();
			if (adjusted.x < 0)
				parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_LEFT, -adjusted.x));
			else if (adjusted.x > 0)
				parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_RIGHT, adjusted.x));
			if (adjusted.y > 0)
				parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_UP, adjusted.y));
			else if (adjusted.y < 0)
				parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_DOWN, -adjusted.y));
			return string.Join(" ", parts);
		}

		private static PortGroup ConduitTypeToGroup(ConduitType type) {
			switch (type) {
				case ConduitType.Gas: return PortGroup.Gas;
				case ConduitType.Liquid: return PortGroup.Liquid;
				case ConduitType.Solid: return PortGroup.Solid;
				default: return PortGroup.Gas;
			}
		}

		// ========================================
		// PRIORITY
		// ========================================

		private void SetPriority(int value) {
			PrioritySetting setting;
			string announcement;
			if (value == 0) {
				setting = new PrioritySetting(PriorityScreen.PriorityClass.topPriority, 1);
				announcement = (string)STRINGS.ONIACCESS.TOOLS.PRIORITY_EMERGENCY;
			} else {
				setting = new PrioritySetting(PriorityScreen.PriorityClass.basic, value);
				announcement = string.Format((string)STRINGS.ONIACCESS.TOOLS.PRIORITY_BASIC, value);
			}

			PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel.PriorityScreen
			.SetScreenPriority(setting, false);
			PriorityScreen.PlayPriorityConfirmSound(setting);
			SpeechPipeline.SpeakInterrupt(announcement);
		}

		// ========================================
		// CLOSE AND CLEANUP
		// ========================================

		private void CloseEverything() => LeaveBuildMode(announceCancel: true);
		private void ExitBuildMode() => LeaveBuildMode(announceCancel: false);

		private void LeaveBuildMode(bool announceCancel) {
			if (Game.Instance != null)
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);

			QueueOverlayAndPop();

			try {
				SelectTool.Instance.Activate();
			} catch (Exception ex) {
				Util.Log.Error($"BuildToolHandler.LeaveBuildMode: {ex}");
			}

			if (announceCancel)
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.CANCELED);
			PlayDeactivateSound();
		}

		private void QueueOverlayAndPop() {
			for (int i = HandlerStack.Count - 1; i >= 0; i--) {
				if (HandlerStack.GetAt(i) is TileCursorHandler tch) {
					tch.QueueNextOverlayAnnouncement();
					break;
				}
			}
			HandlerStack.Pop();
		}

		// ========================================
		// SOUNDS
		// ========================================

		private bool IsValidDragTarget(int cell) {
			int col = Grid.CellColumn(cell);
			int row = Grid.CellRow(cell);
			int startCol = Grid.CellColumn(_utilityStartCell);
			int startRow = Grid.CellRow(_utilityStartCell);
			if (col != startCol && row != startRow) return false;
			var path = BuildLinePath(_utilityStartCell, cell);
			return ValidateUtilityPath(path);
		}

		private static void PlayDragSound(int tileCount) =>
			RectangleSelection.PlayDragSound("Tile_Drag", tileCount);

		private static void PlayDeactivateSound() {
			try {
				KFMOD.PlayUISound(GlobalAssets.GetSound("Tile_Cancel"));
			} catch (Exception ex) {
				Util.Log.Warn($"BuildToolHandler.PlayDeactivateSound: {ex}");
			}
		}


		private static void PlayCancelSound() {
			try {
				KFMOD.PlayUISound(GlobalAssets.GetSound("Tile_Confirm_NegativeTool"));
			} catch (Exception ex) {
				Util.Log.Warn($"BuildToolHandler.PlayCancelSound: {ex}");
			}
		}
	}
}
