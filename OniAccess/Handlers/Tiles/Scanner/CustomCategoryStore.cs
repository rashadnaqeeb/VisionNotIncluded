using System;
using System.Collections.Generic;
using System.Linq;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.Scanner {
	/// <summary>
	/// CRUD and selector logic for user-defined custom scanner categories,
	/// backed by the global mod config. The selector-mutation rules
	/// (Apply*/IsSelected) are pure functions over a single category so they
	/// can be tested without the config layer; the id-keyed wrappers find the
	/// category, apply the rule, and persist.
	///
	/// Selectors are user configuration, not live game state, so persisting
	/// them is correct: the never-cache rule covers game data, which the
	/// scanner still re-queries through its backends on every rebuild.
	/// </summary>
	public static class CustomCategoryStore {
		private static List<CustomScannerCategory> All {
			get {
				// A YAML load that bypasses field initializers can leave this
				// null; heal it so every caller sees a list.
				if (ConfigManager.Config.CustomScannerCategories == null)
					ConfigManager.Config.CustomScannerCategories =
						new List<CustomScannerCategory>();
				return ConfigManager.Config.CustomScannerCategories;
			}
		}

		// ===== Queries =====

		/// <summary>All categories in creation order (the live list).</summary>
		public static List<CustomScannerCategory> GetAll() => All;

		public static CustomScannerCategory Find(string id) =>
			All.FirstOrDefault(c => c.Id == id);

		// ===== Category mutation =====

		public static CustomScannerCategory Add(string name) {
			var category = new CustomScannerCategory {
				Id = Guid.NewGuid().ToString("N"),
				Name = name,
			};
			All.Add(category);
			ConfigManager.Save();
			return category;
		}

		public static void Rename(string id, string newName) {
			var category = Find(id);
			if (category == null) {
				Log.Warn($"CustomCategoryStore.Rename: unknown id {id}");
				return;
			}
			category.Name = newName;
			ConfigManager.Save();
		}

		public static void Delete(string id) {
			int removed = All.RemoveAll(c => c.Id == id);
			if (removed == 0) {
				Log.Warn($"CustomCategoryStore.Delete: unknown id {id}");
				return;
			}
			ConfigManager.Save();
		}

		// ===== Selector mutation (id-keyed wrappers) =====

		public static bool IsAll(string id, string category) {
			var c = Find(id);
			return c != null && IsAllSelected(c, category);
		}

		public static bool IsSub(string id, string category, string subcategory) {
			var c = Find(id);
			return c != null && IsSubSelected(c, category, subcategory);
		}

		public static void SetAll(string id, string category, bool on) {
			var c = Find(id);
			if (c == null) {
				Log.Warn($"CustomCategoryStore.SetAll: unknown id {id}");
				return;
			}
			ApplyAll(c, category, on);
			ConfigManager.Save();
		}

		public static void SetSub(string id, string category, string subcategory, bool on) {
			var c = Find(id);
			if (c == null) {
				Log.Warn($"CustomCategoryStore.SetSub: unknown id {id}");
				return;
			}
			ApplySub(c, category, subcategory, on);
			ConfigManager.Save();
		}

		// ===== Pure selector logic (testable without the config layer) =====

		/// <summary>True when the category's whole-category ("all") selector
		/// is set.</summary>
		public static bool IsAllSelected(CustomScannerCategory c, string category) {
			return c.Selectors.Any(s =>
				s.Category == category && s.Subcategory == ScannerTaxonomy.Subcategories.All);
		}

		/// <summary>A named sub reads checked when its own selector is set OR
		/// when the whole-category selector supersedes it.</summary>
		public static bool IsSubSelected(CustomScannerCategory c, string category, string subcategory) {
			if (IsAllSelected(c, category)) return true;
			return c.Selectors.Any(s => s.Category == category && s.Subcategory == subcategory);
		}

		/// <summary>Setting "all" drops every named selector for the category
		/// (the whole category supersedes them); clearing it removes the
		/// "all" selector, leaving the category with nothing selected.</summary>
		public static void ApplyAll(CustomScannerCategory c, string category, bool on) {
			c.Selectors.RemoveAll(s => s.Category == category);
			if (on)
				c.Selectors.Add(new CustomSelector {
					Category = category,
					Subcategory = ScannerTaxonomy.Subcategories.All,
				});
		}

		/// <summary>
		/// Toggling a named sub while "all" is on first expands the whole
		/// category into its explicit named subs (each implicitly on), then
		/// applies this one toggle, so the visual stays truthful: the user has
		/// dropped from "the whole category" to "these named subs". Otherwise
		/// it just adds or removes this sub's selector.
		/// </summary>
		public static void ApplySub(CustomScannerCategory c, string category, string subcategory, bool on) {
			if (IsAllSelected(c, category)) {
				var named = ScannerTaxonomy.NamedSubcategories(category);
				// The expansion below can only reproduce subs the taxonomy
				// knows; an unknown sub (hand-edited or post-update config)
				// would be dropped without trace, so surface it.
				if (System.Array.IndexOf(named, subcategory) < 0)
					Log.Warn($"CustomCategoryStore.ApplySub: subcategory '{subcategory}' "
						+ $"not in taxonomy for '{category}', toggle dropped");
				c.Selectors.RemoveAll(s => s.Category == category);
				foreach (var sub in named) {
					bool keep = sub == subcategory ? on : true;
					if (keep)
						c.Selectors.Add(new CustomSelector { Category = category, Subcategory = sub });
				}
				return;
			}

			bool present = c.Selectors.Any(s => s.Category == category && s.Subcategory == subcategory);
			if (on && !present)
				c.Selectors.Add(new CustomSelector { Category = category, Subcategory = subcategory });
			else if (!on && present)
				c.Selectors.RemoveAll(s => s.Category == category && s.Subcategory == subcategory);
		}
	}
}
