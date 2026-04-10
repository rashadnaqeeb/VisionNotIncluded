using OniAccess.Input;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Base class for screen handlers that compose multiple IScreenTab objects
	/// and cycle between them with Tab/Shift+Tab. Each tab is an autonomous
	/// object that owns its own input handling and announcements.
	///
	/// Not all handlers with Tab cycling use this. Handlers that cycle an index
	/// over their own content (DetailsScreen sections, ReportScreen days,
	/// MinionSelect slots, etc.) don't have composed tab objects and extend
	/// their navigation base classes directly.
	///
	/// Provides tab array management, CycleTab with wrap detection, and default
	/// Tick/HandleKeyDown/OnDeactivate that delegate to the active tab.
	///
	/// Subclasses set _tabs in their constructor and override OnActivate to
	/// select the initial tab. Override HandleTabKey for custom Tab behavior
	/// (e.g., CodexScreenHandler) and HandleKeyDown for custom Escape handling.
	/// </summary>
	public abstract class TabbedScreenHandler: BaseScreenHandler {
		private IScreenTab[] _tabArray;
		private int _activeTabIndex;

		private static readonly HelpEntry _tabSwitchHelp =
			new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_PANEL);

		protected TabbedScreenHandler(KScreen screen) : base(screen) { }

		/// <summary>
		/// Returns the active tab's help entries plus Tab/Shift+Tab.
		/// Subclasses can override to add screen-level entries.
		/// </summary>
		public override System.Collections.Generic.IReadOnlyList<HelpEntry> HelpEntries {
			get {
				var tabEntries = _tabArray[_activeTabIndex].HelpEntries;
				if (tabEntries == null)
					return new System.Collections.Generic.List<HelpEntry> { _tabSwitchHelp };
				var entries = new System.Collections.Generic.List<HelpEntry>(tabEntries);
				entries.Add(_tabSwitchHelp);
				return entries;
			}
		}

		/// <summary>
		/// Set by subclass constructors after creating tab objects.
		/// </summary>
		protected void SetTabs(params IScreenTab[] tabs) {
			_tabArray = tabs;
		}

		protected int ActiveTabIndex {
			get => _activeTabIndex;
			set => _activeTabIndex = value;
		}

		/// <summary>
		/// Deactivate the current tab before switching to a new one.
		/// Used by subclass jump methods (e.g., JumpToTreeTab).
		/// </summary>
		protected void DeactivateCurrentTab() {
			_tabArray[_activeTabIndex].OnTabDeactivated();
		}

		/// <summary>
		/// Activate the current tab after switching ActiveTabIndex.
		/// Used by subclass jump methods after setting the new index.
		/// </summary>
		protected void ActivateCurrentTab(bool announce) {
			_tabArray[_activeTabIndex].OnTabActivated(announce);
		}

		public override void OnDeactivate() {
			_tabArray[_activeTabIndex].OnTabDeactivated();
			base.OnDeactivate();
		}

		public override bool Tick() {
			if (base.Tick()) return true;

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab)) {
				return HandleTabKey();
			}

			return _tabArray[_activeTabIndex].HandleInput();
		}

		/// <summary>
		/// Handle the Tab key press. Default cycles tabs via CycleTab.
		/// Override for screens with custom Tab behavior.
		/// </summary>
		protected virtual bool HandleTabKey() {
			int dir = InputUtil.ShiftHeld() ? -1 : 1;
			CycleTab(dir);
			return true;
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			return _tabArray[_activeTabIndex].HandleKeyDown(e);
		}

		protected void CycleTab(int direction) {
			_tabArray[_activeTabIndex].OnTabDeactivated();
			int next = (_activeTabIndex + direction + _tabArray.Length) % _tabArray.Length;
			bool wrapped = direction > 0 ? next <= _activeTabIndex : next >= _activeTabIndex;
			_activeTabIndex = next;
			if (wrapped) PlaySound("HUD_Click");
			else PlaySound("HUD_Mouseover");
			_tabArray[_activeTabIndex].OnTabActivated(announce: true);
		}
	}
}
