namespace OniAccess.Widgets {
	/// <summary>
	/// The role tags carried by <see cref="NavItem.RoleKey"/>. Widget subclasses
	/// report their control role; handlers tag synthetic <see cref="MenuNode"/>s and
	/// match those tags in <see cref="Navigation.NavTree.SearchFilter"/> to exclude
	/// command rows or synthetic duplicates from type-ahead.
	///
	/// Centralized so the site that sets a tag and the site that filters on it cannot
	/// silently drift apart: a mistyped literal would otherwise break search on a
	/// blind-only surface with no error.
	/// </summary>
	public static class NavRoles {
		public const string Button = "button";
		public const string Dropdown = "dropdown";
		public const string Slider = "slider";
		public const string Priority = "priority";
		public const string TextInput = "textinput";
		public const string Toggle = "toggle";

		/// <summary>A synthetic pinned-resource duplicate; excluded from search.</summary>
		public const string Pinned = "pinned";
		/// <summary>A rocket module row; the only searchable item in the module picker.</summary>
		public const string Module = "module";
		/// <summary>A learnable skill row; the only searchable item in the skills tab.</summary>
		public const string Skill = "skill";
	}
}
