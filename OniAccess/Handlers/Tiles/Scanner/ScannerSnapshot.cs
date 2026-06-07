using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Scanner {
	public class ScannerItem {
		public string ItemName;
		public List<ScanEntry> Instances;
	}

	public class ScannerSubcategory {
		public string Name;
		// Set only for keyword subcategories: the user's keyword, spoken in
		// preference to the taxonomy label lookup. Null for taxonomy subs.
		public string DisplayName;
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

			// Custom categories sort ahead of the built-ins, in the order the
			// store supplies them (alphabetical by name).
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
		/// Each keyword and each selector becomes a named subcategory; keyword
		/// subcategories sort ahead of the taxonomy selector subcategories. A
		/// keyword gathers every entry whose item name matches it; an "all"
		/// selector gathers every entry in its source category, a named
		/// selector only its own subcategory.
		///
		/// Items get their own ScannerItem objects (distinct from the real
		/// category they mirror), but within one custom category every
		/// subcategory and the implicit "all" share a single ScannerItem per
		/// item identity (the ItemPool). Identity is the entry's
		/// category/subcategory/name triple, matching how the built-in hierarchy
		/// groups items, so the same name under two subcategories (a wild and a
		/// tame critter of one species) or two categories (a solid element and
		/// its debris) stays distinct. Sharing matters because a keyword can
		/// overlap a selector on the very same entries: without the pool that
		/// entry would become two ScannerItem objects, "all" would list it
		/// twice, and pruning one would leave the other stale. With the pool,
		/// prune-by-identity removes an emptied item from every subcategory at
		/// once, exactly as a real category behaves. A custom category that
		/// matches nothing is skipped, like any empty built-in category never
		/// appears.
		/// </summary>
		private static List<ScannerCategory> BuildCustomCategories(
				List<ScanEntry> entries, int cursorCell,
				IReadOnlyList<CustomScannerCategory> customDefs) {
			var result = new List<ScannerCategory>();
			if (customDefs == null) return result;

			// Built lazily and shared across every keyword of every category: the
			// link-stripped, normalized form of each distinct item name, so a
			// keyword match strips each name once per rebuild instead of once per
			// keyword per entry.
			Dictionary<string, string> plainByName = null;

			foreach (var def in customDefs) {
				bool hasSelectors = def.Selectors != null && def.Selectors.Count > 0;
				bool hasKeywords = def.Keywords != null && def.Keywords.Count > 0;
				if (!hasSelectors && !hasKeywords) continue;

				var pool = new ItemPool(cursorCell);
				var namedSubs = new List<ScannerSubcategory>();

				if (hasKeywords) {
					if (plainByName == null) plainByName = BuildPlainNames(entries);
					foreach (var keyword in def.Keywords) {
						var sub = BuildKeywordSub(entries, cursorCell, keyword, pool, plainByName);
						if (sub != null) namedSubs.Add(sub);
					}
				}

				if (hasSelectors)
					foreach (var sel in OrderedSelectors(def.Selectors)) {
						var sub = BuildSelectorSub(entries, cursorCell, sel, pool);
						if (sub != null) namedSubs.Add(sub);
					}

				if (namedSubs.Count == 0) continue;

				// "all" references the same pooled items, deduped by item identity.
				var allItems = new List<ScannerItem>(pool.Items);
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

		/// <summary>An item's identity for pooling: its category, subcategory,
		/// and name, matching how the built-in hierarchy groups items. Same name
		/// under two subcategories or two categories stays distinct.</summary>
		private static string ItemKey(ScanEntry e) =>
			e.Category + "" + e.Subcategory + "" + e.ItemName;

		/// <summary>One ScannerItem per item identity within a single custom
		/// category, shared across its subcategories so prune-by-identity works.
		/// Instances are sorted nearest-first the first time an item is pooled.</summary>
		private sealed class ItemPool {
			private readonly int _cursorCell;
			private readonly Dictionary<string, ScannerItem> _byKey =
				new Dictionary<string, ScannerItem>();

			public ItemPool(int cursorCell) { _cursorCell = cursorCell; }

			public IEnumerable<ScannerItem> Items => _byKey.Values;

			public ScannerItem GetOrAdd(string key, string itemName, List<ScanEntry> instances) {
				if (_byKey.TryGetValue(key, out var existing)) return existing;
				instances.Sort((a, b) =>
					GridUtil.CellDistance(_cursorCell, a.Cell)
						.CompareTo(GridUtil.CellDistance(_cursorCell, b.Cell)));
				var item = new ScannerItem { ItemName = itemName, Instances = instances };
				_byKey[key] = item;
				return item;
			}
		}

		private static ScannerSubcategory BuildSelectorSub(
				List<ScanEntry> entries, int cursorCell, CustomSelector sel, ItemPool pool) {
			bool isAll = sel.Subcategory == ScannerTaxonomy.Subcategories.All;

			var byKey = new Dictionary<string, List<ScanEntry>>();
			foreach (var entry in entries) {
				if (entry.Category != sel.Category) continue;
				if (!isAll && entry.Subcategory != sel.Subcategory) continue;
				string key = ItemKey(entry);
				if (!byKey.TryGetValue(key, out var instances))
					byKey[key] = instances = new List<ScanEntry>();
				instances.Add(entry);
			}
			if (byKey.Count == 0) return null;

			var items = new List<ScannerItem>();
			foreach (var kvp in byKey)
				items.Add(pool.GetOrAdd(kvp.Key, kvp.Value[0].ItemName, kvp.Value));
			items.Sort((a, b) => CompareItems(a, b, cursorCell));

			// An "all" selector speaks its source category's name; a named
			// selector speaks the subcategory's name. Both keys resolve through
			// the navigator's existing label lookup.
			string subName = isAll ? sel.Category : sel.Subcategory;
			return new ScannerSubcategory { Name = subName, Items = items };
		}

		/// <summary>All distinct item names mapped to their link-stripped,
		/// normalized match form, computed once so keyword matching never re-strips
		/// the same name.</summary>
		private static Dictionary<string, string> BuildPlainNames(List<ScanEntry> entries) {
			var map = new Dictionary<string, string>();
			foreach (var entry in entries)
				if (!map.ContainsKey(entry.ItemName))
					map[entry.ItemName] = ScannerSearch.PlainForMatch(entry.ItemName);
			return map;
		}

		/// <summary>
		/// Build a subcategory from a search keyword: every entry whose item
		/// name matches, ordered by match quality then distance, exactly as the
		/// scanner search ranks results. The subcategory carries the keyword as
		/// its DisplayName so it is spoken verbatim rather than routed through
		/// the taxonomy label lookup, which would mistranslate a keyword that
		/// happens to equal a taxonomy key.
		/// </summary>
		private static ScannerSubcategory BuildKeywordSub(
				List<ScanEntry> entries, int cursorCell, string keyword, ItemPool pool,
				Dictionary<string, string> plainByName) {
			var matcher = new ScannerSearch.NameMatcher(keyword);
			var byKey = new Dictionary<string, List<ScanEntry>>();
			var matchKey = new Dictionary<string, int>();
			foreach (var entry in entries) {
				int sortKey = matcher.MatchPlain(plainByName[entry.ItemName]);
				if (sortKey < 0) continue;
				string key = ItemKey(entry);
				if (!byKey.TryGetValue(key, out var instances)) {
					byKey[key] = instances = new List<ScanEntry>();
					matchKey[key] = sortKey;
				}
				instances.Add(entry);
			}
			if (byKey.Count == 0) return null;

			var items = new List<ScannerItem>();
			var itemKeyOf = new Dictionary<ScannerItem, string>();
			foreach (var kvp in byKey) {
				var item = pool.GetOrAdd(kvp.Key, kvp.Value[0].ItemName, kvp.Value);
				items.Add(item);
				itemKeyOf[item] = kvp.Key;
			}
			items.Sort((a, b) => {
				int sk = matchKey[itemKeyOf[a]].CompareTo(matchKey[itemKeyOf[b]]);
				if (sk != 0) return sk;
				return GridUtil.CellDistance(cursorCell, a.Instances[0].Cell)
					.CompareTo(GridUtil.CellDistance(cursorCell, b.Instances[0].Cell));
			});
			return new ScannerSubcategory { Name = keyword, DisplayName = keyword, Items = items };
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
