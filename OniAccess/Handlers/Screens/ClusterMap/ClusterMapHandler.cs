using System.Collections.Generic;
using OniAccess.Handlers.Tiles;
using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.ClusterMap {
	/// <summary>
	/// Handler for the Spaced Out DLC ClusterMapScreen (hex-grid starmap).
	/// Provides hex cursor navigation, entity selection, scanner, pathfinding,
	/// and coordinate reading for blind players.
	///
	/// CapturesAllInput = true. TileCursorHandler stays on the stack but
	/// receives no input while this handler is active.
	///
	/// Lifecycle: Show-patched on ClusterMapScreen.OnShow(bool).
	/// </summary>
	public class ClusterMapHandler: BaseScreenHandler {
		private AxialI _cursorLocation;
		private readonly ClusterScanNavigator _scanner = new ClusterScanNavigator();
		private AxialI _pathStart;
		private bool _hasPathStart;
		private bool _activated;

		public ClusterMapHandler(KScreen screen) : base(screen) {
		}

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.STARMAP.HANDLER_NAME;

		public override bool CapturesAllInput => true;

		private static readonly ConsumedKey[] _consumedKeys = {
			// Hex directions
			new ConsumedKey(KKeyCode.U),
			new ConsumedKey(KKeyCode.O),
			new ConsumedKey(KKeyCode.J),
			new ConsumedKey(KKeyCode.L),
			new ConsumedKey(KKeyCode.N),
			new ConsumedKey(KKeyCode.Period),
			// Arrow keys
			new ConsumedKey(KKeyCode.UpArrow),
			new ConsumedKey(KKeyCode.DownArrow),
			new ConsumedKey(KKeyCode.LeftArrow),
			new ConsumedKey(KKeyCode.RightArrow),
			// Jump home
			new ConsumedKey(KKeyCode.H),
			// Entity selection
			new ConsumedKey(KKeyCode.Return),
			new ConsumedKey(KKeyCode.Return, Modifier.Ctrl),
			// Coordinates and tooltip
			new ConsumedKey(KKeyCode.K),
			new ConsumedKey(KKeyCode.I),
			// Pathfinder
			new ConsumedKey(KKeyCode.Space),
			new ConsumedKey(KKeyCode.D),
			// Scanner keys
			new ConsumedKey(KKeyCode.End),
			new ConsumedKey(KKeyCode.Home),
			new ConsumedKey(KKeyCode.Backspace),
			new ConsumedKey(KKeyCode.End, Modifier.Shift),
			new ConsumedKey(KKeyCode.Home, Modifier.Shift),
			new ConsumedKey(KKeyCode.PageUp, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.PageDown, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.PageUp),
			new ConsumedKey(KKeyCode.PageDown),
			new ConsumedKey(KKeyCode.PageUp, Modifier.Alt),
			new ConsumedKey(KKeyCode.PageDown, Modifier.Alt),
			new ConsumedKey(KKeyCode.F, Modifier.Ctrl),
		};

		public override IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("U/O/J/L/N/.", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.HEX_MOVE),
			new HelpEntry("Arrow keys", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.HEX_MOVE_ARROWS),
			new HelpEntry("K", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.READ_COORDS),
			new HelpEntry("I", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.READ_TOOLTIP),
			new HelpEntry("H", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.JUMP_HOME),
			new HelpEntry("Enter", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.SELECT_ENTITY),
			new HelpEntry("Ctrl+Enter", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.SWITCH_WORLD),
			new HelpEntry("Space", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.PATHFIND_START),
			new HelpEntry("D", STRINGS.ONIACCESS.CLUSTER_MAP.HELP.PATHFIND_CALC),
			new HelpEntry("End", STRINGS.ONIACCESS.SCANNER.HELP.REFRESH),
			new HelpEntry("Ctrl+PageUp/Down", STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_CATEGORY),
			new HelpEntry("PageUp/Down", STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_ITEM),
			new HelpEntry("Alt+PageUp/Down", STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_INSTANCE),
			new HelpEntry("Ctrl+F", STRINGS.ONIACCESS.SCANNER.HELP.SEARCH),
			new HelpEntry("Home", STRINGS.ONIACCESS.SCANNER.HELP.TELEPORT),
			new HelpEntry("Backspace", STRINGS.ONIACCESS.SCANNER.HELP.TELEPORT_BACK),
			new HelpEntry("Shift+Home", STRINGS.ONIACCESS.SCANNER.HELP.ORIENT_ITEM),
			new HelpEntry("Shift+End", STRINGS.ONIACCESS.SCANNER.HELP.TOGGLE_AUTO_MOVE),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			if (!_activated) {
				_activated = true;
				_cursorLocation = FindStartLocation();

				var mode = ClusterMapScreen.Instance.GetMode();
				if (mode == ClusterMapScreen.Mode.SelectDestination) {
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.CLUSTER_MAP.SELECT_DESTINATION);
				} else {
					base.OnActivate();
				}
			} else {
				SpeechPipeline.SpeakInterrupt(DisplayName);
			}

			SpeechPipeline.SpeakQueued(HexAnnouncer.AnnounceHex(_cursorLocation));
		}

		public override void OnDeactivate() {
			_activated = false;
		}

		// ========================================
		// INPUT
		// ========================================

		public override bool Tick() {
			// --- Hex direction keys (6-direction) ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.U)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.NORTHWEST);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.O)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.NORTHEAST);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.J)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.WEST);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.L)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.EAST);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.N)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.SOUTHWEST);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Period)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(AxialI.SOUTHEAST);
				return true;
			}

			// --- Arrow key fallback ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(HexCursor.ArrowToHexDirection(
					_cursorLocation, HexCursor.Direction.Up));
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(HexCursor.ArrowToHexDirection(
					_cursorLocation, HexCursor.Direction.Down));
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(HexCursor.ArrowToHexDirection(
					_cursorLocation, HexCursor.Direction.Left));
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)
				&& !InputUtil.AnyModifierHeld()) {
				MoveHex(HexCursor.ArrowToHexDirection(
					_cursorLocation, HexCursor.Direction.Right));
				return true;
			}

			// --- H: jump to active world ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.H)
				&& !InputUtil.AnyModifierHeld()) {
				_cursorLocation = FindStartLocation();
				SpeechPipeline.SpeakInterrupt(
					HexAnnouncer.AnnounceHex(_cursorLocation));
				return true;
			}

			// --- Enter: select entity or confirm destination ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				if (InputUtil.CtrlHeld()) {
					SwitchToWorld();
				} else {
					HandleEnter();
				}
				return true;
			}

			// --- K: read coordinates ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.K)
				&& !InputUtil.AnyModifierHeld()) {
				ReadCoordinates();
				return true;
			}

			// --- I: read entity details ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.I)
				&& !InputUtil.AnyModifierHeld()) {
				SpeechPipeline.SpeakInterrupt(
					HexAnnouncer.AnnounceTooltip(_cursorLocation));
				return true;
			}

			// --- Pathfinder ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)
				&& !InputUtil.AnyModifierHeld()) {
				SetPathStart();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.D)
				&& !InputUtil.AnyModifierHeld()) {
				CalculatePath();
				return true;
			}

			// --- Scanner keys ---
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F)
				&& InputUtil.CtrlCmdHeld()) {
				HandlerStack.Push(new SearchInputHandler(
					q => _scanner.SearchRefresh(q, _cursorLocation)));
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.End)) {
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(_scanner.ToggleAutoMove());
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					_scanner.Refresh(_cursorLocation);
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Home)) {
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(
						_scanner.OrientItem(_cursorLocation));
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					var dest = _scanner.Teleport(_cursorLocation);
					if (dest.HasValue) {
						_cursorLocation = dest.Value;
						SpeechPipeline.SpeakInterrupt(
							HexAnnouncer.AnnounceHex(_cursorLocation));
					}
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backspace)
				&& !InputUtil.AnyModifierHeld()) {
				var dest = _scanner.TeleportBack();
				if (dest.HasValue) {
					_cursorLocation = dest.Value;
					SpeechPipeline.SpeakInterrupt(
						HexAnnouncer.AnnounceHex(_cursorLocation));
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.PageUp)) {
				if (InputUtil.CtrlHeld())
					_scanner.CycleCategory(-1, _cursorLocation);
				else if (InputUtil.AltHeld())
					_scanner.CycleInstance(-1, _cursorLocation);
				else
					_scanner.CycleItem(-1, _cursorLocation);
				CheckAutoMove();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.PageDown)) {
				if (InputUtil.CtrlHeld())
					_scanner.CycleCategory(1, _cursorLocation);
				else if (InputUtil.AltHeld())
					_scanner.CycleInstance(1, _cursorLocation);
				else
					_scanner.CycleItem(1, _cursorLocation);
				CheckAutoMove();
				return true;
			}

			return false;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (e.IsAction(Action.Escape)) {
				var mode = ClusterMapScreen.Instance.GetMode();
				if (mode == ClusterMapScreen.Mode.SelectDestination) {
					e.TryConsume(Action.Escape);

					try {
						var closeOnSelect = (bool)HarmonyLib.AccessTools.Field(
							typeof(ClusterMapScreen), "m_closeOnSelect")
							.GetValue(ClusterMapScreen.Instance);

						if (closeOnSelect) {
							// Map was opened for this selection — cancel and close entirely.
							var selector = HarmonyLib.AccessTools.Field(
								typeof(ClusterMapScreen), "m_destinationSelector")
								.GetValue(ClusterMapScreen.Instance) as ClusterDestinationSelector;
							HarmonyLib.AccessTools.Method(typeof(ClusterMapScreen), "SetMode")
								.Invoke(ClusterMapScreen.Instance,
									new object[] { ClusterMapScreen.Mode.Default });
							if (selector != null)
								selector.Trigger(94158097);
							ManagementMenu.Instance.CloseAll();
						} else {
							ClusterMapScreen.Instance.TryHandleCancel();
						}
					} catch (System.Exception ex) {
						Util.Log.Error($"ClusterMapHandler.HandleKeyDown (destination cancel): {ex}");
						ClusterMapScreen.Instance.TryHandleCancel();
					}

					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.CLUSTER_MAP.DESTINATION_CANCELLED);
					return true;
				}
				// Otherwise let the game handle Escape (closes the screen,
				// which pops this handler via the Show patch)
				return false;
			}
			return false;
		}

		// ========================================
		// MOVEMENT
		// ========================================

		private void MoveHex(AxialI direction) {
			if (HexCursor.TryMove(_cursorLocation, direction, out var next)) {
				_cursorLocation = next;
				SpeechPipeline.SpeakInterrupt(
					HexAnnouncer.AnnounceHex(_cursorLocation));
			} else {
				PlaySound("Negative");
			}
		}

		// ========================================
		// ENTITY SELECTION
		// ========================================

		private void HandleEnter() {
			var mode = ClusterMapScreen.Instance.GetMode();
			if (mode == ClusterMapScreen.Mode.SelectDestination) {
				ConfirmDestination();
				return;
			}

			var entities = ClusterGrid.Instance.GetVisibleEntitiesAtCell(
				_cursorLocation);
			if (entities == null || entities.Count == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.NOTHING_TO_SELECT);
				return;
			}

			// Filter to selectable entities
			var selectables = new List<ClusterGridEntity>();
			foreach (var entity in entities) {
				var sel = entity.GetComponent<KSelectable>();
				if (entity.IsVisible && sel != null && sel.IsSelectable)
					selectables.Add(entity);
			}

			if (selectables.Count == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.NOTHING_TO_SELECT);
				return;
			}
			if (selectables.Count == 1) {
				PlaySound("Select_empty");
				ClusterMapSelectTool.Instance.Select(
					selectables[0].GetComponent<KSelectable>());
				return;
			}
			HandlerStack.Push(new ClusterEntityPickerHandler(selectables));
		}

		private void ConfirmDestination() {
			try {
				var selectorField = HarmonyLib.AccessTools.Field(
					typeof(ClusterMapScreen), "m_destinationSelector");
				var selector = selectorField?.GetValue(
					ClusterMapScreen.Instance) as ClusterDestinationSelector;
				if (selector == null) {
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.CLUSTER_MAP.NO_PATH);
					return;
				}

				var path = ClusterGrid.Instance.GetPath(
					selector.GetMyWorldLocation(), _cursorLocation, selector);
				if (path == null || path.Count == 0) {
					SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.ONIACCESS.CLUSTER_MAP.NO_PATH);
					return;
				}

				// Mirror the game's SelectHex flow: FinishingSelectDestination →
				// SetDestination → CloseAll or Default
				var setMode = HarmonyLib.AccessTools.Method(
					typeof(ClusterMapScreen), "SetMode");
				setMode.Invoke(ClusterMapScreen.Instance,
					new object[] { ClusterMapScreen.Mode.FinishingSelectDestination });
				selector.SetDestination(_cursorLocation);
				var closeOnSelect = (bool)HarmonyLib.AccessTools.Field(
					typeof(ClusterMapScreen), "m_closeOnSelect")
					.GetValue(ClusterMapScreen.Instance);
				if (closeOnSelect)
					ManagementMenu.Instance.CloseAll();
				else
					setMode.Invoke(ClusterMapScreen.Instance,
						new object[] { ClusterMapScreen.Mode.Default });
				SpeechPipeline.SpeakInterrupt(string.Format(
					(string)STRINGS.ONIACCESS.CLUSTER_MAP.DESTINATION_SET,
					path.Count));
			} catch (System.Exception ex) {
				Util.Log.Error($"ClusterMapHandler.ConfirmDestination: {ex}");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.CLUSTER_MAP.NO_PATH);
			}
		}

		private void SwitchToWorld() {
			try {
				int worldId = ClusterUtil.GetAsteroidWorldIdAtLocation(_cursorLocation);
				if (worldId < 0) {
					PlaySound("Negative");
					return;
				}
				CameraController.Instance.ActiveWorldStarWipe(worldId);
			} catch (System.Exception ex) {
				Util.Log.Warn($"ClusterMapHandler.SwitchToWorld: {ex.Message}");
				PlaySound("Negative");
			}
		}

		// ========================================
		// COORDINATES
		// ========================================

		private void ReadCoordinates() {
			var home = FindHomeLocation();
			SpeechPipeline.SpeakInterrupt(
				HexCoordinates.Format(home, _cursorLocation));
		}

		// ========================================
		// PATHFINDER
		// ========================================

		private void SetPathStart() {
			_pathStart = _cursorLocation;
			_hasPathStart = true;
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.BUILD_MENU.START_SET);
		}

		private void CalculatePath() {
			if (!_hasPathStart) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.CLUSTER_MAP.SET_START_FIRST);
				return;
			}
			var result = HexPathfinder.FindPath(_pathStart, _cursorLocation);
			SpeechPipeline.SpeakInterrupt(HexPathfinder.FormatResult(result));
		}

		// ========================================
		// SCANNER AUTO-MOVE
		// ========================================

		private void CheckAutoMove() {
			if (!_scanner.AutoMove) return;
			var loc = _scanner.CurrentLocation();
			if (loc.HasValue)
				_cursorLocation = loc.Value;
		}

		// ========================================
		// LOCATION HELPERS
		// ========================================

		/// <summary>
		/// Find the starting cursor location when the map opens.
		/// Active world's asteroid -> rocket's location (for interiors) -> center.
		/// </summary>
		private AxialI FindStartLocation() {
			try {
				var world = ClusterManager.Instance.activeWorld;
				// GetMyWorldLocation handles asteroids, rocket interiors,
				// and rocket modules via the ClusterUtil extension chain
				return world.GetMyWorldLocation();
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"ClusterMapHandler.FindStartLocation: {ex.Message}");
			}
			return AxialI.ZERO;
		}

		/// <summary>
		/// Find the home asteroid location for coordinate reference.
		/// </summary>
		private AxialI FindHomeLocation() {
			try {
				var startWorld = ClusterManager.Instance.GetStartWorld();
				if (startWorld != null) {
					var asteroid = startWorld.GetComponent<AsteroidGridEntity>();
					if (asteroid != null) return asteroid.Location;
				}
			} catch (System.Exception ex) {
				Util.Log.Warn(
					$"ClusterMapHandler.FindHomeLocation: {ex.Message}");
			}
			return AxialI.ZERO;
		}
	}
}
