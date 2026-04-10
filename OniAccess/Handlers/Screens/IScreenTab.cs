namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Contract for composed tabs inside tabbed screen handlers.
	/// Tab objects are owned by the parent TabbedScreenHandler — they are never
	/// pushed onto the HandlerStack. The parent delegates input to the active tab
	/// after consuming Tab/Shift+Tab for tab cycling.
	/// </summary>
	public interface IScreenTab {
		string TabName { get; }

		/// <summary>
		/// Help entries shown by ? when this tab is active.
		/// Null means no tab-specific help (parent handler's entries are used).
		/// </summary>
		System.Collections.Generic.IReadOnlyList<HelpEntry> HelpEntries { get; }
		void OnTabActivated(bool announce);
		void OnTabDeactivated();

		/// <summary>
		/// Handle one frame of input. Called from TabbedScreenHandler.Tick()
		/// after Tab has already been consumed by the parent.
		/// Returns true if a key was consumed.
		/// </summary>
		bool HandleInput();

		/// <summary>
		/// Handle Escape interception from KButtonEvent.
		/// Returns true if Escape was consumed (e.g., clearing search).
		/// </summary>
		bool HandleKeyDown(KButtonEvent e);
	}
}
