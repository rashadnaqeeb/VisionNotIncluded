using System.Collections.Generic;

namespace OniAccess.Handlers {
	/// <summary>
	/// Manages a stack of IAccessHandler instances.
	///
	/// KeyPoller and ModInputRouter walk the stack top-to-bottom using GetAt() + Count.
	/// Each handler receives Tick() / HandleKeyDown() until one consumes the event or
	/// a CapturesAllInput barrier is reached. Barriers stop the walk (inclusive).
	///
	/// Push/Pop/Replace manage the handler lifecycle with OnActivate/OnDeactivate
	/// callbacks. OnDeactivate only fires when a handler is popped off.
	///
	/// The stack represents the current input context hierarchy:
	/// e.g., [BaselineHandler] or [BaselineHandler, BuildHandler] or [BaselineHandler, BuildHandler, HelpHandler].
	///
	/// All methods are safe for null/empty stack (no exceptions thrown).
	/// </summary>
	public static class HandlerStack {
		private static readonly List<IAccessHandler> _stack = new List<IAccessHandler>();
		private static readonly List<int> _pushFrames = new List<int>();

		/// <summary>
		/// Frame counter source. Defaults to UnityEngine.Time.frameCount.
		/// Tests replace this to avoid native Unity calls.
		/// </summary>
		internal static System.Func<int> FrameSource = () => UnityEngine.Time.frameCount;

		/// <summary>
		/// The currently active handler (top of stack), or null if stack is empty.
		/// </summary>
		public static IAccessHandler ActiveHandler =>
			_stack.Count > 0 ? _stack[_stack.Count - 1] : null;

		/// <summary>
		/// Number of handlers on the stack. Used with GetAt() for top-to-bottom iteration.
		/// </summary>
		public static int Count => _stack.Count;

		/// <summary>
		/// Return the handler at the given index (0 = bottom, Count-1 = top).
		/// Returns null for out-of-range indices, safe if stack mutates mid-loop.
		/// </summary>
		public static IAccessHandler GetAt(int index) {
			if (index < 0 || index >= _stack.Count) return null;
			return _stack[index];
		}

		/// <summary>
		/// Push a handler onto the stack, making it the top handler.
		/// Calls handler.OnActivate(). Does NOT call OnDeactivate on the previous
		/// handler. OnDeactivate only fires on Pop.
		/// </summary>
		public static void Push(IAccessHandler handler) {
			if (handler == null) {
				Util.Log.Warn("HandlerStack.Push called with null handler");
				return;
			}

			try {
				handler.OnActivate();
			} catch (System.Exception ex) {
				Util.Log.Error($"HandlerStack.Push: {handler.DisplayName} failed: {ex}");
				Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SPEECH.HANDLER_FAILED, handler.DisplayName));
				return;
			}
			_stack.Add(handler);
			_pushFrames.Add(FrameSource());
			Util.Log.Debug($"HandlerStack.Push: {handler.DisplayName} (depth={_stack.Count})");
		}

		/// <summary>
		/// Remove the top handler from the stack, calling its OnDeactivate.
		/// If a handler is now exposed underneath, calls its OnActivate.
		/// </summary>
		public static void Pop() {
			if (_stack.Count == 0) {
				Util.Log.Warn("HandlerStack.Pop called on empty stack");
				return;
			}

			var removed = _stack[_stack.Count - 1];
			_stack.RemoveAt(_stack.Count - 1);
			if (_pushFrames.Count > _stack.Count)
				_pushFrames.RemoveAt(_stack.Count);
			try {
				removed.OnDeactivate();
			} catch (System.Exception ex) {
				Util.Log.Warn($"HandlerStack.Pop: OnDeactivate of {removed.DisplayName} failed: {ex}");
			}
			Util.Log.Debug($"HandlerStack.Pop: {removed.DisplayName} (depth={_stack.Count})");

			// If a handler is now exposed underneath, reactivate it
			var newActive = ActiveHandler;
			if (newActive != null) {
				try {
					newActive.OnActivate();
				} catch (System.Exception ex) {
					Util.Log.Error($"HandlerStack.Pop: reactivation of {newActive.DisplayName} failed: {ex}");
					Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SPEECH.HANDLER_FAILED, newActive.DisplayName));
					_stack.RemoveAt(_stack.Count - 1);
					if (_pushFrames.Count > _stack.Count)
						_pushFrames.RemoveAt(_stack.Count);
					return;
				}
				Util.Log.Debug($"HandlerStack.Pop: reactivated {newActive.DisplayName}");
			}
		}

		/// <summary>
		/// Replace the current active handler with a new one.
		/// Pops the current handler (calling OnDeactivate), then pushes the new handler
		/// (calling OnActivate). Used by ContextDetector when switching between
		/// same-level handlers (e.g., BaselineHandler to MenuHandler).
		/// If stack is empty, equivalent to Push.
		/// </summary>
		public static void Replace(IAccessHandler handler) {
			if (handler == null) {
				Util.Log.Warn("HandlerStack.Replace called with null handler");
				return;
			}

			if (_stack.Count > 0) {
				var removed = _stack[_stack.Count - 1];
				_stack.RemoveAt(_stack.Count - 1);
				if (_pushFrames.Count > _stack.Count)
					_pushFrames.RemoveAt(_stack.Count);
				try {
					removed.OnDeactivate();
				} catch (System.Exception ex) {
					Util.Log.Warn($"HandlerStack.Replace: OnDeactivate of {removed.DisplayName} failed: {ex}");
				}
				Util.Log.Debug($"HandlerStack.Replace: removed {removed.DisplayName}");
			}

			try {
				handler.OnActivate();
			} catch (System.Exception ex) {
				Util.Log.Error($"HandlerStack.Replace: {handler.DisplayName} failed: {ex}");
				Speech.SpeechPipeline.SpeakInterrupt(string.Format(STRINGS.ONIACCESS.SPEECH.HANDLER_FAILED, handler.DisplayName));
				return;
			}
			_stack.Add(handler);
			_pushFrames.Add(FrameSource());
			Util.Log.Debug($"HandlerStack.Replace: activated {handler.DisplayName} (depth={_stack.Count})");
		}

		/// <summary>
		/// Pop all handlers above the given handler, calling OnDeactivate on each.
		/// Does not reactivate intermediates. Does not pop the target handler itself.
		/// Returns false if the handler is not on the stack.
		/// </summary>
		public static bool PopAbove(IAccessHandler target) {
			int targetIndex = _stack.IndexOf(target);
			if (targetIndex < 0) return false;

			for (int i = _stack.Count - 1; i > targetIndex; i--) {
				var removed = _stack[i];
				_stack.RemoveAt(i);
				if (i < _pushFrames.Count)
					_pushFrames.RemoveAt(i);
				try {
					removed.OnDeactivate();
				} catch (System.Exception ex) {
					Util.Log.Warn($"HandlerStack.PopAbove: OnDeactivate of {removed.DisplayName} failed: {ex}");
				}
				Util.Log.Debug($"HandlerStack.PopAbove: removed {removed.DisplayName}");
			}
			return true;
		}

		/// <summary>
		/// Deactivate all handlers top-to-bottom and clear the stack.
		/// Used by ModToggle toggle OFF.
		/// </summary>
		public static void DeactivateAll() {
			for (int i = _stack.Count - 1; i >= 0; i--) {
				try {
					_stack[i].OnDeactivate();
				} catch (System.Exception ex) {
					Util.Log.Warn($"HandlerStack.DeactivateAll: OnDeactivate of {_stack[i].DisplayName} failed: {ex}");
				}
				Util.Log.Debug($"HandlerStack.DeactivateAll: deactivated {_stack[i].DisplayName}");
			}
			_stack.Clear();
			_pushFrames.Clear();
		}

		/// <summary>
		/// Clear the stack without calling any callbacks.
		/// Used for emergency cleanup only.
		/// </summary>
		public static void Clear() {
			_stack.Clear();
			_pushFrames.Clear();
			Util.Log.Debug("HandlerStack.Clear: stack cleared without callbacks");
		}

		/// <summary>
		/// True when a BaseScreenHandler for the given KScreen is anywhere on the stack.
		/// </summary>
		public static bool ContainsScreen(KScreen screen) {
			for (int i = _stack.Count - 1; i >= 0; i--) {
				if (_stack[i] is BaseScreenHandler sh && sh.Screen == screen) return true;
			}
			return false;
		}

		/// <summary>
		/// Remove a BaseScreenHandler whose Screen matches the given KScreen,
		/// regardless of stack position. Called by OnScreenDeactivating when
		/// the handler is buried under other handlers (not on top).
		/// Calls OnDeactivate on the removed handler.
		/// </summary>
		public static bool RemoveByScreen(KScreen screen) {
			for (int i = _stack.Count - 1; i >= 0; i--) {
				if (_stack[i] is BaseScreenHandler sh && sh.Screen == screen) {
					_stack.RemoveAt(i);
					if (i < _pushFrames.Count)
						_pushFrames.RemoveAt(i);
					try {
						sh.OnDeactivate();
					} catch (System.Exception ex) {
						Util.Log.Warn($"HandlerStack.RemoveByScreen: OnDeactivate failed for {sh.DisplayName}: {ex.Message}");
					}
					Util.Log.Debug($"HandlerStack.RemoveByScreen: removed {sh.DisplayName} at index {i}");
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Remove BaseScreenHandlers whose KScreen has been destroyed or hidden
		/// without firing Deactivate. Walks the entire stack, not just the top,
		/// so stale handlers buried under non-capturing handlers are cleaned up.
		/// Calls OnDeactivate on each removed handler.
		/// </summary>
		public static void RemoveStaleHandlers() {
			for (int i = _stack.Count - 1; i >= 0; i--) {
				if (!(_stack[i] is BaseScreenHandler sh)) continue;
				if (System.Object.ReferenceEquals(sh.Screen, null)) continue;
				if (sh.Screen != null && sh.Screen.gameObject.activeInHierarchy) continue;
				// Show-patched screens manage lifecycle via Show patches. If inactive
				// via SetActive(false) rather than Show(false), it's a temporary hide
				// (e.g., PauseScreen hides itself during save confirmation dialogs).
				if (sh.Screen != null && ContextDetector.IsShowPatched(sh.Screen.GetType())) continue;
				// Grace period: skip handlers pushed this frame or last frame.
				// Screens may be transiently inactive during modal transitions.
				if (i < _pushFrames.Count && FrameSource() - _pushFrames[i] <= 1) continue;

				_stack.RemoveAt(i);
				if (i < _pushFrames.Count)
					_pushFrames.RemoveAt(i);
				try {
					sh.OnDeactivate();
				} catch (System.Exception ex) {
					Util.Log.Warn($"HandlerStack.RemoveStaleHandlers: OnDeactivate failed for {sh.DisplayName}: {ex.Message}");
				}
				Util.Log.Debug($"HandlerStack.RemoveStaleHandlers: removed {sh.DisplayName} at index {i}");
			}
		}

		/// <summary>
		/// Walk the stack top-to-bottom (mirroring KeyPoller's tick walk),
		/// collecting HelpEntries from each reachable handler. Stops after
		/// a CapturesAllInput barrier (inclusive). Deduplicates by KeyName —
		/// topmost handler wins.
		/// </summary>
		public static System.Collections.Generic.IReadOnlyList<HelpEntry> CollectHelpEntries() {
			var entries = new System.Collections.Generic.List<HelpEntry>();
			var seenKeys = new System.Collections.Generic.HashSet<string>();
			for (int i = _stack.Count - 1; i >= 0; i--) {
				var handler = _stack[i];
				if (handler.HelpEntries != null) {
					foreach (var entry in handler.HelpEntries) {
						if (entry.Available && seenKeys.Add(entry.KeyName))
							entries.Add(entry);
					}
				}
				if (handler.CapturesAllInput) break;
			}
			return entries.AsReadOnly();
		}
	}
}
