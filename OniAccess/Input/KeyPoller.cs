using OniAccess.Handlers;
using OniAccess.Speech;

namespace OniAccess.Input {
	/// <summary>
	/// MonoBehaviour that drives the per-frame Tick() on the active handler.
	///
	/// Also handles the Ctrl+Shift+F12 toggle key, which must work even when
	/// the mod is off (the only key active in ModToggle OFF state).
	///
	/// All UnityEngine.Input references are fully qualified per Phase 1 decision:
	/// bare Input resolves to the OniAccess.Input namespace, not UnityEngine.Input.
	/// </summary>
	public class KeyPoller: UnityEngine.MonoBehaviour {
		public static KeyPoller Instance { get; private set; }

		private bool _startupDone;

		private void Awake() {
			Instance = this;
		}

		private void Update() {
			// Toggle key: Ctrl+Shift+F12 -- ALWAYS check, even when mod is off.
			// This is the only key that works when mod is disabled.
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12)
				&& InputUtil.CtrlHeld() && InputUtil.ShiftHeld()) {
				ModToggle.Toggle();
				return; // Don't process Ctrl+Shift+F12 further this frame
			}

			// When mod is off, don't process anything else
			if (!ModToggle.IsEnabled) return;

			// Control silences the system voice, as it silences a screen reader.
			// Not consumed: the same key starts the mod's Ctrl combos.
			// Screen reader backends stop themselves, so only a voice-controlled
			// backend gets the call.
			if ((UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftControl)
					|| UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightControl))
				&& SpeechOutputSelector.VoiceControlledBackend != null) {
				SpeechEngine.Stop();
			}

			// One-time: MainMenu.Activate fires before Harmony patches, so our
			// KScreen.Activate postfix misses it. Find it on first frame.
			if (!_startupDone) {
				_startupDone = true;
				try {
					var mainMenu = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
					if (mainMenu != null) {
						Util.Log.Debug("Startup screen detect: MainMenu found");
						ContextDetector.OnScreenActivated(mainMenu);
					}
				} catch (System.Exception ex) {
					Util.Log.Warn($"Startup screen detect: {ex.Message}");
				}
			}

			// Remove screen handlers whose KScreen has been destroyed or hidden
			// without firing Deactivate. Walks the entire stack so stale handlers
			// buried under non-capturing handlers (e.g., MainMenu under TileCursorHandler)
			// are cleaned up, not just the top.
			HandlerStack.RemoveStaleHandlers();

			// F12 (bare): open config screen
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12)
				&& !InputUtil.AnyModifierHeld()
				&& !(HandlerStack.ActiveHandler is ConfigHandler)) {
				HandlerStack.Push(new ConfigHandler());
				return;
			}

			// Shift+/ (?): open help with entries from all reachable handlers.
			// Centralized here to prevent double-push when layered non-capturing
			// handlers both detect the key in the same frame.
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Slash)
				&& InputUtil.ShiftHeld() && !InputUtil.AnyCtrlHeld()
				&& !(HandlerStack.ActiveHandler is HelpHandler)) {
				var entries = HandlerStack.CollectHelpEntries();
				HandlerStack.Push(new HelpHandler(entries));
				return;
			}

			// Walk the stack top-to-bottom, ticking each handler.
			// Stop after any CapturesAllInput barrier (inclusive).
			// Also stop if a Tick mutates the stack (Push/Pop/Replace),
			// otherwise lower handlers see the same keypress in the same frame.
			int count = HandlerStack.Count;
			for (int i = count - 1; i >= 0; i--) {
				var handler = HandlerStack.GetAt(i);
				if (handler == null) break;
				if (handler.Tick()) break;
				if (HandlerStack.Count != count || HandlerStack.GetAt(i) != handler) break;
				if (handler.CapturesAllInput) break;
			}
		}

		private void OnDestroy() {
			if (Instance == this) Instance = null;
		}
	}
}
