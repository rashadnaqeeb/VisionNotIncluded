using System.Collections.Generic;
using OniAccess.Handlers.Notifications;
using OniAccess.Handlers.Tiles.Scanner;
using OniAccess.Handlers.Tiles.Skip;
using OniAccess.Input;
using OniAccess.Speech;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// BaseScreenHandler bound to the Hud KScreen. Active when the game world
	/// is loaded and no modal menu is on top.
	///
	/// Routes arrow keys to TileCursor movement, K to coordinate
	/// reading, Shift+K to coordinate mode cycling.
	///
	/// CapturesAllInput = false: game hotkeys (overlays, tools, WASD camera,
	/// pause) pass through.
	/// </summary>
	public class TileCursorHandler: BaseScreenHandler {
		private Overlays.OverlayProfileRegistry _overlayRegistry;
		private SkipEngine _skipEngine;
		private ScannerNavigator _scanner;
		private CursorBookmarks _bookmarks;
		private GameStateMonitor _monitor;
		private NotificationTracker _notificationTracker;
		private NotificationAnnouncer _notificationAnnouncer;
		private DupeNavigator _dupeNavigator;
		private BotNavigator _botNavigator;
		private ExternalFollowListener _externalFollow;
		private PathabilityChecker _pathabilityChecker;
		private bool _lastCycledBots;
		private bool _overlaySubscribed;
		private int _preJumpCell = Grid.InvalidCell;
		private int _queueNextOverlayTtl;
		private HashedString _lastOverlayMode;

		public void QueueNextOverlayAnnouncement() => _queueNextOverlayTtl = 2;

		private static readonly ConsumedKey[] _consumedKeys = {
			new ConsumedKey(KKeyCode.Tab),
			new ConsumedKey(KKeyCode.BackQuote),
			new ConsumedKey(KKeyCode.F, Modifier.Ctrl),
			// A overwrites PanLeft (camera pan — mod cursor replaces camera navigation)
			new ConsumedKey(KKeyCode.A),
			new ConsumedKey(KKeyCode.I),
			new ConsumedKey(KKeyCode.I, Modifier.Shift),
			new ConsumedKey(KKeyCode.P, Modifier.Shift),
			// D overwrites PanRight (camera pan — mod cursor replaces camera navigation)
			new ConsumedKey(KKeyCode.D),
			new ConsumedKey(KKeyCode.D, Modifier.Shift),
			new ConsumedKey(KKeyCode.K, Modifier.Shift),
			new ConsumedKey(KKeyCode.UpArrow),
			new ConsumedKey(KKeyCode.DownArrow),
			new ConsumedKey(KKeyCode.LeftArrow),
			new ConsumedKey(KKeyCode.RightArrow),
			new ConsumedKey(KKeyCode.UpArrow, Modifier.Shift),
			new ConsumedKey(KKeyCode.DownArrow, Modifier.Shift),
			new ConsumedKey(KKeyCode.DownArrow, Modifier.Ctrl | Modifier.Shift),
			new ConsumedKey(KKeyCode.UpArrow, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.DownArrow, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.LeftArrow, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.RightArrow, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.UpArrow, Modifier.Alt),
			new ConsumedKey(KKeyCode.DownArrow, Modifier.Alt),
			new ConsumedKey(KKeyCode.LeftArrow, Modifier.Alt),
			new ConsumedKey(KKeyCode.RightArrow, Modifier.Alt),
			// Scanner keybinds
			new ConsumedKey(KKeyCode.Backspace),
			new ConsumedKey(KKeyCode.End),
			new ConsumedKey(KKeyCode.Home),
			new ConsumedKey(KKeyCode.Home, Modifier.Shift),
			new ConsumedKey(KKeyCode.End, Modifier.Shift),
			new ConsumedKey(KKeyCode.PageUp, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.PageDown, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.PageUp, Modifier.Shift),
			new ConsumedKey(KKeyCode.PageDown, Modifier.Shift),
			new ConsumedKey(KKeyCode.PageUp),
			new ConsumedKey(KKeyCode.PageDown),
			new ConsumedKey(KKeyCode.PageUp, Modifier.Alt),
			new ConsumedKey(KKeyCode.PageDown, Modifier.Alt),
			new ConsumedKey(KKeyCode.Q),
			new ConsumedKey(KKeyCode.Q, Modifier.Shift),
			new ConsumedKey(KKeyCode.Return),
			new ConsumedKey(KKeyCode.N, Modifier.Shift),
			new ConsumedKey(KKeyCode.S),
			// Red alert keybind
			new ConsumedKey(KKeyCode.R, Modifier.Ctrl),
			// Ruler keybinds
			new ConsumedKey(KKeyCode.B, Modifier.Ctrl),
			new ConsumedKey(KKeyCode.B, Modifier.Ctrl | Modifier.Shift),
			// Bookmark keybinds
			new ConsumedKey(KKeyCode.H),
			new ConsumedKey(KKeyCode.H, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha1, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha2, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha3, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha4, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha5, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha6, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha7, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha8, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha9, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha0, Modifier.Shift),
			new ConsumedKey(KKeyCode.Alpha1, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha2, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha3, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha4, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha5, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha6, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha7, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha8, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha9, Modifier.Alt),
			new ConsumedKey(KKeyCode.Alpha0, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad1, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad2, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad3, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad4, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad5, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad6, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad7, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad8, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad9, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad0, Modifier.Shift),
			new ConsumedKey(KKeyCode.Keypad1, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad2, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad3, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad4, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad5, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad6, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad7, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad8, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad9, Modifier.Alt),
			new ConsumedKey(KKeyCode.Keypad0, Modifier.Alt),
			// Dupe and bot cycling keybinds
			new ConsumedKey(KKeyCode.LeftBracket),
			new ConsumedKey(KKeyCode.RightBracket),
			new ConsumedKey(KKeyCode.LeftBracket, Modifier.Shift),
			new ConsumedKey(KKeyCode.RightBracket, Modifier.Shift),
			new ConsumedKey(KKeyCode.Backslash),
			new ConsumedKey(KKeyCode.Backslash, Modifier.Shift),
			new ConsumedKey(KKeyCode.Backslash, Modifier.Ctrl),
			// W overwrites PanUp (camera pan — mod cursor replaces camera navigation)
			new ConsumedKey(KKeyCode.W),
			// Shift+G opens disinfect threshold settings (G = game's dig tool; Shift variant is free)
			new ConsumedKey(KKeyCode.G, Modifier.Shift),
			// Shift+V opens the fast travel menu (V is unbound in vanilla)
			new ConsumedKey(KKeyCode.V, Modifier.Shift),
		};
		public override IReadOnlyList<ConsumedKey> ConsumedKeys => _consumedKeys;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Arrow keys", (string)STRINGS.ONIACCESS.HELP.MOVE_CURSOR),
			new HelpEntry("Shift+Up/Down", (string)STRINGS.ONIACCESS.BIG_CURSOR.HELP_CYCLE_SIZE),
			new HelpEntry("Ctrl+Shift+Down", (string)STRINGS.ONIACCESS.BIG_CURSOR.HELP_RESET_SIZE),
			new HelpEntry("Ctrl+Arrow keys", (string)STRINGS.ONIACCESS.SKIP.HELP_SKIP),
			new HelpEntry("Alt+Arrow keys", (string)STRINGS.ONIACCESS.SKIP.HELP_SKIP_DEFAULT),
			new HelpEntry("Tab", (string)STRINGS.ONIACCESS.BUILD_MENU.HELP_OPEN_ACTION_MENU),
			new HelpEntry("Enter", (string)STRINGS.ONIACCESS.HELP.SELECT_ENTITY),
			new HelpEntry("A", (string)STRINGS.ONIACCESS.HELP.READ_TILE_DETAILS),
			new HelpEntry("I", (string)STRINGS.ONIACCESS.HELP.READ_TOOLTIP_SUMMARY),
			new HelpEntry("Shift+K", (string)STRINGS.ONIACCESS.HELP.READ_COORDS),
			new HelpEntry("End", (string)STRINGS.ONIACCESS.SCANNER.HELP.REFRESH),
			new HelpEntry("Home", (string)STRINGS.ONIACCESS.SCANNER.HELP.TELEPORT),
			new HelpEntry("Backspace", (string)STRINGS.ONIACCESS.SCANNER.HELP.TELEPORT_BACK),
			new HelpEntry("Shift+End", (string)STRINGS.ONIACCESS.SCANNER.HELP.TOGGLE_AUTO_MOVE),
			new HelpEntry("Shift+Home", (string)STRINGS.ONIACCESS.SCANNER.HELP.ORIENT_ITEM),
			new HelpEntry("Ctrl+PageUp/Down", (string)STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_CATEGORY),
			new HelpEntry("Shift+PageUp/Down", (string)STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_SUBCATEGORY),
			new HelpEntry("PageUp/Down", (string)STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_ITEM),
			new HelpEntry("Alt+PageUp/Down", (string)STRINGS.ONIACCESS.SCANNER.HELP.CYCLE_INSTANCE),
			new HelpEntry("Ctrl+F", (string)STRINGS.ONIACCESS.SCANNER.HELP.SEARCH),
			new HelpEntry("Q", (string)STRINGS.ONIACCESS.GAME_STATE.READ_CYCLE_STATUS),
			new HelpEntry("Shift+Q", (string)STRINGS.ONIACCESS.GAME_STATE.READ_TIME_PLAYED),
			new HelpEntry("S", (string)STRINGS.ONIACCESS.GAME_STATE.READ_COLONY_STATUS),
			new HelpEntry("Ctrl+R", (string)STRINGS.ONIACCESS.GAME_STATE.TOGGLE_RED_ALERT),
			new HelpEntry("`", (string)STRINGS.ONIACCESS.HELP.CYCLE_GAME_SPEED),
			new HelpEntry("D", (string)STRINGS.ONIACCESS.DIAGNOSTICS.HELP_READ),
			new HelpEntry("Shift+D", (string)STRINGS.ONIACCESS.DIAGNOSTICS.HELP_OPEN_BROWSER),
			new HelpEntry("Shift+N", (string)STRINGS.ONIACCESS.NOTIFICATIONS.OPEN_MENU_HELP),
			new HelpEntry("Shift+I", (string)STRINGS.ONIACCESS.RESOURCES.HELP_OPEN),
			new HelpEntry("Shift+P", (string)STRINGS.ONIACCESS.RESOURCES.HELP_READ_PINNED),
			new HelpEntry("H", (string)STRINGS.ONIACCESS.BOOKMARKS.HELP_HOME),
			new HelpEntry("Ctrl+1-0", (string)STRINGS.ONIACCESS.BOOKMARKS.HELP_SET_BOOKMARK),
			new HelpEntry("Shift+1-0", (string)STRINGS.ONIACCESS.BOOKMARKS.HELP_GOTO_BOOKMARK),
			new HelpEntry("Alt+1-0", (string)STRINGS.ONIACCESS.BOOKMARKS.HELP_ORIENT_BOOKMARK),
			new HelpEntry("Ctrl+B", (string)STRINGS.ONIACCESS.RULER.HELP_PLACE),
			new HelpEntry("Ctrl+Shift+B", (string)STRINGS.ONIACCESS.RULER.HELP_CLEAR),
			new HelpEntry((string)STRINGS.ONIACCESS.DUPES.KEY_BRACKETS, (string)STRINGS.ONIACCESS.DUPES.HELP_CYCLE),
			new HelpEntry((string)STRINGS.ONIACCESS.BOTS.KEY_SHIFT_BRACKETS, (string)STRINGS.ONIACCESS.BOTS.HELP_CYCLE),
			new HelpEntry("\\", (string)STRINGS.ONIACCESS.DUPES.HELP_JUMP),
			new HelpEntry("Ctrl+\\", (string)STRINGS.ONIACCESS.DUPES.FOLLOW.HELP_FOLLOW),
			new HelpEntry("Shift+\\", (string)STRINGS.ONIACCESS.DUPES.HELP_CHECK_PATH),
			new HelpEntry("W", (string)STRINGS.ONIACCESS.WORLD_SELECTOR.OPEN),
			new HelpEntry("Shift+G", (string)STRINGS.ONIACCESS.DISINFECT_SETTINGS.HELP_OPEN),
			new HelpEntry("Shift+V", (string)STRINGS.ONIACCESS.FAST_TRAVEL.HELP_OPEN),
			// Base game management screen hotkeys. The mod does not consume these keys;
			// they are listed here so blind players can discover them via the help screen.
			new HelpEntry("L", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.PRIORITIES),
			new HelpEntry("F", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.CONSUMABLES),
			new HelpEntry("V", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.VITALS),
			new HelpEntry("R", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.RESEARCH),
			new HelpEntry((string)STRINGS.ONIACCESS.TILE_CURSOR.KEY_PERIOD, (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.SCHEDULE),
			new HelpEntry("J", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.SKILLS),
			new HelpEntry("E", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.COLONY_REPORT),
			new HelpEntry("U", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.DATABASE),
			new HelpEntry("Z", (string)STRINGS.ONIACCESS.TILE_CURSOR.MANAGEMENT_HELP.STARMAP),
		}.AsReadOnly();

		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.COLONY_VIEW;
		public override bool CapturesAllInput => false;
		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		public TileCursorHandler(KScreen screen) : base(screen) {
		}

		public override void OnActivate() {
			if (_scanner == null) {
				_overlayRegistry = Overlays.OverlayProfileRegistry.Build();
				_skipEngine = new SkipEngine(SkipStrategyRegistry.Build());
				ToolProfiles.ToolProfileRegistry.Build();
				TileCursor.Create(_overlayRegistry);
				_scanner = new ScannerNavigator();
				_bookmarks = new CursorBookmarks();
				_monitor = new GameStateMonitor();
				_dupeNavigator = new DupeNavigator();
				_botNavigator = new BotNavigator();
				_externalFollow = new ExternalFollowListener();
				_pathabilityChecker = new PathabilityChecker();
				if (NotificationManager.Instance != null) {
					_notificationTracker = new NotificationTracker();
					_notificationTracker.Attach();
					_notificationAnnouncer = new NotificationAnnouncer(_notificationTracker);
				}
				LoadGate.Reset();
				SpeechPipeline.SpeakQueued(DisplayName);
				try {
					TileCursor.Instance.Initialize();
				} catch (System.Exception ex) {
					Util.Log.Error($"TileCursorHandler.OnActivate: cursor init failed: {ex}");
				}
			}
			if (CursorRuler.Instance == null)
				CursorRuler.Create();
			if (OverlayScreen.Instance != null)
				OverlayScreen.Instance.OnOverlayChanged -= OnOverlayChanged;
			if (Game.Instance != null)
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);
			if (OverlayScreen.Instance != null) {
				OverlayScreen.Instance.OnOverlayChanged += OnOverlayChanged;
				try {
					_lastOverlayMode = OverlayScreen.Instance.mode;
					_overlaySubscribed = true;
				} catch {
					// OverlayScreen exists but mode not populated yet during early load.
					// Tick() will retry via the deferred subscription path.
					_overlaySubscribed = false;
				}
			} else {
				_overlaySubscribed = false;
			}
			if (Game.Instance != null)
				Game.Instance.Subscribe(1174281782, OnActiveToolChanged);
		}

		public override void OnDeactivate() {
			if (Game.Instance != null)
				Game.Instance.Unsubscribe(1174281782, OnActiveToolChanged);
			_notificationAnnouncer?.Detach();
			_notificationAnnouncer = null;
			_notificationTracker?.Detach();
			_notificationTracker = null;
			_externalFollow?.StopFollow();
			_dupeNavigator?.StopFollowAndClear();
			_botNavigator?.StopFollowAndClear();
			TileCursor.Destroy();
			CursorRuler.Destroy();
			ScannerNavigator.Destroy();
			_scanner = null;
			_dupeNavigator = null;
			_botNavigator = null;
			_externalFollow = null;
			_pathabilityChecker = null;
			if (OverlayScreen.Instance != null)
				OverlayScreen.Instance.OnOverlayChanged -= OnOverlayChanged;
			_overlaySubscribed = false;
		}

		private void OnOverlayChanged(HashedString newMode) {
			TileCursor.Instance.ResetRoomName();
			// Skip during timelapse — the game toggles overlay off for screenshots
			if (Game.Instance?.timelapser?.CapturingTimelapseScreenshot == true) return;
			// Skip redundant announcements when the game resets the overlay to None
			// (e.g., ManagementMenu.ToggleScreen resets overlay before opening a screen).
			if (newMode == _lastOverlayMode) return;
			_lastOverlayMode = newMode;
			Audio.SonifierController.Instance?.OnOverlayChanged(newMode);
			if (_queueNextOverlayTtl > 0) {
				_queueNextOverlayTtl = 0;
				SpeechPipeline.SpeakQueued(_overlayRegistry.GetOverlayName(newMode));
			} else {
				SpeechPipeline.SpeakInterrupt(_overlayRegistry.GetOverlayName(newMode));
			}
		}

		public override bool Tick() {
			if (_queueNextOverlayTtl > 0)
				_queueNextOverlayTtl--;

			if (!_overlaySubscribed && OverlayScreen.Instance != null) {
				try {
					_lastOverlayMode = OverlayScreen.Instance.mode;
				} catch {
					// OverlayScreen.Instance exists but currentModeInfo
					// isn't populated yet during early load. Retry next frame.
					return false;
				}
				OverlayScreen.Instance.OnOverlayChanged += OnOverlayChanged;
				_overlaySubscribed = true;
			}

			string worldSwitchSpeech = TileCursor.Instance.CheckWorldSwitch();
			if (worldSwitchSpeech != null) {
				SpeechPipeline.SpeakInterrupt(worldSwitchSpeech);
				Audio.EarconScheduler.Instance?.ResetTransitionState();
				UpdateAudioForCell();
			}
			_scanner.CheckWorldSwitch();
			_monitor.Tick();
			_notificationAnnouncer?.Tick();
			_dupeNavigator.TickFollow();
			_botNavigator.TickFollow();
			_externalFollow.TickFollow(_dupeNavigator.IsFollowing, _botNavigator.IsFollowing);
			LoadGate.Tick();

			string arrived = TileCursor.Instance.SyncToCamera();
			if (arrived != null) {
				SpeechPipeline.SpeakInterrupt(arrived);
				Audio.EarconScheduler.Instance?.ResetTransitionState();
				UpdateAudioForCell();
			}

			CursorRuler.Instance.OnCursorMoved(TileCursor.Instance.Cell);

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
				&& !InputUtil.AnyModifierHeld()) {
				OpenEntityPicker();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)
				&& !InputUtil.AnyModifierHeld()) {
				OpenActionMenu();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.BackQuote)
				&& !InputUtil.AnyModifierHeld()) {
				_monitor.CycleSpeed();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& InputUtil.ShiftHeld() && InputUtil.CtrlHeld()) {
				string result = TileCursor.Instance.ResetRadius();
				if (result != null) {
					PlaySound("HUD_Click_Deselect");
					SpeechPipeline.SpeakInterrupt(result);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
				&& InputUtil.ShiftHeld() && !InputUtil.CtrlHeld()) {
				string result = TileCursor.Instance.IncreaseRadius();
				if (result != null) {
					PlaySound("HUD_Click_Deselect");
					SpeechPipeline.SpeakInterrupt(result);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& InputUtil.ShiftHeld() && !InputUtil.CtrlHeld()) {
				string result = TileCursor.Instance.DecreaseRadius();
				if (result != null) {
					PlaySound("HUD_Click_Deselect");
					SpeechPipeline.SpeakInterrupt(result);
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
				&& InputUtil.AltHeld() && !InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.SkipDefault(Direction.Up));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& InputUtil.AltHeld() && !InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.SkipDefault(Direction.Down));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)
				&& InputUtil.AltHeld() && !InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.SkipDefault(Direction.Left));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)
				&& InputUtil.AltHeld() && !InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.SkipDefault(Direction.Right));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
				&& InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.Skip(Direction.Up));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.Skip(Direction.Down));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)
				&& InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.Skip(Direction.Left));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)
				&& InputUtil.CtrlHeld()) {
				SpeechPipeline.SpeakInterrupt(_skipEngine.Skip(Direction.Right));
				UpdateAudioForCell();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)
				&& !InputUtil.AnyModifierHeld()) {
				SpeakMove(Direction.Up);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)
				&& !InputUtil.AnyModifierHeld()) {
				SpeakMove(Direction.Down);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)
				&& !InputUtil.AnyModifierHeld()) {
				SpeakMove(Direction.Left);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)
				&& !InputUtil.AnyModifierHeld()) {
				SpeakMove(Direction.Right);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.K)
				&& InputUtil.ShiftHeld()) {
				SpeechPipeline.SpeakInterrupt(TileCursor.Instance.ReadCoordinates());
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.A)
				&& !InputUtil.AnyModifierHeld()) {
				string details = TileCursor.Instance.ReadTileDetails();
				if (details != null)
					SpeechPipeline.SpeakInterrupt(details);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.I)) {
				if (InputUtil.ShiftHeld()) {
					OpenResourceBrowser();
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					ReadTooltipSummary();
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.P)
				&& InputUtil.ShiftHeld()) {
				ReadPinnedResources();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Q)) {
				if (InputUtil.ShiftHeld()) {
					_monitor.SpeakTimePlayed();
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					_monitor.SpeakCycleStatus();
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.S)
				&& !InputUtil.AnyModifierHeld()) {
				_monitor.SpeakColonyStatus();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.D)) {
				if (InputUtil.ShiftHeld()) {
					OpenDiagnosticBrowser();
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					ReadDiagnostics();
					return true;
				}
			}
			// Dupe cycling
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftBracket)
				&& !InputUtil.AnyModifierHeld()) {
				_lastCycledBots = false;
				bool wasFollowingBot = _botNavigator.IsFollowing;
				if (wasFollowingBot)
					_botNavigator.StopFollowAndClear();
				_dupeNavigator.CycleDupe(-1);
				if (wasFollowingBot)
					_dupeNavigator.StartFollow();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightBracket)
				&& !InputUtil.AnyModifierHeld()) {
				_lastCycledBots = false;
				bool wasFollowingBot = _botNavigator.IsFollowing;
				if (wasFollowingBot)
					_botNavigator.StopFollowAndClear();
				_dupeNavigator.CycleDupe(1);
				if (wasFollowingBot)
					_dupeNavigator.StartFollow();
				return true;
			}
			// Bot cycling
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftBracket)
				&& InputUtil.ShiftHeld()) {
				_lastCycledBots = true;
				bool wasFollowingDupe = _dupeNavigator.IsFollowing;
				if (wasFollowingDupe)
					_dupeNavigator.StopFollowAndClear();
				_botNavigator.CycleBot(-1);
				if (wasFollowingDupe)
					_botNavigator.StartFollow();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightBracket)
				&& InputUtil.ShiftHeld()) {
				_lastCycledBots = true;
				bool wasFollowingDupe = _dupeNavigator.IsFollowing;
				if (wasFollowingDupe)
					_dupeNavigator.StopFollowAndClear();
				_botNavigator.CycleBot(1);
				if (wasFollowingDupe)
					_botNavigator.StartFollow();
				return true;
			}
			// Jump/follow/pathability — targets whichever entity type was last cycled
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backslash)) {
				if (InputUtil.CtrlHeld() && !InputUtil.ShiftHeld()) {
					string speech = _lastCycledBots
						? _botNavigator.StartFollow()
						: _dupeNavigator.StartFollow();
					if (speech != null)
						SpeechPipeline.SpeakInterrupt(speech);
					return true;
				}
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(
						_pathabilityChecker.Check(_dupeNavigator, _botNavigator, _lastCycledBots));
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					_preJumpCell = TileCursor.Instance.Cell;
					if (_lastCycledBots)
						_botNavigator.JumpOrSelect();
					else
						_dupeNavigator.JumpOrSelect();
					Audio.EarconScheduler.Instance?.ResetTransitionState();
					UpdateAudioForCell();
					return true;
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.W)
				&& !InputUtil.AnyModifierHeld()) {
				OpenWorldSelector();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.N)
				&& InputUtil.ShiftHeld()) {
				OpenNotificationMenu();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.G)
				&& InputUtil.ShiftHeld()) {
				OpenDisinfectSettings();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.V)
				&& InputUtil.ShiftHeld() && !InputUtil.CtrlHeld() && !InputUtil.AltHeld()) {
				OpenFastTravelMenu();
				return true;
			}

			// Red alert toggle
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R)
				&& InputUtil.CtrlHeld() && !InputUtil.ShiftHeld()) {
				_monitor.ToggleRedAlert();
				return true;
			}

			// Ruler keybinds
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.B)
				&& InputUtil.CtrlHeld()) {
				if (InputUtil.ShiftHeld())
					SpeechPipeline.SpeakInterrupt(CursorRuler.Instance.Clear());
				else
					SpeechPipeline.SpeakInterrupt(
						CursorRuler.Instance.PlaceAt(TileCursor.Instance.Cell));
				return true;
			}

			// Bookmark keybinds
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.H)) {
				if (InputUtil.AltHeld()) {
					SpeechPipeline.SpeakInterrupt(CursorBookmarks.OrientHome());
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					SpeechPipeline.SpeakInterrupt(CursorBookmarks.JumpHome());
					Audio.EarconScheduler.Instance?.ResetTransitionState();
					UpdateAudioForCell();
					return true;
				}
			}
			int bmDigit = InputUtil.GetDigitKeyDown();
			if (bmDigit >= 0) {
				int idx = bmDigit == 0 ? 9 : bmDigit - 1;
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(_bookmarks.Goto(idx));
					Audio.EarconScheduler.Instance?.ResetTransitionState();
					UpdateAudioForCell();
					return true;
				}
				if (InputUtil.AltHeld()) {
					SpeechPipeline.SpeakInterrupt(_bookmarks.Orient(idx));
					return true;
				}
				if (InputUtil.CtrlHeld()) {
					string speech = _bookmarks.Set(idx);
					if (speech != null)
						SpeechPipeline.SpeakQueued(speech);
					return true;
				}
			}

			// Scanner keybinds
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F)
				&& InputUtil.CtrlHeld()) {
				HandlerStack.Push(new SearchInputHandler(q => _scanner.SearchRefresh(q)));
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.End)) {
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(_scanner.ToggleAutoMove());
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					_scanner.Refresh();
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Home)) {
				if (InputUtil.ShiftHeld()) {
					SpeechPipeline.SpeakInterrupt(_scanner.OrientItem());
					return true;
				}
				if (!InputUtil.AnyModifierHeld()) {
					_preJumpCell = TileCursor.Instance.Cell;
					_scanner.Teleport();
					Audio.EarconScheduler.Instance?.ResetTransitionState();
					UpdateAudioForCell();
					return true;
				}
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backspace)
				&& !InputUtil.AnyModifierHeld()) {
				if (_preJumpCell != Grid.InvalidCell) {
					int savedCell = _preJumpCell;
					_preJumpCell = Grid.InvalidCell;
					string speech = TileCursor.Instance.JumpTo(savedCell);
					if (speech != null) {
						SpeechPipeline.SpeakInterrupt(speech);
						Audio.EarconScheduler.Instance?.ResetTransitionState();
						UpdateAudioForCell();
					}
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.PageUp)) {
				if (InputUtil.CtrlHeld())
					_scanner.CycleCategory(-1);
				else if (InputUtil.ShiftHeld())
					_scanner.CycleSubcategory(-1);
				else if (InputUtil.AltHeld())
					_scanner.CycleInstance(-1);
				else
					_scanner.CycleItem(-1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.PageDown)) {
				if (InputUtil.CtrlHeld())
					_scanner.CycleCategory(1);
				else if (InputUtil.ShiftHeld())
					_scanner.CycleSubcategory(1);
				else if (InputUtil.AltHeld())
					_scanner.CycleInstance(1);
				else
					_scanner.CycleItem(1);
				return true;
			}
			return false;
		}

		private void SpeakMove(Direction direction) {
			string speech = TileCursor.Instance.Move(direction);
			if (speech != null) {
				SpeechPipeline.SpeakInterrupt(speech);
				UpdateAudioForCell();
			} else {
				Audio.EarconScheduler.Instance?.CancelAll();
				Audio.ShapeEarconPlayer.Instance?.CancelAll();
				Audio.SonifierController.Instance?.Stop();
			}
		}

		/// <summary>
		/// Move the tile cursor to the given cell and run the standard
		/// post-teleport bookkeeping: reset audio transition state, refresh
		/// per-cell earcons/sonification, and speak the destination tile.
		/// Used by handlers that close themselves and hand control back to
		/// the colony view (e.g., the fast travel menu).
		/// </summary>
		public void TeleportCursorTo(int cell) {
			string speech = TileCursor.Instance.JumpTo(cell);
			Audio.EarconScheduler.Instance?.ResetTransitionState();
			UpdateAudioForCell();
			if (speech != null)
				SpeechPipeline.SpeakInterrupt(speech);
		}

		private void UpdateAudioForCell() {
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				Audio.EarconScheduler.Instance?.CancelAll();
				Audio.EarconScheduler.Instance?.ResetTransitionState();
				Audio.ShapeEarconPlayer.Instance?.CancelAll();
				Audio.SonifierController.Instance?.Stop();
				return;
			}
			HashedString mode = OverlayScreen.Instance != null
				? OverlayScreen.Instance.GetMode()
				: OverlayModes.None.ID;
			if (Audio.EarconScheduler.Instance != null)
				Audio.EarconScheduler.Instance.PlayForCell(cell, mode);
			Audio.ShapeEarconPlayer.Instance?.OnCursorMoved(cell, mode);
			Audio.FootstepPlayer.Instance?.Play(cell);
			Audio.SonifierController.Instance?.OnCursorMoved(cell, mode);
		}

		private void OpenActionMenu() {
			if (!(PlayerController.Instance.ActiveTool is SelectTool))
				SelectTool.Instance.Activate();
			HandlerStack.Push(new Build.ActionMenuHandler());
		}

		private void OpenEntityPicker() {
			int cell = TileCursor.Instance.Cell;
			if (!Grid.IsVisible(cell)) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}
			var selectables = EntityPickerHandler.CollectSelectables(cell);
			if (selectables.Count == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.NOTHING_TO_SELECT);
				return;
			}
			if (selectables.Count == 1) {
				if (!(PlayerController.Instance.ActiveTool is SelectTool))
					SelectTool.Instance.Activate();
				SelectTool.Instance.Select(selectables[0]);
				return;
			}
			var tooltipLines = TooltipCapture.GetTooltipLines();
			var displayLabels = EntityPickerHandler.MatchTooltipLabels(
				selectables, tooltipLines);
			HandlerStack.Push(new EntityPickerHandler(selectables, displayLabels));
		}

		private void OpenResourceBrowser() {
			if (AllResourcesScreen.Instance != null)
				AllResourcesScreen.Instance.Show(true);
		}

		private void ReadPinnedResources() {
			string speech = Resources.ResourceHelper.BuildPinnedSpeech();
			SpeechPipeline.SpeakInterrupt(speech);
		}

		private void ReadDiagnostics() {
			if (ColonyDiagnosticUtility.Instance == null) return;
			int worldId = ClusterManager.Instance.activeWorldId;
			var settings = ColonyDiagnosticUtility.Instance.diagnosticDisplaySettings;
			if (!settings.ContainsKey(worldId)) return;

			var parts = new System.Collections.Generic.List<string>();
			// Collect qualifying diagnostics, then sort worst-first
			var qualifying = new System.Collections.Generic.List<ColonyDiagnostic>();
			foreach (var kvp in settings[worldId]) {
				if (ColonyDiagnosticUtility.Instance.IsDiagnosticTutorialDisabled(kvp.Key))
					continue;
				if (kvp.Value == ColonyDiagnosticUtility.DisplaySetting.Never)
					continue;
				var diag = ColonyDiagnosticUtility.Instance.GetDiagnostic(kvp.Key, worldId);
				if (diag == null) continue;
				if (kvp.Value == ColonyDiagnosticUtility.DisplaySetting.AlertOnly
					&& diag.LatestResult.opinion >= ColonyDiagnostic.DiagnosticResult.Opinion.Normal)
					continue;
				qualifying.Add(diag);
			}
			qualifying.Sort((a, b) => a.LatestResult.opinion.CompareTo(b.LatestResult.opinion));

			foreach (var diag in qualifying) {
				string message = diag.LatestResult.Message;
				string value = diag.presentationSetting == ColonyDiagnostic.PresentationSetting.CurrentValue
					? diag.GetCurrentValueString()
					: diag.GetAverageValueString();

				var entry = diag.name + ": ";
				if (!string.IsNullOrWhiteSpace(message))
					entry += message;
				else
					entry += OpinionWord(diag.LatestResult.opinion);
				if (!string.IsNullOrEmpty(value))
					entry += ", " + value;
				parts.Add(entry);
			}

			if (parts.Count == 0) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.DIAGNOSTICS.NO_ALERTS);
				return;
			}
			SpeechPipeline.SpeakInterrupt(string.Join(". ", parts));
		}

		private void OpenDiagnosticBrowser() {
			if (AllDiagnosticsScreen.Instance != null)
				AllDiagnosticsScreen.Instance.Show(true);
		}

		internal static string OpinionWord(ColonyDiagnostic.DiagnosticResult.Opinion opinion) {
			switch (opinion) {
				case ColonyDiagnostic.DiagnosticResult.Opinion.DuplicantThreatening:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_CRITICAL;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Bad:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_BAD;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Warning:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_WARNING;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Concern:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_CONCERN;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Suggestion:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_SUGGESTION;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Normal:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_NORMAL;
				case ColonyDiagnostic.DiagnosticResult.Opinion.Good:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_GOOD;
				default:
					return (string)STRINGS.ONIACCESS.DIAGNOSTICS.OPINION_NORMAL;
			}
		}

		private void OpenNotificationMenu() {
			if (_notificationTracker == null) return;
			if (_notificationTracker.Groups.Count == 0) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.NOTIFICATIONS.EMPTY);
				return;
			}
			HandlerStack.Push(
				new Notifications.NotificationMenuHandler(_notificationTracker));
		}

		private void OpenWorldSelector() {
			if (!DlcManager.FeatureClusterSpaceEnabled()) return;
			if (ClusterManager.Instance == null) return;
			HandlerStack.Push(new WorldSelectorHandler());
		}

		private void OpenDisinfectSettings() {
			if (OverlayScreen.Instance == null) return;
			if (OverlayScreen.Instance.mode != OverlayModes.Disease.ID) return;
			HandlerStack.Push(new DisinfectSettingsHandler());
		}

		private void OpenFastTravelMenu() {
			HandlerStack.Push(new FastTravel.FastTravelMenuHandler());
		}

		private void OnActiveToolChanged(object data) {
			var tool = data as InterfaceTool;
			if (tool == null || tool is SelectTool) return;
			if (tool is BuildTool || tool is UtilityBuildTool || tool is WireBuildTool) return;

			// Build handlers manage their own tool transitions and push
			// replacement handlers when needed. Don't interfere.
			if (HandlerStack.ActiveHandler is Build.BuildToolHandler) return;
			if (HandlerStack.ActiveHandler is Build.ActionMenuHandler) return;

			// Pop any tool handlers above us so we can push the correct one.
			HandlerStack.PopAbove(this);

			PushToolHandler(tool);
		}

		private void PushToolHandler(InterfaceTool tool) {
			if (Sandbox.SandboxToolHandler.IsSandboxTool(tool)) {
				HandlerStack.Push(new Sandbox.SandboxToolHandler());
				return;
			}
			if (tool is CopySettingsTool) {
				HandlerStack.Push(new OniAccess.Handlers.Tools.CopySettingsHandler());
				return;
			}
			if (tool is MoveToLocationTool) {
				HandlerStack.Push(new OniAccess.Handlers.Tools.MoveToLocationHandler());
				return;
			}
			if (tool is PlaceTool) {
				HandlerStack.Push(new OniAccess.Handlers.Tools.PlaceToolHandler());
				return;
			}
			if (tool is DragTool)
				HandlerStack.Push(new OniAccess.Handlers.Tools.ToolHandler());
		}

		private void ReadTooltipSummary() {
			if (!Grid.IsVisible(TileCursor.Instance.Cell)) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TILE_CURSOR.UNEXPLORED);
				return;
			}
			string summary = TooltipCapture.GetPrioritySummary(
				TileCursor.Instance.Cell);
			if (summary == null) {
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLTIP.NO_TOOLTIP);
				return;
			}
			SpeechPipeline.SpeakInterrupt(summary);
		}

	}
}
