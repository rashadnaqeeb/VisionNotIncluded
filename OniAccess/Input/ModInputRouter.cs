using System.Collections.Generic;
using OniAccess.Handlers;

namespace OniAccess.Input {
	/// <summary>
	/// Single IInputHandler registered in ONI's KInputHandler tree at priority 50.
	/// Receives every KButtonEvent before PlayerController (20), KScreenManager (10),
	/// CameraController (1), and DebugHandler (-1).
	///
	/// Walks the handler stack top-to-bottom for each KButtonEvent. A handler that
	/// returns true from HandleKeyDown consumes the event. A CapturesAllInput barrier
	/// blocks unconsumed non-passthrough keys from reaching the game. If no handler
	/// consumes and no barrier exists, the key reaches the game.
	///
	/// When ModToggle is off, all events pass through untouched.
	/// </summary>
	public class ModInputRouter: IInputHandler {
		public static ModInputRouter Instance { get; private set; }

		private static readonly HashSet<Action> _globalBlockedActions = new HashSet<Action> {
			// Building categories — handled by the action tab
			Action.Plan1, Action.Plan2, Action.Plan3, Action.Plan4,
			Action.Plan5, Action.Plan6, Action.Plan7, Action.Plan8,
			Action.Plan9, Action.Plan10, Action.Plan11, Action.Plan12,
			Action.Plan13, Action.Plan14,
			// Camera pan — not usable without sight
			Action.PanUp, Action.PanDown, Action.PanLeft, Action.PanRight,
			// Building search — PlanScreen.OnKeyUp crashes on null BuildingGroupScreen
			Action.Find,
		};

		// Actions whose key went down with Command held; their release is swallowed too.
		private static readonly HashSet<Action> _commandSwallowed = new HashSet<Action>();

		public string handlerName => "OniAccess";
		public KInputHandler inputHandler { get; set; }

		private KInputController _controller;

		private ModInputRouter() {
			Instance = this;
		}

		/// <summary>
		/// Register the router in ONI's input handler tree at priority 50, on the
		/// current controller (the same pattern InputInit.Awake uses for
		/// KScreenManager). Idempotent.
		/// </summary>
		public static void Register() {
			if (Instance != null) return;
			var router = new ModInputRouter();
			router._controller = KInputManager.currentController
				?? Global.GetInputManager().GetDefaultController();
			KInputHandler.Add(router._controller, router, 50);
			Util.Log.Info("ModInputRouter registered at priority 50");
		}

		/// <summary>Take the router back out of the game's input tree (module reload).</summary>
		public static void Unregister() {
			if (Instance == null) return;
			KInputHandler.Remove(Instance._controller, Instance);
			Instance = null;
		}

		public void OnKeyDown(KButtonEvent e) {
			if (e.Consumed || !ModToggle.IsEnabled) return;
			if (IsCommandCombo(e)) {
				_commandSwallowed.Add(e.GetAction());
				e.Consumed = true;
				return;
			}
			if (IsGloballyBlocked(e)) {
				e.Consumed = true;
				return;
			}

			int count = HandlerStack.Count;
			for (int i = count - 1; i >= 0; i--) {
				var handler = HandlerStack.GetAt(i);
				if (handler == null) break;
				if (handler.HandleKeyDown(e)) return;
				if (handler.CapturesAllInput) {
					if (!IsPassThroughAction(e)) e.Consumed = true;
					return;
				}
				if (IsConsumedKey(e, handler)) {
					e.Consumed = true;
					return;
				}
			}
		}

		public void OnKeyUp(KButtonEvent e) {
			if (e.Consumed || !ModToggle.IsEnabled) return;
			if (_commandSwallowed.Remove(e.GetAction()) || IsCommandCombo(e)) {
				e.Consumed = true;
				return;
			}
			if (IsGloballyBlocked(e)) {
				e.Consumed = true;
				return;
			}

			int count = HandlerStack.Count;
			for (int i = count - 1; i >= 0; i--) {
				var handler = HandlerStack.GetAt(i);
				if (handler == null) break;
				if (handler.CapturesAllInput) {
					if (!IsPassThroughAction(e)) e.Consumed = true;
					return;
				}
				if (IsConsumedKey(e, handler)) {
					e.Consumed = true;
					return;
				}
			}
		}

		/// <summary>
		/// Check if the event matches any action currently bound to the handler's
		/// consumed physical keys. Iterates the global binding table to find every
		/// action mapped to each consumed key+modifier, then checks the event against
		/// those actions. This is rebind-safe: if the player moves an action to a
		/// different key, only the new key triggers that action, and only our declared
		/// physical keys get consumed regardless of what the game binds to them.
		/// </summary>
		private static bool IsConsumedKey(KButtonEvent e, IAccessHandler handler) {
			var keys = handler.ConsumedKeys;
			if (keys.Count == 0) return false;
			var bindings = GameInputMapping.KeyBindings;
			for (int i = 0; i < keys.Count; i++) {
				for (int j = 0; j < bindings.Length; j++) {
					if (bindings[j].mKeyCode == keys[i].KeyCode
						&& bindings[j].mModifier == keys[i].Modifier
						&& e.IsAction(bindings[j].mAction))
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// A key pressed with Command on Mac. The game cannot see Command, so the
		/// combo would reach it as the bare key (Command+F would open Consumables).
		/// Every Command combo belongs to the mod or to macOS, so none reach the game.
		/// </summary>
		private static bool IsCommandCombo(KButtonEvent e) {
			return InputUtil.IsMac && InputUtil.CommandHeld() && !IsPassThroughAction(e);
		}

		/// <summary>
		/// Check if the event is an action that should pass through even in
		/// full-capture mode. Includes mouse/zoom (pitfall #6) and Escape.
		/// Escape must reach game screens so KScreen.OnKeyDown can call Deactivate(),
		/// which fires our Harmony patch, which pops the handler. Handlers that need
		/// to intercept Escape consume it in HandleKeyDown before this check runs.
		/// </summary>
		private static bool IsGloballyBlocked(KButtonEvent e) {
			foreach (var action in _globalBlockedActions) {
				if (e.IsAction(action)) return true;
			}
			return false;
		}

		private static bool IsPassThroughAction(KButtonEvent e) {
			return e.IsAction(Action.MouseLeft) || e.IsAction(Action.MouseRight)
				|| e.IsAction(Action.MouseMiddle) || e.IsAction(Action.ShiftMouseLeft)
				|| e.IsAction(Action.ZoomIn) || e.IsAction(Action.ZoomOut)
				|| e.IsAction(Action.Escape);
		}
	}
}
