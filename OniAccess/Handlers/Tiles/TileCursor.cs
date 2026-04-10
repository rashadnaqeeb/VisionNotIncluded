using System;
using System.Collections.Generic;
using OniAccess.Handlers.Tiles.ToolProfiles;
using ProcGen;
using UnityEngine;

namespace OniAccess.Handlers.Tiles {
	public enum Direction { Up, Down, Left, Right }

	public enum CoordinateMode { Off, Append, Prepend }

	/// <summary>
	/// Owns a cell index for tile-by-tile world navigation.
	/// Arrow key movement, world bounds clamping, KInputManager mouse lock,
	/// camera follow, coordinate reading. Speech content is delegated to
	/// GlanceComposer (single cell) or IAreaScanner (big cursor).
	/// </summary>
	public class TileCursor {
		public static TileCursor Instance { get; private set; }

		private static readonly int[] RadiusSteps = { 0, 1, 2, 4, 10 };
		private int _radiusStepIndex;
		private int _cell;
		private int _lastWorldId = -1;
		private bool _wasPanning;
		private bool _wasTimelapsing;
		private string _lastRoomName;
		private string _lastBiomeName;
		private readonly Scanner.Routing.BiomeNameResolver _biomeResolver = new Scanner.Routing.BiomeNameResolver();
		private readonly Overlays.OverlayProfileRegistry _registry;
		private readonly TileDetailsComposer _detailsComposer;

		private static bool IsTimelapsing =>
			Game.Instance?.timelapser?.CapturingTimelapseScreenshot == true;

		public ToolProfile ActiveToolProfile { get; set; }

		public int Radius => RadiusSteps[_radiusStepIndex];
		public int CursorSize => Radius * 2 + 1;

		public TileCursor(Overlays.OverlayProfileRegistry registry) {
			_registry = registry;
			_detailsComposer = new TileDetailsComposer(_biomeResolver);
		}

		public static TileCursor Create(Overlays.OverlayProfileRegistry registry) {
			Instance = new TileCursor(registry);
			return Instance;
		}

		public static void Destroy() {
			Instance = null;
			KInputManager.isMousePosLocked = false;
		}

		public CoordinateMode Mode { get; internal set; } = ConfigManager.Config.CoordinateMode;

		public int Cell => _cell;

		/// <summary>
		/// Initialize cursor at the Printing Pod on the active world.
		/// Falls back to world center if no telepad exists.
		/// </summary>
		public void Initialize() {
			_lastWorldId = ClusterManager.Instance.activeWorldId;
			_cell = Util.GridCoordinates.GetOriginCell();
			LockMouseToCell(_cell);
			SnapCameraToCell(_cell);
			UpdateBiomeTracking();
		}

		/// <summary>
		/// Detect world switch and re-initialize cursor for the new world.
		/// Called every tick from TileCursorHandler. Returns the world name
		/// speech if a switch occurred, null otherwise.
		/// </summary>
		public string CheckWorldSwitch() {
			if (IsTimelapsing) return null;
			int worldId = ClusterManager.Instance.activeWorldId;
			if (worldId == _lastWorldId) return null;
			_lastWorldId = worldId;
			Util.GridCoordinates.ClearOrigin();
			_cell = Util.GridCoordinates.GetOriginCell();
			_lastRoomName = null;
			_lastBiomeName = null;
			_wasPanning = false;
			LockMouseToCell(_cell);
			UpdateBiomeTracking();
			return BuildCellSpeech();
		}

		// ========================================
		// RADIUS
		// ========================================

		/// <summary>
		/// Step to the next larger radius. Returns the size announcement
		/// (e.g. "3x3") or null if already at maximum.
		/// </summary>
		public string IncreaseRadius() {
			if (_radiusStepIndex >= RadiusSteps.Length - 1) return null;
			_radiusStepIndex++;
			return string.Format(STRINGS.ONIACCESS.BIG_CURSOR.SIZE_FORMAT, CursorSize);
		}

		/// <summary>
		/// Step to the next smaller radius. Returns the size announcement
		/// (e.g. "1x1") or null if already at minimum.
		/// </summary>
		public string DecreaseRadius() {
			if (_radiusStepIndex <= 0) return null;
			_radiusStepIndex--;
			return string.Format(STRINGS.ONIACCESS.BIG_CURSOR.SIZE_FORMAT, CursorSize);
		}

		public string ResetRadius() {
			if (_radiusStepIndex == 0) return null;
			_radiusStepIndex = 0;
			return string.Format(STRINGS.ONIACCESS.BIG_CURSOR.SIZE_FORMAT, CursorSize);
		}

		/// <summary>
		/// Returns the two corner cells of the cursor area for rectangle selection.
		/// When Radius == 0, both corners are the current cell.
		/// Corners are clamped to world bounds.
		/// </summary>
		public (int corner1, int corner2) GetAreaCorners() {
			if (Radius == 0) return (_cell, _cell);
			int cx = Grid.CellColumn(_cell);
			int cy = Grid.CellRow(_cell);
			var world = ClusterManager.Instance.activeWorld;
			int minX = Math.Max(cx - Radius, (int)world.minimumBounds.x);
			int maxX = Math.Min(cx + Radius, (int)world.maximumBounds.x);
			int minY = Math.Max(cy - Radius, (int)world.minimumBounds.y);
			int maxY = Math.Min(cy + Radius, (int)world.maximumBounds.y);
			return (Grid.XYToCell(minX, minY), Grid.XYToCell(maxX, maxY));
		}

		// ========================================
		// MOVEMENT
		// ========================================

		/// <summary>
		/// Move in the given direction. Steps by CursorSize when Radius > 0.
		/// Returns the speech string for the new position, or null if blocked.
		/// </summary>
		public string Move(Direction direction) {
			if (Radius == 0) {
				int candidate = GetNeighbor(_cell, direction);
				if (candidate == Grid.InvalidCell || !IsInWorldBounds(candidate)) {
					PlayBoundarySound();
					return null;
				}
				_cell = candidate;
			} else {
				int cx = Grid.CellColumn(_cell);
				int cy = Grid.CellRow(_cell);
				int step = CursorSize;
				int targetX = cx, targetY = cy;
				switch (direction) {
					case Direction.Up: targetY = cy + step; break;
					case Direction.Down: targetY = cy - step; break;
					case Direction.Left: targetX = cx - step; break;
					case Direction.Right: targetX = cx + step; break;
				}
				var world = ClusterManager.Instance.activeWorld;
				int minX = (int)world.minimumBounds.x + Radius;
				int maxX = (int)world.maximumBounds.x - Radius;
				int minY = (int)world.minimumBounds.y + Radius;
				int maxY = (int)world.maximumBounds.y - Radius;
				targetX = Math.Max(minX, Math.Min(maxX, targetX));
				targetY = Math.Max(minY, Math.Min(maxY, targetY));
				if (targetX == cx && targetY == cy) {
					PlayBoundarySound();
					return null;
				}
				_cell = Grid.XYToCell(targetX, targetY);
			}
			LockMouseToCell(_cell);
			SnapCameraToCell(_cell);
			return BuildCellSpeech(announceBiome: true);
		}

		/// <summary>
		/// Return coordinates for the current cell, relative to the cell
		/// below the Printing Pod (0,0). Falls back to world center.
		/// </summary>
		public string ReadCoordinates() {
			return Util.GridCoordinates.Format(_cell);
		}

		public string ReadTileDetails() {
			if (!Grid.IsVisible(_cell))
				return AttachCoordinates((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
			string details = _detailsComposer.Compose(_cell);
			if (details == null) return null;
			return AttachCoordinates(details);
		}

		/// <summary>
		/// Cycle Off -> Append -> Prepend -> Off.
		/// Returns the spoken name of the new mode.
		/// </summary>
		public string CycleMode() {
			string spoken;
			switch (Mode) {
				case CoordinateMode.Off:
					Mode = CoordinateMode.Append;
					spoken = (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_APPEND;
					break;
				case CoordinateMode.Append:
					Mode = CoordinateMode.Prepend;
					spoken = (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_PREPEND;
					break;
				default:
					Mode = CoordinateMode.Off;
					spoken = (string)STRINGS.ONIACCESS.TILE_CURSOR.COORD_OFF;
					break;
			}
			ConfigManager.Config.CoordinateMode = Mode;
			ConfigManager.Save();
			return spoken;
		}

		/// <summary>
		/// Re-sync cursor to the camera's center cell. Called every frame
		/// so the cursor follows game-initiated camera movement (alerts,
		/// follow-cam, etc.) and the mouse lock stays correct.
		/// Returns tile speech when the camera finishes a pan, null otherwise.
		/// </summary>
		public string SyncToCamera() {
			if (IsTimelapsing) {
				_wasTimelapsing = true;
				return null;
			}
			if (_wasTimelapsing) {
				_wasTimelapsing = false;
				SnapCameraToCell(_cell);
				LockMouseToCell(_cell);
				return null;
			}
			if (Camera.main == null) return null;
			var ctrl = CameraController.Instance;
			bool gameMoving = ctrl != null
				&& (ctrl.isTargetPosSet || ctrl.followTarget != null);
			if (gameMoving) {
				Vector3 center = Camera.main.transform.position;
				int cell = Grid.PosToCell(center);
				if (Grid.IsValidCell(cell) && cell != _cell && IsInWorldBounds(cell)) {
					_cell = cell;
					UpdateBiomeTracking();
				}
			}
			LockMouseToCell(_cell);

			bool panning = ctrl != null && ctrl.isTargetPosSet;
			if (_wasPanning && !panning) {
				_wasPanning = false;
				return BuildCellSpeech();
			}
			_wasPanning = panning;
			return null;
		}

		public string JumpTo(int cell) {
			if (!IsInWorldBounds(cell)) return null;
			_cell = cell;
			LockMouseToCell(_cell);
			SnapCameraToCell(_cell);
			var speech = BuildCellSpeech();
			UpdateBiomeTracking();
			return speech;
		}

		// ========================================
		// PRIVATE
		// ========================================

		private string BuildCellSpeech(bool announceBiome = false) {
			if (Radius > 0) {
				HashedString scanMode = OverlayScreen.Instance != null
					? OverlayScreen.Instance.GetMode()
					: OverlayModes.None.ID;
				var scanner = _registry.GetAreaScanner(scanMode);
				int totalCells = CursorSize * CursorSize;
				int[] cells = CollectVisibleAreaCells(out int unexploredCount);
				string content = scanner.Scan(cells, totalCells, unexploredCount);
				return AttachCoordinates(content);
			}

			if (!Grid.IsVisible(_cell))
				return AttachCoordinates((string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);

			var overlayScreen = OverlayScreen.Instance;
			HashedString mode = overlayScreen != null ? overlayScreen.GetMode() : OverlayModes.None.ID;
			GlanceComposer composer = _registry.GetComposer(mode);

			var profile = ActiveToolProfile;
			if (profile != null) {
				if (profile.IsOverride)
					composer = profile.Composer;
				else
					composer = composer.WithPrepended(profile.PrependSections);
			}

			string content2 = composer.Compose(_cell);
			if (content2 == null) {
				var element = Grid.Element[_cell];
				content2 = element.IsVacuum
					? element.name
					: string.Format(STRINGS.ONIACCESS.GLANCE.ELEMENT_MASS,
						element.name, Sections.ElementSection.FormatGlanceMass(Grid.Mass[_cell]));
			}

			if (mode == OverlayModes.Rooms.ID)
				content2 = PrependRoomName(content2);

			if (announceBiome)
				content2 = PrependBiomeName(content2);

			return AttachCoordinates(content2);
		}

		/// <summary>
		/// Collects visible cells in the cursor area. Cells outside world
		/// bounds or not yet explored are excluded from the returned array.
		/// </summary>
		private int[] CollectVisibleAreaCells(out int unexploredCount) {
			int cx = Grid.CellColumn(_cell);
			int cy = Grid.CellRow(_cell);
			var list = new List<int>(CursorSize * CursorSize);
			unexploredCount = 0;
			for (int dy = -Radius; dy <= Radius; dy++) {
				for (int dx = -Radius; dx <= Radius; dx++) {
					int cell = Grid.XYToCell(cx + dx, cy + dy);
					if (!IsInWorldBounds(cell)) continue;
					if (!Grid.IsVisible(cell)) {
						unexploredCount++;
						continue;
					}
					list.Add(cell);
				}
			}
			return list.ToArray();
		}

		internal static int GetNeighbor(int cell, Direction direction) {
			switch (direction) {
				case Direction.Up: return Grid.CellAbove(cell);
				case Direction.Down: return Grid.CellBelow(cell);
				case Direction.Left: return Grid.CellLeft(cell);
				case Direction.Right: return Grid.CellRight(cell);
				default: return Grid.InvalidCell;
			}
		}

		internal static bool IsInWorldBounds(int cell) {
			if (!Grid.IsValidCell(cell))
				return false;
			var world = ClusterManager.Instance.activeWorld;
			if (Grid.WorldIdx[cell] != world.id)
				return false;
			int x = Grid.CellColumn(cell);
			int y = Grid.CellRow(cell);
			return x >= (int)world.minimumBounds.x
				&& x <= (int)world.maximumBounds.x
				&& y >= (int)world.minimumBounds.y
				&& y <= (int)world.maximumBounds.y;
		}

		/// <summary>
		/// Check if the cell is at the edge of the active world in the given
		/// direction. Uses coordinate comparison against world bounds directly,
		/// independent of Grid.CellAbove/Below/Left/Right and WorldIdx.
		/// </summary>
		internal static bool IsAtWorldEdge(int cell, Direction direction) {
			var world = ClusterManager.Instance.activeWorld;
			int x = Grid.CellColumn(cell);
			int y = Grid.CellRow(cell);
			switch (direction) {
				case Direction.Up: return y >= (int)world.maximumBounds.y;
				case Direction.Down: return y <= (int)world.minimumBounds.y;
				case Direction.Left: return x <= (int)world.minimumBounds.x;
				case Direction.Right: return x >= (int)world.maximumBounds.x;
				default: return false;
			}
		}

		private static void LockMouseToCell(int cell) {
			if (Camera.main == null) {
				Util.Log.Warn("TileCursor.LockMouseToCell: Camera.main is null");
				return;
			}
			Vector3 worldPos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
			Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
			KInputManager.isMousePosLocked = true;
			KInputManager.lockedMousePos = screenPos;
		}

		private static void SnapCameraToCell(int cell) {
			if (IsTimelapsing) return;
			if (CameraController.Instance == null) {
				Util.Log.Warn("TileCursor.SnapCameraToCell: CameraController.Instance is null");
				return;
			}
			Vector3 worldPos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
			if (ConfigManager.Config.LockZoom)
				CameraController.Instance.SnapTo(worldPos, 10f);
			else
				CameraController.Instance.SnapTo(worldPos);
		}

		private string AttachCoordinates(string content) {
			switch (Mode) {
				case CoordinateMode.Append:
					return content + ", " + ReadCoordinates();
				case CoordinateMode.Prepend:
					return ReadCoordinates() + ", " + content;
				default:
					return content;
			}
		}

		public void ResetRoomName() {
			_lastRoomName = null;
		}

		private string PrependRoomName(string content) {
			var cavity = Game.Instance.roomProber.GetCavityForCell(_cell);
			string roomName = cavity?.room != null
				? cavity.room.roomType.Name
				: (string)STRINGS.ONIACCESS.TILE_CURSOR.NO_ROOM;
			if (roomName == _lastRoomName)
				return content;
			_lastRoomName = roomName;
			return roomName + ", " + content;
		}

		private void UpdateBiomeTracking() {
			if (World.Instance?.zoneRenderData == null) return;
			var zoneType = World.Instance.zoneRenderData.GetSubWorldZoneType(_cell);
			_lastBiomeName = _biomeResolver.GetName(zoneType);
		}

		private string PrependBiomeName(string content) {
			if (!ConfigManager.Config.AnnounceBiomeChanges) return content;
			if (World.Instance?.zoneRenderData == null) return content;
			var zoneType = World.Instance.zoneRenderData.GetSubWorldZoneType(_cell);
			string biomeName = _biomeResolver.GetName(zoneType);
			if (biomeName == _lastBiomeName)
				return content;
			_lastBiomeName = biomeName;
			return biomeName + ", " + content;
		}

		private static void PlayBoundarySound() {
			BaseScreenHandler.PlaySound("Negative");
		}
	}
}
