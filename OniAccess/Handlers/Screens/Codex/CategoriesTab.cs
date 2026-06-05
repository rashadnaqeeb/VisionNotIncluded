using System;
using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Codex {
	/// <summary>
	/// Categories tab: a drill tree over the codex.
	/// Level 0 = top categories (programmatic CategoryEntry + YAML-based)
	/// Level 1 = entries or sub-categories within a category
	/// Level 2 = entries within a sub-category (CategoryEntry children)
	///           OR SubEntries of a level 1 entry (critter morphs, etc.)
	/// Level 3 = SubEntries of a level 2 entry inside a sub-category
	///
	/// Each node's children are computed on demand: a category yields its entries,
	/// a non-category entry yields its visible sub-entries. Enter on a CodexEntry
	/// opens its article; Right drills into sub-entries; Enter on a sub-entry opens
	/// the parent article at that sub-entry's section.
	///
	/// Type-ahead searches a custom frontier spanning leaf entries, sub-entries, and
	/// the top categories (categories ordered last), so it overrides the search hooks
	/// rather than using the engine's leaf frontier.
	/// </summary>
	internal class CategoriesTab: NavTreeHandler, IScreenTab {
		private readonly CodexScreenHandler _parent;

		// Snapshot of the flat search frontier for the current type-ahead pass. Rebuilt
		// every SearchCount() call (once per keystroke) and read by SearchEntryText,
		// GetSearchGroup, and MoveSearchCursor during that pass, mirroring NavTree's own
		// _searchFrontier lifecycle. Never held across passes: the codex can gain entries
		// live, so a stale frontier would land search on the wrong sub-entry.
		private List<FlatEntry> _flatEntries;

		internal CategoriesTab(CodexScreenHandler parent) : base(screen: null) {
			_parent = parent;
			_search.GroupOf = GetSearchGroup;
		}

		public string TabName => (string)STRINGS.ONIACCESS.CODEX.CATEGORIES_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => DrillNavHelpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			OnTabActivatedOnEntry(announce, entryId: null);
		}

		/// <summary>
		/// Activate and optionally position the cursor on a specific entry.
		/// Used when returning from the content tab to land on the article
		/// the user was reading.
		/// </summary>
		internal void OnTabActivatedOnEntry(bool announce, string entryId) {
			_flatEntries = null;
			if (entryId == null || !NavigateToEntry(entryId))
				ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				AnnounceCurrent(interrupt: false);
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var topCats = CodexHelper.GetTopCategories();
			var roots = new List<NavItem>(topCats.Count);
			foreach (var cat in topCats) {
				var c = cat;
				roots.Add(new MenuNode(
					() => CodexHelper.GetCategoryDisplayName(c),
					children: () => ChildrenOf(c)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> ChildrenOf(CodexEntry entry) {
			if (CodexHelper.IsCategory(entry)) {
				var children = CodexHelper.GetEntriesInCategory(entry);
				var list = new List<NavItem>(children.Count);
				foreach (var child in children)
					list.Add(EntryNode(child));
				return list;
			}
			var subs = CodexHelper.GetVisibleSubEntries(entry);
			var subList = new List<NavItem>(subs.Count);
			foreach (var sub in subs) {
				var s = sub;
				subList.Add(new MenuNode(
					() => CodexHelper.GetSubEntryName(s),
					activate: () => { OpenSubEntry(s); return true; }));
			}
			return subList;
		}

		private NavItem EntryNode(CodexEntry entry) {
			var e = entry;
			if (CodexHelper.IsCategory(e)) {
				// Sub-category: drillable, not directly activatable.
				return new MenuNode(
					() => CodexHelper.GetEntryName(e),
					children: () => ChildrenOf(e));
			}
			// Leaf entry: opens its article on Enter; Right still drills into sub-entries.
			return new MenuNode(
				() => CodexHelper.GetEntryName(e),
				children: () => ChildrenOf(e),
				activate: () => { OpenEntry(e); return true; });
		}

		// Categories drill on Enter; entries and sub-entries open their article.
		protected override bool ShouldDrillOnActivate() {
			var node = Nav.Current();
			return node != null && !node.IsActivatable();
		}

		private void OpenEntry(CodexEntry entry) {
			var screen = _parent.CodexScreen;
			if (screen == null) return;
			PlaySound("HUD_Click_Open");
			screen.ChangeArticle(entry.id);
			_parent.JumpToContentTab();
		}

		private void OpenSubEntry(SubEntry sub) {
			_parent.ContentTabRef.SetPendingSubEntryId(sub.id);
			var screen = _parent.CodexScreen;
			if (screen == null) return;
			PlaySound("HUD_Click_Open");
			screen.ChangeArticle(sub.id);
			_parent.JumpToContentTab();
		}

		// ========================================
		// SEARCH (custom frontier: leaf entries, sub-entries, and top categories)
		// ========================================

		protected override int SearchCount() {
			_flatEntries = BuildSearchableEntries();
			return _flatEntries.Count;
		}

		protected override string SearchEntryText(int index) {
			if (_flatEntries == null || index < 0 || index >= _flatEntries.Count) return null;
			var item = _flatEntries[index];
			if (item.subEntryName != null) return item.subEntryName;
			return CodexHelper.GetEntryName(item.entry);
		}

		protected override void MoveSearchCursor(int index) {
			if (_flatEntries == null || index < 0 || index >= _flatEntries.Count) return;
			var item = _flatEntries[index];
			int targetLevel = item.isCategory ? 0 : item.targetLevel;
			Nav.SetPath(PathFor(item, targetLevel));
			_search.Clear();
			AnnounceCurrent();
		}

		private int GetSearchGroup(int flatIndex) {
			return (_flatEntries != null && flatIndex >= 0 && flatIndex < _flatEntries.Count
				&& _flatEntries[flatIndex].isCategory) ? 1 : 0;
		}

		// ========================================
		// CURSOR POSITIONING
		// ========================================

		/// <summary>
		/// Position the cursor on the leaf entry matching entryId.
		/// Returns false if the entry isn't found in the category tree.
		/// </summary>
		private bool NavigateToEntry(string entryId) {
			var all = BuildSearchableEntries();
			for (int i = 0; i < all.Count; i++) {
				if (all[i].isCategory) continue;
				if (all[i].subEntryName != null) continue;
				if (all[i].entry.id == entryId) {
					// Original landed with subEntryIdx forced to 0.
					var item = all[i];
					item.subEntryIdx = 0;
					Nav.SetPath(PathFor(item, item.targetLevel));
					_search.Clear();
					SuppressSearchThisFrame();
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The cursor path for a flat entry at the given target level: the leading
		/// indices of [catIdx, entryIdx, subCatIdx, subEntryIdx]. A sub-entry's
		/// position lives in subEntryIdx, so for a sub-entry directly under a level-1
		/// entry (target level 2) that index belongs at the deepest level — otherwise
		/// the generic copy would place the (zero) sub-category index there and land
		/// on the entry's first sub-entry instead of the matched one.
		/// </summary>
		private static int[] PathFor(FlatEntry item, int targetLevel) {
			var full = new[] { item.catIdx, item.entryIdx, item.subCatIdx, item.subEntryIdx };
			var path = new int[targetLevel + 1];
			Array.Copy(full, path, targetLevel + 1);
			if (item.subEntryName != null)
				path[targetLevel] = item.subEntryIdx;
			return path;
		}

		private struct FlatEntry {
			internal CodexEntry entry;
			internal int catIdx;
			internal int entryIdx;
			internal int subCatIdx;
			internal int subEntryIdx;
			internal int targetLevel;
			internal bool isCategory;
			internal string subEntryName;
		}

		private List<FlatEntry> BuildSearchableEntries() {
			var result = new List<FlatEntry>();
			var topCats = CodexHelper.GetTopCategories();
			for (int c = 0; c < topCats.Count; c++) {
				var entries = CodexHelper.GetEntriesInCategory(topCats[c]);
				for (int e = 0; e < entries.Count; e++) {
					if (CodexHelper.IsCategory(entries[e])) {
						var subEntries = CodexHelper.GetEntriesInCategory(entries[e]);
						for (int s = 0; s < subEntries.Count; s++) {
							result.Add(new FlatEntry {
								entry = subEntries[s],
								catIdx = c,
								entryIdx = e,
								subCatIdx = s,
								targetLevel = 2
							});
							// SubEntries of entries within a sub-category (level 3)
							AddSubEntrySearchItems(result, subEntries[s], c, e, s, 3);
						}
					} else {
						result.Add(new FlatEntry {
							entry = entries[e],
							catIdx = c,
							entryIdx = e,
							subCatIdx = 0,
							targetLevel = 1
						});
						// SubEntries of direct entries (level 2)
						AddSubEntrySearchItems(result, entries[e], c, e, 0, 2);
					}
				}
			}
			for (int c = 0; c < topCats.Count; c++) {
				result.Add(new FlatEntry {
					entry = topCats[c],
					catIdx = c,
					isCategory = true
				});
			}
			return result;
		}

		private static void AddSubEntrySearchItems(
			List<FlatEntry> result, CodexEntry entry,
			int catIdx, int entryIdx, int subCatIdx, int targetLevel
		) {
			var subs = CodexHelper.GetVisibleSubEntries(entry);
			for (int i = 0; i < subs.Count; i++) {
				result.Add(new FlatEntry {
					entry = entry,
					catIdx = catIdx,
					entryIdx = entryIdx,
					subCatIdx = subCatIdx,
					subEntryIdx = i,
					targetLevel = targetLevel,
					subEntryName = CodexHelper.GetSubEntryName(subs[i])
				});
			}
		}
	}
}
