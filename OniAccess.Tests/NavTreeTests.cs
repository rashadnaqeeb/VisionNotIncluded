using System;
using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Widgets;

namespace OniAccess.Tests {
	/// <summary>
	/// Offline tests for the lazy-children navigation engine (NavTree). The engine is
	/// pure — every operation returns a NavMove describing where the cursor landed and
	/// what changed — so its behavior is asserted directly with no game state.
	///
	/// Tree used by most tests (matches the action menu's mixed-depth shape):
	///   Tools            depth-0
	///     T0, T1         depth-1 leaves
	///   Build            depth-0
	///     Sub0           depth-1
	///       B0, B1       depth-2 leaves
	///     Sub1           depth-1
	///       B2           depth-2 leaf
	///     SubEmpty       depth-1, non-navigable, no children
	///   Power            depth-0
	///     PSub           depth-1
	///       P0           depth-2 leaf
	/// </summary>
	static class NavTreeTests {
		// --- test node ---

		private class N: NavItem {
			public string Name;
			public bool Nav = true;
			public List<NavItem> Kids;

			public N(string name, params NavItem[] kids) {
				Name = name;
				if (kids != null && kids.Length > 0) Kids = new List<NavItem>(kids);
			}

			public string RoleKey => null;
			public bool IsNavigable() => Nav;
			public bool IsActivatable() => false;
			public string Announce() => Name;
			public bool Activate() => false;
			public bool Adjust(int direction, int stepLevel) => false;
			public IReadOnlyList<NavItem> GetChildren() =>
				(IReadOnlyList<NavItem>)Kids ?? Array.Empty<NavItem>();
		}

		private static N NonNav(string name) {
			return new N(name) { Nav = false };
		}

		private static List<NavItem> MakeTree() {
			return new List<NavItem> {
				new N("Tools", new N("T0"), new N("T1")),
				new N("Build",
					new N("Sub0", new N("B0"), new N("B1")),
					new N("Sub1", new N("B2")),
					NonNav("SubEmpty")),
				new N("Power", new N("PSub", new N("P0"))),
			};
		}

		private static NavTree Tree() => new NavTree(MakeTree);

		private static (string, bool, string) Pass(string name) => (name, true, "OK");
		private static (string, bool, string) Check(string name, bool ok, string detail)
			=> (name, ok, ok ? "OK" : detail);

		// ========================================
		// UP / DOWN
		// ========================================

		public static (string, bool, string) NextWithinParentNoContext() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 0 }); // B0
			var m = t.Next();             // B1, same parent
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "B1"
				&& m.ChangedAncestors.Count == 0;
			return Check("NextWithinParentNoContext", ok,
				$"item={m.Item?.Announce()}, ancestors={m.ChangedAncestors.Count}");
		}

		public static (string, bool, string) NextCrossesParentBoundary() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 1 }); // B1 (last child of Sub0)
			var m = t.Next();             // B2 (Sub1), crosses subcategory
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "B2"
				&& m.ChangedAncestors.Count == 1
				&& m.ChangedAncestors[0].Announce() == "Sub1";
			return Check("NextCrossesParentBoundary", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) NextCrossesGrandparentBoundary() {
			var t = Tree();
			t.SetPath(new[] { 1, 1, 0 }); // B2 (Build/Sub1)
			var m = t.Next();             // P0 (Power/PSub), crosses category + subcategory
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "P0"
				&& m.ChangedAncestors.Count == 2
				&& m.ChangedAncestors[0].Announce() == "Power"
				&& m.ChangedAncestors[1].Announce() == "PSub";
			return Check("NextCrossesGrandparentBoundary", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) NextWrapsAtEnd() {
			var t = Tree();
			t.SetPath(new[] { 2, 0, 0 }); // P0 (last depth-2 leaf)
			var m = t.Next();             // wraps to B0
			bool ok = m.Moved && m.Wrapped && m.Item.Announce() == "B0";
			return Check("NextWrapsAtEnd", ok,
				$"item={m.Item?.Announce()}, wrapped={m.Wrapped}");
		}

		public static (string, bool, string) NextSkipsEmptyBranch() {
			// SubEmpty contributes no depth-2 nodes, so the depth-2 walk never lands in it.
			var t = Tree();
			t.SetPath(new[] { 1, 0, 1 }); // B1
			t.Next();                     // B2
			var m = t.Next();             // P0 — SubEmpty skipped, not a dead stop
			bool ok = m.Item.Announce() == "P0";
			return Check("NextSkipsEmptyBranch", ok, $"item={m.Item?.Announce()}");
		}

		public static (string, bool, string) NextSingleItemIsNoOp() {
			var t = Tree();
			t.SetPath(new[] { 1, 1, 0 }); // B2 — Sub1 has one child, but depth-2 frontier has many
			// At depth 0 with a single navigable root we'd no-op; build that case:
			var single = new NavTree(() => new List<NavItem> { new N("only") });
			var m = single.Next();
			bool ok = !m.Moved;
			return Check("NextSingleItemIsNoOp", ok, $"moved={m.Moved}");
		}

		public static (string, bool, string) NextFromNonNavigableCurrent() {
			// If the current node goes non-navigable, Next lands on the nearest later leaf.
			var roots = MakeTree();
			var build = (N)roots[1];
			var sub0 = (N)build.Kids[0];
			var b1 = (N)sub0.Kids[1];
			var t = new NavTree(() => roots);
			t.SetPath(new[] { 1, 0, 1 }); // B1
			b1.Nav = false;               // B1 vanishes from the frontier
			var m = t.Next();             // should land on B2, the next leaf after [1,0,1]
			bool ok = m.Moved && m.Item.Announce() == "B2";
			return Check("NextFromNonNavigableCurrent", ok, $"item={m.Item?.Announce()}");
		}

		public static (string, bool, string) PrevCrossesBoundaryBackward() {
			var t = Tree();
			t.SetPath(new[] { 1, 1, 0 }); // B2
			var m = t.Prev();             // B1 (Sub0)
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "B1"
				&& m.ChangedAncestors.Count == 1
				&& m.ChangedAncestors[0].Announce() == "Sub0";
			return Check("PrevCrossesBoundaryBackward", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) PrevWrapsAtStart() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 0 }); // B0 (first depth-2 leaf)
			var m = t.Prev();             // wraps to P0
			bool ok = m.Moved && m.Wrapped && m.Item.Announce() == "P0";
			return Check("PrevWrapsAtStart", ok,
				$"item={m.Item?.Announce()}, wrapped={m.Wrapped}");
		}

		// ========================================
		// HOME / END (within current parent group)
		// ========================================

		public static (string, bool, string) FirstStaysWithinParent() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 1 }); // B1 (Sub0)
			var m = t.First();            // B0 (first child of Sub0, not global first)
			bool ok = m.Moved && m.Item.Announce() == "B0" && m.ChangedAncestors.Count == 0;
			return Check("FirstStaysWithinParent", ok, $"item={m.Item?.Announce()}");
		}

		public static (string, bool, string) LastStaysWithinParent() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 0 }); // B0 (Sub0)
			var m = t.Last();             // B1 (last child of Sub0, not global last)
			bool ok = m.Moved && m.Item.Announce() == "B1";
			return Check("LastStaysWithinParent", ok, $"item={m.Item?.Announce()}");
		}

		// ========================================
		// CTRL+UP / CTRL+DOWN (jump parent group)
		// ========================================

		public static (string, bool, string) JumpNextToNextGroup() {
			var t = Tree();
			t.SetPath(new[] { 1, 0, 0 }); // B0 (Build/Sub0)
			var m = t.JumpNext();         // first child of Build/Sub1 = B2
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "B2"
				&& m.ChangedAncestors.Count == 1
				&& m.ChangedAncestors[0].Announce() == "Sub1";
			return Check("JumpNextToNextGroup", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) JumpNextCrossesCategory() {
			var t = Tree();
			t.SetPath(new[] { 1, 1, 0 }); // B2 (Build/Sub1)
			var m = t.JumpNext();         // first child of Power/PSub = P0
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "P0"
				&& m.ChangedAncestors.Count == 2;
			return Check("JumpNextCrossesCategory", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) JumpNextWrapsToFirstGroup() {
			var t = Tree();
			t.SetPath(new[] { 2, 0, 0 }); // P0 (last group)
			var m = t.JumpNext();         // wraps to first child of Build/Sub0 = B0
			bool ok = m.Moved && m.Wrapped && m.Item.Announce() == "B0";
			return Check("JumpNextWrapsToFirstGroup", ok,
				$"item={m.Item?.Announce()}, wrapped={m.Wrapped}");
		}

		public static (string, bool, string) JumpPrevToPrevGroup() {
			var t = Tree();
			t.SetPath(new[] { 1, 1, 0 }); // B2 (Build/Sub1)
			var m = t.JumpPrev();         // first child of Build/Sub0 = B0
			bool ok = m.Moved && !m.Wrapped && m.Item.Announce() == "B0"
				&& m.ChangedAncestors.Count == 1
				&& m.ChangedAncestors[0].Announce() == "Sub0";
			return Check("JumpPrevToPrevGroup", ok,
				$"item={m.Item?.Announce()}, ancestors=[{string.Join(",", Names(m.ChangedAncestors))}]");
		}

		public static (string, bool, string) JumpAtRootActsLikeStep() {
			var t = Tree();
			t.Reset(0);                   // depth-0 cursor on Tools
			var m = t.JumpNext();         // at root, jump == step → Build
			bool ok = m.Moved && m.Item.Announce() == "Build";
			return Check("JumpAtRootActsLikeStep", ok, $"item={m.Item?.Announce()}");
		}

		// ========================================
		// DRILL / BACK
		// ========================================

		public static (string, bool, string) DrillEntersFirstChild() {
			var t = Tree();
			t.SetPath(new[] { 1 });   // Build
			var m = t.Drill();        // Sub0
			bool ok = m.Moved && m.Item.Announce() == "Sub0" && t.Depth == 1;
			return Check("DrillEntersFirstChild", ok, $"item={m.Item?.Announce()}, depth={t.Depth}");
		}

		public static (string, bool, string) DrillLeafIsNoOp() {
			var t = Tree();
			t.SetPath(new[] { 0, 0 }); // T0, a leaf
			var m = t.Drill();
			bool ok = !m.Moved && t.Depth == 1;
			return Check("DrillLeafIsNoOp", ok, $"moved={m.Moved}, depth={t.Depth}");
		}

		public static (string, bool, string) DrillSkipsToFirstNavigableChild() {
			// Build's first navigable child is Sub0; a leading non-navigable child is skipped.
			var roots = new List<NavItem> {
				new N("Cat", NonNav("hidden"), new N("Real", new N("leaf"))),
			};
			var t = new NavTree(() => roots);
			t.SetPath(new[] { 0 });   // Cat
			var m = t.Drill();        // Real (index 1), skipping hidden (index 0)
			bool ok = m.Moved && m.Item.Announce() == "Real";
			return Check("DrillSkipsToFirstNavigableChild", ok, $"item={m.Item?.Announce()}");
		}

		public static (string, bool, string) BackPopsLevel() {
			var t = Tree();
			t.SetPath(new[] { 1, 0 }); // Sub0
			var m = t.Back();          // Build
			bool ok = m.Moved && m.Item.Announce() == "Build" && t.Depth == 0
				&& m.ChangedAncestors.Count == 0;
			return Check("BackPopsLevel", ok, $"item={m.Item?.Announce()}, depth={t.Depth}");
		}

		public static (string, bool, string) BackAtRootIsNoOp() {
			var t = Tree();
			t.SetPath(new[] { 1 });
			var m = t.Back();
			bool ok = !m.Moved && t.Depth == 0;
			return Check("BackAtRootIsNoOp", ok, $"moved={m.Moved}, depth={t.Depth}");
		}

		public static (string, bool, string) CanDrillReflectsChildren() {
			var t = Tree();
			t.SetPath(new[] { 1 });        // Build → has children
			bool buildCan = t.CanDrill();
			t.SetPath(new[] { 0, 0 });     // T0 → leaf
			bool leafCan = t.CanDrill();
			bool ok = buildCan && !leafCan;
			return Check("CanDrillReflectsChildren", ok, $"build={buildCan}, leaf={leafCan}");
		}

		// ========================================
		// SEARCH
		// ========================================

		public static (string, bool, string) SearchLeavesSpansAllDepths() {
			var t = Tree();
			t.SearchScope = SearchScope.Leaves;
			int count = t.SearchCount();
			// Leaves: T0, T1 (depth 1) + B0, B1, B2, P0 (depth 2) = 6. SubEmpty non-navigable.
			bool ok = count == 6;
			return Check("SearchLeavesSpansAllDepths", ok, $"count={count}");
		}

		public static (string, bool, string) SearchLeafMoveChangesDepth() {
			var t = Tree();
			t.SearchScope = SearchScope.Leaves;
			t.Reset(0);                 // start at depth 0
			t.SearchCount();
			// Find the index of B2 in the leaf frontier and move to it.
			int target = -1;
			int n = t.SearchCount();
			for (int i = 0; i < n; i++) if (t.SearchLabel(i) == "B2") target = i;
			var m = t.SearchMoveTo(target);
			bool ok = m.Moved && m.Item.Announce() == "B2" && t.Depth == 2;
			return Check("SearchLeafMoveChangesDepth", ok,
				$"item={m.Item?.Announce()}, depth={t.Depth}");
		}

		public static (string, bool, string) SearchCurrentLevelUsesDepth() {
			var t = Tree();
			t.SearchScope = SearchScope.CurrentLevel;
			t.SetPath(new[] { 1, 0 });   // depth 1 (a subcategory)
			int count = t.SearchCount();
			// Depth-1 frontier: Sub0, Sub1, PSub, T0, T1 navigable... actually all depth-1
			// navigable nodes across the whole tree: T0,T1,Sub0,Sub1,PSub = 5 (SubEmpty excluded).
			bool ok = count == 5;
			return Check("SearchCurrentLevelUsesDepth", ok, $"count={count}");
		}

		public static (string, bool, string) SearchFilterExcludesNodes() {
			var t = Tree();
			t.SearchScope = SearchScope.Leaves;
			t.SearchFilter = node => node.Announce() != "T1";
			int count = t.SearchCount();
			bool ok = count == 5;
			return Check("SearchFilterExcludesNodes", ok, $"count={count}");
		}

		public static (string, bool, string) SearchActivatableLeavesExcludesEmptyBranch() {
			// The action menu defines its search as "activatable leaves only" via
			// SearchScope.Leaves + SearchFilter(IsActivatable), so tools and buildings
			// are searched but categories and empty subcategories are not. This locks
			// that mechanism on MenuNode, the type the action menu actually uses.
			var tool0 = new MenuNode(() => "tool0", activate: () => true);
			var tool1 = new MenuNode(() => "tool1", activate: () => true);
			var b0 = new MenuNode(() => "B0", activate: () => true);
			var sub0 = new MenuNode(() => "Sub0",
				children: () => new List<NavItem> { b0 });
			var emptySub = new MenuNode(() => "EmptySub",
				children: () => new List<NavItem>());
			var tools = new MenuNode(() => "Tools",
				children: () => new List<NavItem> { tool0, tool1 });
			var build = new MenuNode(() => "Build",
				children: () => new List<NavItem> { sub0, emptySub });
			var roots = new List<NavItem> { tools, build };

			var t = new NavTree(() => roots);
			t.SearchScope = SearchScope.Leaves;
			t.SearchFilter = n => n.IsActivatable();
			int filtered = t.SearchCount(); // tool0, tool1, B0
			t.SearchFilter = null;
			int all = t.SearchCount();      // + EmptySub, a childless non-activatable leaf
			bool ok = filtered == 3 && all == 4;
			return Check("SearchActivatableLeavesExcludesEmptyBranch", ok,
				$"filtered={filtered}, all={all}");
		}

		// ========================================
		// CROSSING SCOPE (confinement)
		// ========================================

		public static (string, bool, string) ConfineNextStaysInBranch() {
			var t = Tree();
			t.Crossing = CrossingScope.WithinGrandparent;
			t.SetPath(new[] { 1, 1, 0 }); // B2, last building under Build
			var m = t.Next();             // confined to Build → wraps to B0, never reaches Power
			bool ok = m.Moved && m.Wrapped && m.Item.Announce() == "B0";
			return Check("ConfineNextStaysInBranch", ok,
				$"item={m.Item?.Announce()}, wrapped={m.Wrapped}");
		}

		public static (string, bool, string) ConfineDepthOneStaysGlobal() {
			var t = Tree();
			t.Crossing = CrossingScope.WithinGrandparent;
			t.SetPath(new[] { 1, 1 }); // Sub1 under Build, depth 1
			var m = t.Next();          // depth 1 grandparent is above root → global → PSub
			bool ok = m.Moved && m.Item.Announce() == "PSub";
			return Check("ConfineDepthOneStaysGlobal", ok, $"item={m.Item?.Announce()}");
		}

		public static (string, bool, string) ConfineJumpStaysInBranch() {
			var t = Tree();
			t.Crossing = CrossingScope.WithinGrandparent;
			t.SetPath(new[] { 1, 1, 0 }); // B2 (Build/Sub1, last group in Build)
			var m = t.JumpNext();         // confined to Build → wraps to Build/Sub0's B0
			bool ok = m.Moved && m.Wrapped && m.Item.Announce() == "B0";
			return Check("ConfineJumpStaysInBranch", ok,
				$"item={m.Item?.Announce()}, wrapped={m.Wrapped}");
		}

		public static (string, bool, string) ConfineSearchScopedToBranch() {
			var t = Tree();
			t.Crossing = CrossingScope.WithinGrandparent;
			t.SearchScope = SearchScope.CurrentLevel;
			t.SetPath(new[] { 1, 0, 0 }); // depth 2 under Build
			int count = t.SearchCount();
			// Build's buildings only (B0, B1, B2) — Power's P0 excluded.
			bool ok = count == 3;
			return Check("ConfineSearchScopedToBranch", ok, $"count={count}");
		}

		// ========================================
		// CLAMP
		// ========================================

		public static (string, bool, string) SetPathClampsOutOfRange() {
			var t = Tree();
			t.SetPath(new[] { 1, 5, 9 }); // Build has 3 children; clamp to SubEmpty (idx 2),
										   // which has no children → truncate the depth-2 index.
			var p = t.Path;
			bool ok = p.Count == 2 && p[0] == 1 && p[1] == 2;
			return Check("SetPathClampsOutOfRange", ok, $"path=[{string.Join(",", p)}]");
		}

		public static (string, bool, string) ClampSurvivesTreeShrink() {
			List<NavItem> roots = MakeTree();
			var t = new NavTree(() => roots);
			t.SetPath(new[] { 1, 1, 0 }); // B2 (Build/Sub1)
			// Build now has only Sub0 (with one child B0).
			roots = new List<NavItem> {
				roots[0],
				new N("Build", new N("Sub0", new N("B0"))),
				roots[2],
			};
			t.ClampToTree();
			var p = t.Path;
			// path[1]=1 out of range → clamp to 0 (Sub0); path[2]=0 still valid (B0).
			bool ok = p.Count == 3 && p[0] == 1 && p[1] == 0 && p[2] == 0
				&& t.Current().Announce() == "B0";
			return Check("ClampSurvivesTreeShrink", ok,
				$"path=[{string.Join(",", p)}], item={t.Current()?.Announce()}");
		}

		// ========================================
		// REGISTRATION
		// ========================================

		public static IEnumerable<(string, bool, string)> All() {
			yield return NextWithinParentNoContext();
			yield return NextCrossesParentBoundary();
			yield return NextCrossesGrandparentBoundary();
			yield return NextWrapsAtEnd();
			yield return NextSkipsEmptyBranch();
			yield return NextSingleItemIsNoOp();
			yield return NextFromNonNavigableCurrent();
			yield return PrevCrossesBoundaryBackward();
			yield return PrevWrapsAtStart();
			yield return FirstStaysWithinParent();
			yield return LastStaysWithinParent();
			yield return JumpNextToNextGroup();
			yield return JumpNextCrossesCategory();
			yield return JumpNextWrapsToFirstGroup();
			yield return JumpPrevToPrevGroup();
			yield return JumpAtRootActsLikeStep();
			yield return DrillEntersFirstChild();
			yield return DrillLeafIsNoOp();
			yield return DrillSkipsToFirstNavigableChild();
			yield return BackPopsLevel();
			yield return BackAtRootIsNoOp();
			yield return CanDrillReflectsChildren();
			yield return SearchLeavesSpansAllDepths();
			yield return SearchLeafMoveChangesDepth();
			yield return SearchCurrentLevelUsesDepth();
			yield return SearchFilterExcludesNodes();
			yield return SearchActivatableLeavesExcludesEmptyBranch();
			yield return ConfineNextStaysInBranch();
			yield return ConfineDepthOneStaysGlobal();
			yield return ConfineJumpStaysInBranch();
			yield return ConfineSearchScopedToBranch();
			yield return SetPathClampsOutOfRange();
			yield return ClampSurvivesTreeShrink();
		}

		private static IEnumerable<string> Names(IReadOnlyList<NavItem> items) {
			foreach (var i in items) yield return i.Announce();
		}
	}
}
