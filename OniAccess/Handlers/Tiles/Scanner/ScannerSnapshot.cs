using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Scanner {
	public class ScannerItem {
		public string ItemName;
		public List<ScanEntry> Instances;
	}

	public class ScannerSubcategory {
		public string Name;
		public List<ScannerItem> Items;
	}

	public class ScannerCategory {
		public string Name;
		// Set only for synthetic custom categories: the user's display name,
		// spoken in preference to the taxonomy lookup. Null for built-ins.
		public string DisplayName;
		public List<ScannerSubcategory> Subcategories;
	}

	/// <summary>
	/// Frozen 4-level hierarchy built from a flat list of ScanEntry objects.
	/// Categories and subcategories follow ScannerTaxonomy ordering.
	/// The "all" subcategory at index 0 of each category holds shared
	/// ScannerItem references, so removing an instance from a named
	/// subcategory's item automatically removes it from "all".
	/// </summary>
	public class ScannerSnapshot {
		public readonly List<ScannerCategory> Categories;
		public readonly int OriginCell;

		public ScannerSnapshot(List<ScanEntry> entries, int cursorCell,
				IReadOnlyList<CustomScannerCategory> customDefs = null) {
			OriginCell = cursorCell;
			Categories = Build(entries, cursorCell, customDefs);
		}

		public int CategoryCount => Categories.Count;

		public ScannerCategory GetCategory(int ci) => Categories[ci];

		public ScannerSubcategory GetSubcategory(int ci, int si) =>
			Categories[ci].Subcategories[si];

		public ScannerItem GetItem(int ci, int si, int ii) =>
			Categories[ci].Subcategories[si].Items[ii];

		public ScanEntry GetInstance(int ci, int si, int ii, int ni) =>
			Categories[ci].Subcategories[si].Items[ii].Instances[ni];

		/// <summary>
		/// Remove a ScanEntry from its item's instance list. Because "all"
		/// holds shared ScannerItem references, the entry disappears from
		/// both named and "all" subcategories. If the item becomes empty,
		/// prune it from all subcategory lists and clean up empty containers.
		/// </summary>
		public void RemoveInstance(ScannerItem item, ScanEntry entry) {
			item.Instances.Remove(entry);
			if (item.Instances.Count > 0) return;
			PruneEmptyItem(item);
		}

		private static List<ScannerCategory> Build(
				List<ScanEntry> entries, int cursorCell,
				IReadOnlyList<CustomScannerCategory> customDefs) {
			// Group entries: category -> subcategory -> itemName -> instances
			var grouped = new Dictionary<string,
				Dictionary<string, Dictionary<string, List<ScanEntry>>>>();

			foreach (var entry in entries) {
				if (!grouped.TryGetValue(entry.Category, out var byCat))
					grouped[entry.Category] = byCat =
						new Dictionary<string, Dictionary<string, List<ScanEntry>>>();
				if (!byCat.TryGetValue(entry.Subcategory, out var bySub))
					byCat[entry.Subcategory] = bySub =
						new Dictionary<string, List<ScanEntry>>();
				if (!bySub.TryGetValue(entry.ItemName, out var instances))
					bySub[entry.ItemName] = instances = new List<ScanEntry>();
				instances.Add(entry);
			}

			var categories = new List<ScannerCategory>();

			foreach (var catKvp in grouped) {
				string catName = catKvp.Key;

				// Build named subcategories
				var namedSubcats = new List<ScannerSubcategory>();
				foreach (var subKvp in catKvp.Value) {
					var items = new List<ScannerItem>();
					foreach (var itemKvp in subKvp.Value) {
						var instances = itemKvp.Value;
						instances.Sort((a, b) =>
							GridUtil.CellDistance(cursorCell, a.Cell)
								.CompareTo(GridUtil.CellDistance(cursorCell, b.Cell)));
						items.Add(new ScannerItem {
							ItemName = itemKvp.Key,
							Instances = instances,
						});
					}
					items.Sort((a, b) => CompareItems(a, b, cursorCell));

					namedSubcats.Add(new ScannerSubcategory {
						Name = subKvp.Key,
						Items = items,
					});
				}

				namedSubcats.Sort((a, b) =>
					ScannerTaxonomy.SubcategorySortIndex(catName, a.Name)
						.CompareTo(ScannerTaxonomy.SubcategorySortIndex(catName, b.Name)));

				// Build "all" from shared item references
				var allItems = new List<ScannerItem>();
				foreach (var sub in namedSubcats)
					allItems.AddRange(sub.Items);
				allItems.Sort((a, b) => CompareItems(a, b, cursorCell));

				var subcats = new List<ScannerSubcategory>(namedSubcats.Count + 1) {
					new ScannerSubcategory {
						Name = ScannerTaxonomy.Subcategories.All,
						Items = allItems,
					}
				};
				subcats.AddRange(namedSubcats);

				categories.Add(new ScannerCategory {
					Name = catName,
					Subcategories = subcats,
				});
			}

			categories.Sort((a, b) =>
				ScannerTaxonomy.CategorySortIndex(a.Name)
					.CompareTo(ScannerTaxonomy.CategorySortIndex(b.Name)));

			// Custom categories sort ahead of the built-ins, in creation order.
			var customCats = BuildCustomCategories(entries, cursorCell, customDefs);
			if (customCats.Count == 0)
				return categories;
			var combined = new List<ScannerCategory>(customCats.Count + categories.Count);
			combined.AddRange(customCats);
			combined.AddRange(categories);
			return combined;
		}

		/// <summary>
		/// Synthesize the user's custom categories from the same entry list.
		/// Each selector becomes a named subcategory: an "all" selector
		/// gathers every entry in its source category, a named selector only
		/// its own subcategory. Items get their own ScannerItem objects
		/// (distinct from the real category they mirror), but within a custom
		/// category the selector sub and the implicit "all" share item
		/// references so prune-by-identity works exactly as it does in a real
		/// category. A custom category that matches nothing is skipped, like
		/// any empty built-in category never appears.
		/// </summary>
		private static List<ScannerCategory> BuildCustomCategories(
				List<ScanEntry> entries, int cursorCell,
				IReadOnlyList<CustomScannerCategory> customDefs) {
			var result = new List<ScannerCategory>();
			if (customDefs == null) return result;

			foreach (var def in customDefs) {
				if (def.Selectors == null || def.Selectors.Count == 0) continue;

				var namedSubs = new List<ScannerSubcategory>();
				foreach (var sel in OrderedSelectors(def.Selectors)) {
					bool isAll = sel.Subcategory == ScannerTaxonomy.Subcategories.All;

					var byName = new Dictionary<string, List<ScanEntry>>();
					foreach (var entry in entries) {
						if (entry.Category != sel.Category) continue;
						if (!isAll && entry.Subcategory != sel.Subcategory) continue;
						if (!byName.TryGetValue(entry.ItemName, out var instances))
							byName[entry.ItemName] = instances = new List<ScanEntry>();
						instances.Add(entry);
					}
					if (byName.Count == 0) continue;

					var items = new List<ScannerItem>();
					foreach (var kvp in byName) {
						kvp.Value.Sort((a, b) =>
							GridUtil.CellDistance(cursorCell, a.Cell)
								.CompareTo(GridUtil.CellDistance(cursorCell, b.Cell)));
						items.Add(new ScannerItem { ItemName = kvp.Key, Instances = kvp.Value });
					}
					items.Sort((a, b) => CompareItems(a, b, cursorCell));

					// An "all" selector speaks its source category's name; a
					// named selector speaks the subcategory's name. Both keys
					// resolve through the navigator's existing label lookup.
					string subName = isAll ? sel.Category : sel.Subcategory;
					namedSubs.Add(new ScannerSubcategory { Name = subName, Items = items });
				}

				if (namedSubs.Count == 0) continue;

				var allItems = new List<ScannerItem>();
				foreach (var sub in namedSubs)
					allItems.AddRange(sub.Items);
				allItems.Sort((a, b) => CompareItems(a, b, cursorCell));

				var subs = new List<ScannerSubcategory>(namedSubs.Count + 1) {
					new ScannerSubcategory {
						Name = ScannerTaxonomy.Subcategories.All,
						Items = allItems,
					}
				};
				subs.AddRange(namedSubs);

				result.Add(new ScannerCategory {
					Name = def.Id,
					DisplayName = def.Name,
					Subcategories = subs,
				});
			}

			return result;
		}

		/// <summary>Selectors in taxonomy order so the custom category's
		/// subcategory cycle reads in the same order as the source taxonomy,
		/// regardless of the order the user toggled them.</summary>
		private static List<CustomSelector> OrderedSelectors(List<CustomSelector> selectors) {
			var copy = new List<CustomSelector>(selectors);
			copy.Sort((a, b) => {
				int c = ScannerTaxonomy.CategorySortIndex(a.Category)
					.CompareTo(ScannerTaxonomy.CategorySortIndex(b.Category));
				if (c != 0) return c;
				return ScannerTaxonomy.SubcategorySortIndex(a.Category, a.Subcategory)
					.CompareTo(ScannerTaxonomy.SubcategorySortIndex(b.Category, b.Subcategory));
			});
			return copy;
		}

		private static int CompareItems(ScannerItem a, ScannerItem b, int cursorCell) {
			int sk = a.Instances[0].SortKey.CompareTo(b.Instances[0].SortKey);
			if (sk != 0) return sk;
			return GridUtil.CellDistance(cursorCell, a.Instances[0].Cell)
				.CompareTo(GridUtil.CellDistance(cursorCell, b.Instances[0].Cell));
		}

		private void PruneEmptyItem(ScannerItem item) {
			for (int ci = Categories.Count - 1; ci >= 0; ci--) {
				var cat = Categories[ci];
				bool found = false;
				for (int si = cat.Subcategories.Count - 1; si >= 0; si--) {
					if (cat.Subcategories[si].Items.Remove(item))
						found = true;
				}
				if (!found) continue;
				for (int si = cat.Subcategories.Count - 1; si >= 0; si--) {
					if (cat.Subcategories[si].Items.Count == 0)
						cat.Subcategories.RemoveAt(si);
				}
				if (cat.Subcategories.Count == 0)
					Categories.RemoveAt(ci);
				break;
			}
		}

	}
}
