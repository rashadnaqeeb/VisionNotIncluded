using System;
using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Handlers.Tiles;
using OniAccess.Handlers.Tiles.ToolProfiles;
using OniAccess.Handlers.Tiles.ToolProfiles.Sections;
using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Sandbox {
	/// <summary>
	/// Non-modal handler for sandbox tool mode. Sits on top of
	/// TileCursorHandler, intercepts Space, Enter, Escape, F, Ctrl+Space.
	///
	/// Rectangle-mode tools (8 BrushTool descendants) use two-press
	/// corner selection via RectangleSelection. Single-cell tools
	/// (Flood, Spawner, StoryTrait) apply at cursor on Enter.
	///
	/// Submission for brush tools populates the tool's cellsInRadius
	/// with rectangle cells and calls Paint() via reflection.
	/// Single-cell tools receive OnLeftClickDown at the cursor position.
	/// </summary>
	public class SandboxToolHandler: BaseScreenHandler {
		public static SandboxToolHandler Instance { get; private set; }

		internal readonly RectangleSelection Selection = new RectangleSelection();
		private bool _skipFirstTick;
		private bool _isRectangleTool;
		private ToolProfile _floodProfile;

		private static readonly IReadOnlyList<ICellSection> FloodSections =
			new List<ICellSection> { new FloodFillSection() }.AsReadOnly();

		private static readonly HashSet<Type> RectangleToolTypes = new HashSet<Type> {
			typeof(SandboxBrushTool),
			typeof(SandboxSprinkleTool),
			typeof(SandboxHeatTool),
			typeof(SandboxStressTool),
			typeof(SandboxClearFloorTool),
			typeof(SandboxDestroyerTool),
			typeof(SandboxFOWTool),
			typeof(SandboxCritterTool),
		};

		private static readonly HashSet<Type> SingleCellToolTypes = new HashSet<Type> {
			typeof(SandboxFloodTool),
			typeof(SandboxSpawnerTool),
			typeof(SandboxStoryTraitTool),
		};

		internal static readonly HashSet<Type> AllSandboxToolTypes;

		static SandboxToolHandler() {
			AllSandboxToolTypes = new HashSet<Type>(RectangleToolTypes);
			AllSandboxToolTypes.UnionWith(SingleCellToolTypes);
			AllSandboxToolTypes.Add(typeof(SandboxSampleTool));
		}

		internal static bool IsSandboxTool(InterfaceTool tool) {
			return tool != null && AllSandboxToolTypes.Contains(tool.GetType());
		}

		private static bool AllowsUnexplored(InterfaceTool tool) => tool is SandboxFOWTool;

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.Space, Modifier.Shift),
			new ConsumedKey(KKeyCode.Space, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.Return),
			new ConsumedKey(KKeyCode.F),
		};
		public override IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		public override string DisplayName => GetToolLabel();
		public override bool CapturesAllInput => false;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Space", (string)STRINGS.ONIACCESS.SANDBOX.HELP.SET_CORNER),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.SANDBOX.HELP.CONFIRM),
			new HelpEntry("Escape", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CANCEL_TOOL),
			new HelpEntry("F", (string)STRINGS.ONIACCESS.SANDBOX.HELP.OPEN_PARAMS),
			new HelpEntry("Ctrl+Space", (string)STRINGS.ONIACCESS.SANDBOX.HELP.SAMPLE),
			new HelpEntry("Shift+Space", (string)STRINGS.ONIACCESS.HELP.TOOLS_HELP.CLEAR_RECT),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			Instance = this;

			var activeTool = PlayerController.Instance?.ActiveTool;
			_isRectangleTool = activeTool != null && RectangleToolTypes.Contains(activeTool.GetType());

			if (Game.Instance != null) {
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);
				Game.Instance.Subscribe(1174281782, OnActiveToolChanged);
			}

			_skipFirstTick = true;

			// The Fill tool replaces a whole connected region of one element. Its
			// extent ends every cursor readout and the activation announcement,
			// the same slot the build extent takes for multi-cell buildings.
			if (activeTool is SandboxFloodTool flood) {
				_floodProfile = ToolProfile.Appending(DisplayName, FloodSections);
				TileCursor.Instance.ActiveToolProfile = _floodProfile;
				int cell = TileCursor.Instance.Cell;
				if (Grid.IsVisible(cell)) {
					SpeechPipeline.SpeakInterrupt(DisplayName + ", " + FloodFillSection.Describe(flood, cell));
					return;
				}
			}
			SpeechPipeline.SpeakInterrupt(DisplayName);
		}

		public override void OnDeactivate() {
			Instance = null;

			if (Game.Instance != null)
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);

			if (_floodProfile != null && TileCursor.Instance != null
				&& TileCursor.Instance.ActiveToolProfile == _floodProfile)
				TileCursor.Instance.ActiveToolProfile = null;
			_floodProfile = null;

			Selection.ClearAll();
		}

		private void OnActiveToolChanged(object data) {
			if (data is SelectTool) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.CANCELED);
				PlayDeactivateSound();
				HandlerStack.Pop();
			}
		}

		// ========================================
		// KEY HANDLING
		// ========================================

		public override bool Tick() {
			if (_skipFirstTick) {
				_skipFirstTick = false;
				return false;
			}

			// Drag sound while pending corner
			if (_isRectangleTool && Selection.PendingFirstCorner != Grid.InvalidCell) {
				int cell = TileCursor.Instance.Cell;
				if (cell != Selection.LastDragCell) {
					Selection.LastDragCell = cell;
					int tileCount = RectangleSelection.TileCountBetween(
						Selection.PendingFirstCorner, cell);
					RectangleSelection.PlayDragSound("Tile_Drag", tileCount);
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) {
				if (InputUtil.CtrlOptionHeld() && !InputUtil.ShiftHeld() && !InputUtil.AltHeld()) {
					SampleAtCursor();
					return true;
				}
				if (InputUtil.ShiftHeld() && !InputUtil.AnyCtrlHeld()) {
					ClearRectAtCursor();
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					if (_isRectangleTool)
						SetCorner();
					else
						PlaceAtCursor();
					return true;
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
				&& !InputUtil.AnyModifierHeld()) {
				ConfirmAndExit();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F)
				&& !InputUtil.AnyModifierHeld()) {
				OpenParamMenu();
				return true;
			}

			return false;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.CANCELED);
				PlayDeactivateSound();
				DeactivateToolAndPop();
				return true;
			}
			return false;
		}

		// ========================================
		// RECTANGLE SELECTION
		// ========================================

		private void SetCorner() {
			var activeTool = PlayerController.Instance?.ActiveTool;
			bool allowUnexplored = AllowsUnexplored(activeTool);
			int cell = TileCursor.Instance.Cell;

			if (!allowUnexplored && !Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			// Big cursor: set both corners at once
			if (TileCursor.Instance.Radius > 0) {
				var (c1, c2) = TileCursor.Instance.GetAreaCorners();
				Selection.AddRectangle(c1, c2);
				int area = RectangleSelection.ComputeArea(c1, c2);
				RectangleSelection.PlayDragSound("Tile_Drag", area);
				SpeechPipeline.SpeakInterrupt(
					RectangleSelection.BuildRectSummary(c1, c2, null, allowUnexplored));
				return;
			}

			var result = Selection.SetCorner(cell, out var rect);
			if (result == RectangleSelection.SetCornerResult.FirstCornerSet) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.CORNER_SET);
				RectangleSelection.PlayDragSound("Tile_Drag", 1);
			} else {
				int area = RectangleSelection.ComputeArea(rect.Cell1, rect.Cell2);
				RectangleSelection.PlayDragSound("Tile_Drag", area);
				SpeechPipeline.SpeakInterrupt(
					RectangleSelection.BuildRectSummary(rect.Cell1, rect.Cell2, null, allowUnexplored));
			}
		}

		/// <summary>
		/// First corner of a rectangle still being dragged out, or
		/// Grid.InvalidCell when no rectangle is in progress.
		/// </summary>
		public int PendingRectCorner =>
			_isRectangleTool ? Selection.PendingFirstCorner : Grid.InvalidCell;

		private void ClearRectAtCursor() {
			if (!_isRectangleTool) return;
			int cell = TileCursor.Instance.Cell;
			if (Selection.ClearRectAtCursor(cell))
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.RECT_CLEARED);
		}

		// ========================================
		// SUBMIT
		// ========================================

		private void ConfirmAndExit() {
			var activeTool = PlayerController.Instance?.ActiveTool;
			if (activeTool == null) return;

			int cell = TileCursor.Instance.Cell;
			if (!AllowsUnexplored(activeTool) && !Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			if (_isRectangleTool) {
				SubmitRectangleTool(activeTool, cell);
			} else {
				SubmitSingleCellTool(activeTool, cell);
			}
		}

		private void SubmitRectangleTool(InterfaceTool activeTool, int cursorCell) {
			// If no selection, auto-select current cell
			if (!Selection.HasSelection) {
				if (TileCursor.Instance.Radius > 0) {
					var (c1, c2) = TileCursor.Instance.GetAreaCorners();
					Selection.AddRectangle(c1, c2);
				} else {
					Selection.AutoSelectSingle(cursorCell);
				}
			}

			bool allowUnexplored = AllowsUnexplored(activeTool);

			// Collect all cells from rectangles
			var cells = new HashSet<int>();
			foreach (var r in Selection.GetRectangles()) {
				r.GetBounds(out int minX, out int maxX, out int minY, out int maxY);
				for (int y = minY; y <= maxY; y++)
					for (int x = minX; x <= maxX; x++) {
						int cell = Grid.XYToCell(x, y);
						if (Grid.IsValidCell(cell) && (allowUnexplored || Grid.IsVisible(cell)))
							cells.Add(cell);
					}
			}

			if (cells.Count == 0) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLS.NO_VALID_CELLS);
				PlaySound("Negative");
				DeactivateToolAndPop();
				return;
			}

			SubmitBrushCells(activeTool as BrushTool, cells);

			string announcement = cells.Count == 1
				? (string)STRINGS.ONIACCESS.SANDBOX.APPLIED_ONE
				: string.Format((string)STRINGS.ONIACCESS.SANDBOX.APPLIED, cells.Count);
			SpeechPipeline.SpeakInterrupt(announcement);
			DeactivateToolAndPop();
		}

		private void SubmitSingleCellTool(InterfaceTool activeTool, int cell) {
			ApplySingleCell(activeTool, cell);
			DeactivateToolAndPop();
		}

		private void PlaceAtCursor() {
			var activeTool = PlayerController.Instance?.ActiveTool;
			if (activeTool == null) return;

			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}

			ApplySingleCell(activeTool, cell);
		}

		private static void ApplySingleCell(InterfaceTool tool, int cell) {
			var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);

			if (tool is SandboxStoryTraitTool storyTool) {
				string error = storyTool.GetError(pos, out _, out _);
				if (error != null) {
					PlaySound("Negative");
					SpeechPipeline.SpeakInterrupt(TextFilter.FilterForSpeech(error));
					return;
				}
			}

			// FillCount also syncs the flood tool's mouse cell to the cursor, so
			// the fill starts where the announced extent was computed.
			int count = tool is SandboxFloodTool flood ? FloodFillSection.FillCount(flood, cell) : 1;
			try {
				tool.OnLeftClickDown(pos);
			} catch (Exception ex) {
				Util.Log.Error($"SandboxToolHandler.ApplySingleCell: {ex}");
			}
			SpeechPipeline.SpeakInterrupt(count == 1
				? (string)STRINGS.ONIACCESS.SANDBOX.APPLIED_ONE
				: string.Format((string)STRINGS.ONIACCESS.SANDBOX.APPLIED, count));
		}

		/// <summary>
		/// Submit cells to a BrushTool by populating cellsInRadius and
		/// calling Paint() via reflection. Clears visitedCells first.
		/// </summary>
		private static System.Reflection.FieldInfo _cellsInRadiusField;
		private static System.Reflection.FieldInfo _visitedCellsField;
		private static System.Reflection.FieldInfo _currentCellField;
		private static System.Reflection.MethodInfo _paintMethod;

		private static void SubmitBrushCells(BrushTool brushTool, HashSet<int> cells) {
			if (brushTool == null) {
				Util.Log.Error("SandboxToolHandler.SubmitBrushCells: activeTool is not a BrushTool");
				return;
			}

			try {
				if (_cellsInRadiusField == null)
					_cellsInRadiusField = AccessTools.Field(typeof(BrushTool), "cellsInRadius");
				if (_visitedCellsField == null)
					_visitedCellsField = AccessTools.Field(typeof(BrushTool), "visitedCells");
				if (_currentCellField == null)
					_currentCellField = AccessTools.Field(typeof(BrushTool), "currentCell");
				if (_paintMethod == null)
					_paintMethod = AccessTools.Method(typeof(BrushTool), "Paint");

				var cellsInRadius = (HashSet<int>)_cellsInRadiusField.GetValue(brushTool);
				var visitedCells = (List<int>)_visitedCellsField.GetValue(brushTool);

				visitedCells.Clear();
				cellsInRadius.Clear();
				foreach (int cell in cells)
					cellsInRadius.Add(cell);

				// Set currentCell to first cell for distance calculations in OnPaintCell
				var enumerator = cells.GetEnumerator();
				if (enumerator.MoveNext())
					_currentCellField.SetValue(brushTool, enumerator.Current);

				_paintMethod.Invoke(brushTool, null);
			} catch (Exception ex) {
				Util.Log.Error($"SandboxToolHandler.SubmitBrushCells: {ex}");
			}
		}

		// ========================================
		// SAMPLE
		// ========================================

		private static void SampleAtCursor() {
			if (SandboxToolParameterMenu.instance == null) return;
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}
			SandboxSampleTool.Sample(cell);
			SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.SANDBOX.SAMPLE);
		}

		// ========================================
		// PARAMETER MENU
		// ========================================

		private static void OpenParamMenu() {
			if (SandboxToolParameterMenu.instance == null) return;
			HandlerStack.Push(new SandboxParamMenuHandler());
		}

		// ========================================
		// HELPERS
		// ========================================

		private string GetToolLabel() {
			var tool = PlayerController.Instance?.ActiveTool;
			if (tool == null) return (string)STRINGS.ONIACCESS.SANDBOX.TOOL_FALLBACK;
			try {
				foreach (var collection in ToolMenu.Instance.sandboxTools)
					foreach (var ti in collection.tools)
						if (ti.toolName == tool.GetType().Name)
							return ti.text;
			} catch (Exception ex) {
				Util.Log.Warn($"SandboxToolHandler.GetToolLabel: {ex.Message}");
			}
			return (string)STRINGS.ONIACCESS.SANDBOX.TOOL_FALLBACK;
		}

		private void DeactivateToolAndPop() {
			Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);
			for (int i = HandlerStack.Count - 1; i >= 0; i--) {
				if (HandlerStack.GetAt(i) is TileCursorHandler tch) {
					tch.QueueNextOverlayAnnouncement();
					break;
				}
			}
			ToolMenu.Instance.ClearSelection();
			SelectTool.Instance.Activate();
			HandlerStack.Pop();
		}

		private static void PlayDeactivateSound() {
			try {
				KFMOD.PlayUISound(GlobalAssets.GetSound("Tile_Cancel"));
			} catch (Exception ex) {
				Util.Log.Warn($"SandboxToolHandler.PlayDeactivateSound: {ex}");
			}
		}
	}
}
