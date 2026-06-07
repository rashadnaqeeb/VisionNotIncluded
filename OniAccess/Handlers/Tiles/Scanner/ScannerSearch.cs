using System;
using System.Collections.Generic;
using OniAccess.Util;

namespace OniAccess.Handlers.Tiles.Scanner {
	public static class ScannerSearch {
		/// <summary>
		/// A reusable name matcher for a single query. Normalizes the query once
		/// (lowercase + diacritic strip) at construction, then ranks item names
		/// against it. Shared by the tile scanner search, the cluster map search,
		/// and custom-category keyword subcategories so all three agree on what
		/// counts as a match, how it ranks, and that link formatting is stripped.
		/// </summary>
		internal readonly struct NameMatcher {
			private readonly string _query;

			public NameMatcher(string query) {
				_query = NormalizeForMatch(query);
			}

			/// <summary>Rank a raw item name: strips link formatting and
			/// normalizes the name before matching.</summary>
			public int Match(string itemName) => MatchPlain(PlainForMatch(itemName));

			/// <summary>Rank a name already passed through <see cref="PlainForMatch"/>,
			/// so a caller matching many names against one query can strip and
			/// normalize each distinct name just once.</summary>
			public int MatchPlain(string plainName) => MatchNormalized(plainName, _query);
		}

		/// <summary>
		/// Filter scanner entries by query, remapping each match into a synthetic
		/// "Search" category whose subcategory is the entry's original category.
		/// </summary>
		public static List<ScanEntry> Filter(List<ScanEntry> allEntries, string query) {
			var results = new List<ScanEntry>();
			var matcher = new NameMatcher(query);

			foreach (var entry in allEntries) {
				int sortKey = matcher.Match(entry.ItemName);
				if (sortKey < 0) continue;

				results.Add(new ScanEntry {
					Cell = entry.Cell,
					Backend = entry.Backend,
					BackendData = entry.BackendData,
					ItemName = entry.ItemName,
					Category = (string)STRINGS.ONIACCESS.SCANNER.CATEGORIES.SEARCH,
					Subcategory = entry.Category,
					SortKey = sortKey,
				});
			}

			return results;
		}

		/// <summary>Strip link formatting then normalize an item name for matching
		/// (lowercase + diacritic strip).</summary>
		internal static string PlainForMatch(string itemName) =>
			NormalizeForMatch(STRINGS.UI.StripLinkFormatting(itemName));

		private static string NormalizeForMatch(string s) =>
			StringUtil.RemoveDiacritics(s.ToLowerInvariant());

		/// <summary>
		/// Rank a raw item name against a raw query, normalizing both. The simple
		/// primitive for matching a single name (and the test seam); the per-entry
		/// loops use <see cref="NameMatcher"/> so the query normalizes only once.
		/// Does not strip link formatting.
		/// </summary>
		internal static int MatchSortKey(string itemName, string query) =>
			MatchNormalized(NormalizeForMatch(itemName), NormalizeForMatch(query));

		/// <summary>
		/// Returns sort key (0=string prefix, 1=whole word at word boundary,
		/// 2=word-start at word boundary) or -1 for no match. Both arguments must
		/// already be normalized. Scans all positions to find the best (lowest) key.
		/// </summary>
		private static int MatchNormalized(string name, string query) {
			if (name.StartsWith(query, StringComparison.Ordinal))
				return 0;

			int best = -1;
			int idx = 0;
			while (true) {
				int pos = name.IndexOf(query, idx, StringComparison.Ordinal);
				if (pos < 0) break;

				if (pos > 0 && name[pos - 1] == ' ') {
					int end = pos + query.Length;
					if (end >= name.Length || name[end] == ' ') {
						return 1; // whole word — can't improve past prefix
					}
					if (best < 0) best = 2;
				}

				idx = pos + 1;
			}

			return best;
		}
	}
}
