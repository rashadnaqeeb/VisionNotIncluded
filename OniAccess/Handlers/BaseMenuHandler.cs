using System.Collections.Generic;

using OniAccess.Input;

namespace OniAccess.Handlers {
	/// <summary>
	/// Reusable 1D list navigation base extending BaseScreenHandler.
	/// Provides arrow-key navigation with wrap-around, Home/End, Enter activation,
	/// Left/Right adjustment, Tab stubs, and A-Z type-ahead search.
	///
	/// Accepts a null KScreen because it serves both screen-bound widget handlers
	/// (via BaseWidgetHandler) and lightweight handlers like HelpHandler
	/// that have no KScreen.
	///
	/// Subclasses implement ItemCount, GetItemLabel, and SpeakCurrentItem to describe
	/// their list. Override ActivateCurrentItem, AdjustCurrentItem, and NavigateTab*
	/// for interaction behavior.
	/// </summary>
	public abstract class BaseMenuHandler: BaseScreenHandler, ISearchable {
		private int _currentIndex;
		protected virtual int CurrentIndex { get => _currentIndex; set => _currentIndex = value; }
		protected readonly TypeAheadSearch _search = new TypeAheadSearch();
		private int _searchSuppressFrame = -1;

		protected BaseMenuHandler(KScreen screen = null) : base(screen) { }

		/// <summary>
		/// Menus are modal: block all input from reaching handlers below.
		/// </summary>
		public override bool CapturesAllInput => true;

		// ========================================
		// ABSTRACT: LIST DESCRIPTION
		// ========================================

		/// <summary>
		/// Number of items in the navigable list.
		/// </summary>
		public abstract int ItemCount { get; }

		/// <summary>
		/// Searchable/speakable label for the item at the given index.
		/// </summary>
		public abstract string GetItemLabel(int index);

		/// <summary>
		/// Speak the currently focused item via SpeakInterrupt.
		/// </summary>
		public abstract void SpeakCurrentItem(string parentContext = null);

		// ========================================
		// VIRTUAL HOOKS
		// ========================================

		/// <summary>
		/// Whether the item at the given index is valid for navigation.
		/// Default returns true. Widget handlers override to check component state.
		/// </summary>
		protected virtual bool IsItemValid(int index) => true;

		/// <summary>
		/// Activate the currently focused item (Enter key). No-op default.
		/// </summary>
		protected virtual void ActivateCurrentItem() { }

		/// <summary>
		/// Adjust the currently focused item's value (Left/Right). No-op default.
		/// </summary>
		protected virtual void AdjustCurrentItem(int direction, int stepLevel) { }

		/// <summary>
		/// Navigate to next tab section. No-op default for non-tabbed screens.
		/// </summary>
		protected virtual void NavigateTabForward() { }

		/// <summary>
		/// Navigate to previous tab section. No-op default.
		/// </summary>
		protected virtual void NavigateTabBackward() { }

		/// <summary>
		/// Jump to the next parent group. No-op default.
		/// </summary>
		protected virtual void JumpNextGroup() { }

		/// <summary>
		/// Jump to the previous parent group. No-op default.
		/// </summary>
		protected virtual void JumpPrevGroup() { }

		// ========================================
		// COMPOSABLE HELP ENTRY LISTS
		// ========================================

		/// <summary>
		/// Help entries for menu-specific features (search).
		/// </summary>
		protected static readonly List<HelpEntry> MenuHelpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
		};

		/// <summary>
		/// Help entries for 1D list navigation.
		/// </summary>
		protected static readonly List<HelpEntry> ListNavHelpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE),
			new HelpEntry("Shift+Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE_LARGE),
			new HelpEntry("Ctrl+Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE_LARGER),
			new HelpEntry("Ctrl+Shift+Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE_LARGEST),
		};

		/// <summary>
		/// Build a help entry list combining menu + list-nav entries with optional extras.
		/// </summary>
		protected IReadOnlyList<HelpEntry> BuildHelpEntries(params HelpEntry[] extra) {
			var list = new List<HelpEntry>();
			list.AddRange(MenuHelpEntries);
			list.AddRange(ListNavHelpEntries);
			list.AddRange(extra);
			return list;
		}

		// ========================================
		// NAVIGATION METHODS
		// ========================================

		/// <summary>
		/// Move to next item with wrap-around. Skips invalid items.
		/// </summary>
		protected virtual void NavigateNext() {
			if (ItemCount == 0) return;
			int start = CurrentIndex;
			for (int i = 0; i < ItemCount; i++) {
				int candidate = (start + 1 + i) % ItemCount;
				if (IsItemValid(candidate)) {
					if (candidate == start) return;
					bool wrapped = candidate <= CurrentIndex;
					CurrentIndex = candidate;
					if (wrapped) PlaySound("HUD_Click");
					else PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
		}

		/// <summary>
		/// Move to previous item with wrap-around. Skips invalid items.
		/// </summary>
		protected virtual void NavigatePrev() {
			if (ItemCount == 0) return;
			int start = CurrentIndex;
			for (int i = 0; i < ItemCount; i++) {
				int candidate = (start - 1 - i + ItemCount) % ItemCount;
				if (IsItemValid(candidate)) {
					if (candidate == start) return;
					bool wrapped = candidate >= CurrentIndex;
					CurrentIndex = candidate;
					if (wrapped) PlaySound("HUD_Click");
					else PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
		}

		/// <summary>
		/// Jump to first valid item.
		/// </summary>
		protected virtual void NavigateFirst() {
			if (ItemCount == 0) return;
			for (int i = 0; i < ItemCount; i++) {
				if (IsItemValid(i)) {
					CurrentIndex = i;
					PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
		}

		/// <summary>
		/// Jump to last valid item.
		/// </summary>
		protected virtual void NavigateLast() {
			if (ItemCount == 0) return;
			for (int i = ItemCount - 1; i >= 0; i--) {
				if (IsItemValid(i)) {
					CurrentIndex = i;
					PlaySound("HUD_Mouseover");
					SpeakCurrentItem();
					return;
				}
			}
		}

		// ========================================
		// SEARCH
		// ========================================

		private static readonly UnityEngine.KeyCode[] _searchNavKeys = {
			UnityEngine.KeyCode.UpArrow, UnityEngine.KeyCode.DownArrow,
			UnityEngine.KeyCode.Home, UnityEngine.KeyCode.End,
			UnityEngine.KeyCode.Backspace,
		};

		/// <summary>
		/// Route keys through _search.HandleKey before standard navigation.
		/// Returns true if the search consumed the key.
		/// </summary>
		protected bool TryRouteToSearch(bool ctrlHeld, bool altHeld) {
			// Skip type-ahead on the frame we activated to avoid consuming
			// the hotkey that opened the screen (e.g., R for Research)
			if (UnityEngine.Time.frameCount == _searchSuppressFrame)
				return false;

			bool consumed = false;

			// Letters (no modifiers) — start or continue search
			if (!ctrlHeld && !altHeld) {
				string inputStr = UnityEngine.Input.inputString;
				for (int i = 0; i < inputStr.Length; i++) {
					char c = inputStr[i];
					if (char.IsLetter(c))
						consumed |= _search.HandleChar(c, this);
				}
			}

			// Space captured by search when active (otherwise passes through to handler)
			if (_search.IsSearchActive && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space))
				consumed |= _search.HandleKey(UnityEngine.KeyCode.Space, ctrlHeld, altHeld, this);

			// Navigation keys captured by search when active
			for (int i = 0; i < _searchNavKeys.Length; i++) {
				if (UnityEngine.Input.GetKeyDown(_searchNavKeys[i]))
					consumed |= _search.HandleKey(_searchNavKeys[i], ctrlHeld, altHeld, this);
			}

			return consumed;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		/// <summary>
		/// Suppress type-ahead search for the current frame.
		/// Prevents the hotkey that opened this handler from triggering search.
		/// </summary>
		protected void SuppressSearchThisFrame() {
			_searchSuppressFrame = UnityEngine.Time.frameCount;
		}

		/// <summary>
		/// Speaks DisplayName (via BaseScreenHandler), resets cursor and search.
		/// </summary>
		public override void OnActivate() {
			base.OnActivate();
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			// Force IME composition on so users with a CJK IME (Chinese, Japanese, Korean)
			// can compose characters into type-ahead. No effect when no IME is selected
			// at the OS level — Latin keystrokes still arrive via Input.inputString.
			UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;
		}

		/// <summary>
		/// Resets cursor and search.
		/// </summary>
		public override void OnDeactivate() {
			base.OnDeactivate();
			CurrentIndex = 0;
			_search.Clear();
			UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.Auto;
		}

		// ========================================
		// TICK: KEY DETECTION
		// ========================================

		/// <summary>
		/// Per-frame key detection for navigation and type-ahead search.
		/// Subclasses should call base.Tick() to get navigation handling.
		/// </summary>
		public override bool Tick() {
			if (base.Tick()) return true;

			// During IME composition, every key belongs to the IME (composing pinyin,
			// navigating candidates, etc.). Consume the frame so list navigation doesn't
			// fire alongside candidate selection.
			if (UnityEngine.Input.compositionString.Length > 0)
				return true;

			bool ctrlHeld = InputUtil.CtrlHeld();
			bool altHeld = InputUtil.AltHeld();

			if (TryRouteToSearch(ctrlHeld, altHeld))
				return true;

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				if (ctrlHeld) JumpNextGroup();
				else NavigateNext();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				if (ctrlHeld) JumpPrevGroup();
				else NavigatePrev();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Home)) {
				NavigateFirst();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.End)) {
				NavigateLast();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)) {
				ActivateCurrentItem();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)) {
				if (InputUtil.ShiftHeld())
					NavigateTabBackward();
				else
					NavigateTabForward();
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
				HandleLeftRight(-1, InputUtil.GetStepLevel());
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				HandleLeftRight(1, InputUtil.GetStepLevel());
				return true;
			}
			return false;
		}

		/// <summary>
		/// Handle Left/Right arrow keys. Default delegates to AdjustCurrentItem.
		/// Overridden by NavTreeHandler for drill-down/go-back behavior.
		/// </summary>
		protected virtual void HandleLeftRight(int direction, int stepLevel) {
			AdjustCurrentItem(direction, stepLevel);
		}

		/// <summary>
		/// Intercept Escape via KButtonEvent when search is active.
		/// Clears search instead of letting the game close the screen.
		/// </summary>
		public override bool HandleKeyDown(KButtonEvent e) {
			if (_search.IsSearchActive && e.TryConsume(Action.Escape)) {
				_search.Clear();
				Speech.SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.SEARCH.CLEARED);
				return true;
			}

			return false;
		}

		// ========================================
		// ISearchable IMPLEMENTATION
		// ========================================

		public int SearchItemCount => ItemCount;

		public string GetSearchLabel(int index) {
			if (index < 0 || index >= ItemCount) return null;
			return Speech.TextFilter.FilterForSpeech(GetItemLabel(index));
		}

		public void SearchMoveTo(int index) {
			if (index < 0 || index >= ItemCount) return;
			CurrentIndex = index;
			SpeakCurrentItem();
		}
	}
}
