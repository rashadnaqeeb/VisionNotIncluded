using System.Collections.Generic;

namespace OniAccess.Handlers {
	/// <summary>
	/// Abstract base for ALL screen handlers. Provides only the infrastructure
	/// that every screen type shares:
	/// - Screen reference for ContextDetector matching
	/// - Display name spoken on activation
	/// - CapturesAllInput (all screens block input fallthrough)
	///
	/// BaseMenuHandler extends this with widget lists, 1D navigation, type-ahead
	/// search, tooltip reading, and widget interaction.
	/// Future 2D grid handlers extend ScreenHandler directly with their own
	/// state (cursor position, tile data) without inheriting menu-specific baggage.
	///
	/// Per locked decisions:
	/// - CapturesAllInput = true for all screen handlers
	/// - Name first, vary early: DisplayName is spoken on activation
	/// </summary>
	public abstract class BaseScreenHandler: IAccessHandler {
		protected KScreen _screen;

		/// <summary>
		/// The KScreen this handler manages. Used by ContextDetector to match
		/// a deactivating screen to its handler for correct Pop behavior.
		/// </summary>
		public KScreen Screen => _screen;

		/// <summary>
		/// Display name spoken on activation (e.g., "Options", "Pause").
		/// Per locked decision: name first, vary early.
		/// </summary>
		public abstract string DisplayName { get; }

		/// <summary>
		/// Help entries for ? navigable help list. Subclasses compose from
		/// screen-type-specific entry lists (MenuHelpEntries, ListNavHelpEntries, etc.).
		/// </summary>
		public abstract IReadOnlyList<HelpEntry> HelpEntries { get; }

		/// <summary>
		/// Whether this handler blocks input from reaching handlers below it on the stack.
		/// Menus return true (modal); grid/world handlers return false (non-modal).
		/// </summary>
		public abstract bool CapturesAllInput { get; }

		private static readonly IReadOnlyList<ConsumedKey> _noKeys = System.Array.Empty<ConsumedKey>();
		public virtual IReadOnlyList<ConsumedKey> ConsumedKeys => _noKeys;

		/// <summary>
		/// Help entry for the Alt+Up/Down line reviewer. Shared by the UI base
		/// handlers that wire in <see cref="TryLineReview"/>.
		/// </summary>
		protected static readonly HelpEntry LineReviewHelpEntry =
			new HelpEntry("Alt+Up/Down", STRINGS.ONIACCESS.HELP.REVIEW_LINES);

		// ========================================
		// CONSTRUCTOR
		// ========================================

		protected BaseScreenHandler(KScreen screen = null) {
			_screen = screen;
		}

		// ========================================
		// IAccessHandler IMPLEMENTATION
		// ========================================

		/// <summary>
		/// Called when this handler becomes active on the stack.
		/// Speaks the screen name. Subclasses extend for additional setup.
		/// </summary>
		public virtual void OnActivate() {
			Speech.SpeechPipeline.SpeakInterrupt(DisplayName);
		}

		/// <summary>
		/// Called when this handler is popped off the stack.
		/// </summary>
		public virtual void OnDeactivate() {
		}

		/// <summary>
		/// Per-frame key detection. ? help is handled centrally by KeyPoller.
		/// Subclasses override for screen-specific key handling.
		/// </summary>
		public virtual bool Tick() => false;

		/// <summary>
		/// The current navigation focus rendered as an announcement string, for the
		/// line reviewer to split and step through. Returns null when nothing is
		/// focused. Overridden by the UI base handlers (menu, table, tabbed) to hand
		/// back the same text the user last heard for the focused item; the default
		/// (null) leaves world/map handlers without review, which is intended.
		/// Internal (not protected) so TabbedScreenHandler can pull it from a tab that
		/// is itself a handler.
		/// </summary>
		internal virtual string GetReviewContent() => null;

		/// <summary>
		/// Identity of the current focus, for the line reviewer to tell a move apart
		/// from a live value change: the cursor rewinds when this changes, not when
		/// GetReviewContent's text changes. Overridden alongside GetReviewContent
		/// (the focused index, cell, or node). Must stay stable while the same item
		/// is focused even as its value ticks.
		/// </summary>
		internal virtual object GetReviewFocusKey() => null;

		/// <summary>
		/// Alt+Up / Alt+Down step through the current focused item's announcement one
		/// line at a time (see <see cref="Speech.LineReview"/>). Called from the Tick
		/// of the UI base handlers (menu, table, tabbed) before their own arrow
		/// handling. Deliberately not wired into the world map or cluster map
		/// handlers, where the arrow keys drive cursor movement. Returns true when it
		/// consumes the key.
		/// </summary>
		protected bool TryLineReview() {
			if (!Input.InputUtil.AltHeld() || Input.InputUtil.AnyCtrlHeld())
				return false;
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				Speech.LineReview.Step(GetReviewContent(), GetReviewFocusKey(), 1);
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				Speech.LineReview.Step(GetReviewContent(), GetReviewFocusKey(), -1);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Handle Escape interception from ONI's KButtonEvent system.
		/// Default: pass through (let the game close the screen, which pops
		/// the handler via Harmony patch).
		/// </summary>
		public virtual bool HandleKeyDown(KButtonEvent e) {
			return false;
		}

		// ========================================
		// SOUNDS
		// ========================================

		protected internal static void PlaySound(string clipName) {
			try {
				KFMOD.PlayUISound(GlobalAssets.GetSound(clipName));
			} catch (System.Exception ex) {
				Util.Log.Warn($"PlaySound({clipName}) failed: {ex.Message}");
			}
		}
	}
}
