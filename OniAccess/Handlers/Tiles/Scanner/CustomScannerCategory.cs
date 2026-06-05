using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Scanner {
	/// <summary>
	/// A user-defined scanner category: a named bundle of taxonomy
	/// category/subcategory filters that sorts ahead of the built-in
	/// categories in the scan cycle. Persisted globally in the mod config
	/// (oni-access-config.yml), so a player's preferred scan scopes follow
	/// them to every colony. Public get/set properties so YAML round-trips.
	/// </summary>
	public class CustomScannerCategory {
		/// <summary>
		/// Stable identifier (GUID). Survives renames and serves as the
		/// snapshot category's Name key so cycle position is preserved across
		/// rebuilds and two categories never collide.
		/// </summary>
		public string Id { get; set; }

		/// <summary>User-given display name, spoken in the scan cycle.</summary>
		public string Name { get; set; }

		public List<CustomSelector> Selectors { get; set; } = new List<CustomSelector>();
	}

	/// <summary>
	/// One filter inside a custom category. Subcategory is either a named
	/// taxonomy subcategory key or the literal "all" (the whole category,
	/// every entry regardless of subcategory). A category never holds both
	/// an "all" selector and a named selector for the same Category at once
	/// (the store collapses them), so selectors within a custom category
	/// never overlap.
	/// </summary>
	public class CustomSelector {
		public string Category { get; set; }
		public string Subcategory { get; set; }
	}
}
