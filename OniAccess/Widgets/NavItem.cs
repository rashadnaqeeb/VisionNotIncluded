using System.Collections.Generic;

namespace OniAccess.Widgets {
	/// <summary>
	/// The contract the speech layer and navigation engines depend on, rather
	/// than the concrete <see cref="Widget"/> class. A live wrapper around a UI
	/// control (Widget) is the primary implementation, but a data row or
	/// synthetic group can implement this in a few lines without carrying Unity
	/// fields.
	///
	/// Nothing here is cached: <see cref="Announce"/> re-reads game state at
	/// speech time and <see cref="GetChildren"/> is computed on demand, so the
	/// structure can reflect live, filtered, or dynamic content.
	/// </summary>
	public interface NavItem {
		/// <summary>Whether the cursor can land on this item.</summary>
		bool IsNavigable();

		/// <summary>Whether Enter performs an action on this item.</summary>
		bool IsActivatable();

		/// <summary>
		/// Own label and value, re-read live. Tooltip, role, and position are
		/// added by <see cref="WidgetSpeech.Compose"/>, not here.
		/// </summary>
		string Announce();

		/// <summary>Activate (click, toggle, begin editing). Returns true if handled.</summary>
		bool Activate();

		/// <summary>Adjust the value (slider step, dropdown cycle). Returns true if it changed.</summary>
		bool Adjust(int direction, int stepLevel);

		/// <summary>
		/// Child items, computed on demand. Empty means a leaf; non-empty means
		/// drillable.
		/// </summary>
		IReadOnlyList<NavItem> GetChildren();

		/// <summary>
		/// Control role key ("button", "slider", null, ...) consumed by future
		/// decoration such as a verbose UI mode. Not spoken today.
		/// </summary>
		string RoleKey { get; }
	}

	/// <summary>
	/// Position context for an item within its list, passed to
	/// <see cref="WidgetSpeech.Compose"/>. Ignored today; reserved for future
	/// decoration (position readout, verbose UI).
	/// </summary>
	public struct NavContext {
		public int Position;
		public int Total;
		public int Level;

		public static readonly NavContext None = new NavContext { Position = -1, Total = -1, Level = 0 };
	}
}
