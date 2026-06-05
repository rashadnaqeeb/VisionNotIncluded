using System;
using System.Collections.Generic;

using OniAccess.Widgets;

namespace OniAccess.Navigation {
	/// <summary>
	/// Which nodes type-ahead search ranges over.
	/// </summary>
	public enum SearchScope {
		/// <summary>
		/// All navigable leaves, at any depth, in tree order. A "leaf" is a navigable
		/// node with no navigable children. This dissolves the mixed-depth search
		/// problem: a depth-one tool and a depth-two building are both leaves, so one
		/// frontier covers them with no per-item target-level computation.
		/// </summary>
		Leaves,

		/// <summary>
		/// Only the nodes at the cursor's current depth. Used by screens that search
		/// the level you are on rather than the leaves (the details screen).
		/// </summary>
		CurrentLevel,
	}

	/// <summary>
	/// How far up the tree a horizontal move (Up/Down, Ctrl+Up/Down, and type-ahead)
	/// is allowed to wander. Drill, back, and Home/End are unaffected.
	/// </summary>
	public enum CrossingScope {
		/// <summary>
		/// Cross every ancestor boundary. At any depth the move ranges over the whole
		/// tree, so the action menu's Down walks all buildings across all subcategories
		/// and categories. The default.
		/// </summary>
		FullTree,

		/// <summary>
		/// Stay within the current grandparent: at depth d the move ranges only over
		/// descendants of the ancestor at depth d-2, crossing the immediate parent
		/// boundary but not the one above it. At depth 0 or 1 the grandparent is above
		/// the root, so it is identical to <see cref="FullTree"/>. This keeps the
		/// details screen's storage-content navigation (level 2) inside the current
		/// section, while its section and item levels (0 and 1) stay global.
		/// </summary>
		WithinGrandparent,
	}

	/// <summary>
	/// The unified list-and-drill navigation engine: a cursor that walks a tree of
	/// <see cref="NavItem"/> computed on demand. It replaces the index-callback model
	/// (GetItemCount/GetItemLabel per level plus a parallel flat-search projection and
	/// hand-rolled cross-boundary traversal) with one depth-generic walker.
	///
	/// The engine is pure: it computes where the cursor lands and what changed, and
	/// returns that as a <see cref="NavMove"/>. Sound and speech stay in the adapter,
	/// which is what lets the walking logic be tested offline.
	///
	/// Nothing is cached. The roots are fetched through the supplied delegate on every
	/// operation, and children through <see cref="NavItem.GetChildren"/>, so the tree
	/// always reflects live, filtered, or dynamic game state. The cursor is an index
	/// path re-resolved against the freshly computed tree each access, clamping when
	/// the tree shrinks.
	/// </summary>
	public sealed class NavTree {
		private readonly Func<IReadOnlyList<NavItem>> _roots;
		private List<int> _path = new List<int> { 0 };

		// Frontier snapshot for one type-ahead pass. Holds index paths (not game
		// state), rebuilt every time SearchCount() is called — i.e. once per keystroke
		// — and read by SearchLabel/SearchMoveTo during that pass.
		private List<int[]> _searchFrontier;

		public NavTree(Func<IReadOnlyList<NavItem>> roots) {
			_roots = roots ?? throw new ArgumentNullException(nameof(roots));
		}

		/// <summary>Current cursor depth (0 is the root level).</summary>
		public int Depth => _path.Count - 1;

		/// <summary>The current cursor path, read-only.</summary>
		public IReadOnlyList<int> Path => _path;

		/// <summary>Search frontier selection. Defaults to all leaves.</summary>
		public SearchScope SearchScope { get; set; } = SearchScope.Leaves;

		/// <summary>How far horizontal moves and search may cross. Defaults to the whole tree.</summary>
		public CrossingScope Crossing { get; set; } = CrossingScope.FullTree;

		/// <summary>
		/// Optional extra predicate excluding nodes from search (e.g. a synthetic
		/// "pinned" duplicate). Applied on top of the scope. Null means no extra filter.
		/// </summary>
		public Func<NavItem, bool> SearchFilter { get; set; }

		// ========================================
		// RESOLUTION
		// ========================================

		/// <summary>The live node the cursor points at, or null if it no longer resolves.</summary>
		public NavItem Current() => ResolveNode(_path, _path.Count);

		/// <summary>Whether the current node has at least one navigable child to drill into.</summary>
		public bool CanDrill() {
			var node = Current();
			if (node == null) return false;
			return FirstNavigable(node.GetChildren()) >= 0;
		}

		private IReadOnlyList<NavItem> RootItems() =>
			_roots() ?? (IReadOnlyList<NavItem>)Array.Empty<NavItem>();

		/// <summary>
		/// Resolve the node addressed by the first <paramref name="len"/> indices of
		/// <paramref name="path"/>, or null if any index is out of range.
		/// </summary>
		private NavItem ResolveNode(IReadOnlyList<int> path, int len) {
			if (len <= 0) return null;
			var items = RootItems();
			NavItem node = null;
			for (int d = 0; d < len; d++) {
				int idx = path[d];
				if (idx < 0 || idx >= items.Count) return null;
				node = items[idx];
				if (d < len - 1) items = node.GetChildren();
			}
			return node;
		}

		/// <summary>
		/// The sibling list at <paramref name="depth"/> under the current cursor's
		/// ancestors: the children of the node at path[0..depth-1], or the roots when
		/// depth is 0.
		/// </summary>
		private IReadOnlyList<NavItem> SiblingsAt(int depth) {
			if (depth == 0) return RootItems();
			var parent = ResolveNode(_path, depth);
			if (parent == null) return Array.Empty<NavItem>();
			return parent.GetChildren();
		}

		/// <summary>The cursor's current sibling list (children of its parent, or the roots at depth 0).</summary>
		public IReadOnlyList<NavItem> SiblingsAtCurrent() => SiblingsAt(Depth);

		/// <summary>The cursor's immediate parent node, or null at the root level.</summary>
		public NavItem CurrentParent() =>
			Depth >= 1 ? ResolveNode(_path, _path.Count - 1) : null;

		private static int FirstNavigable(IReadOnlyList<NavItem> items) {
			for (int i = 0; i < items.Count; i++)
				if (items[i].IsNavigable()) return i;
			return -1;
		}

		private static int LastNavigable(IReadOnlyList<NavItem> items) {
			for (int i = items.Count - 1; i >= 0; i--)
				if (items[i].IsNavigable()) return i;
			return -1;
		}

		// ========================================
		// FRONTIER (all navigable nodes at a depth, in tree order == lexicographic
		// order by path, which is exactly DFS pre-order)
		// ========================================

		private List<int[]> Frontier(int depth) {
			var list = new List<int[]>();
			var prefix = new List<int>();
			CollectFrontier(prefix, RootItems(), 0, depth, list);
			return list;
		}

		/// <summary>
		/// The frontier at <paramref name="depth"/>, narrowed to the cursor's crossing
		/// scope. Under <see cref="CrossingScope.WithinGrandparent"/> only entries that
		/// share the cursor's ancestor at depth d-2 survive, so a horizontal move cannot
		/// leave the current grandparent's subtree.
		/// </summary>
		private List<int[]> ConfinedFrontier(int depth) {
			var f = Frontier(depth);
			int len = ConfinementLen();
			if (len == 0) return f;
			f.RemoveAll(p => p.Length < len || !SamePrefix(p, _path, len));
			return f;
		}

		/// <summary>
		/// How many leading path indices a horizontal move must keep fixed. Zero means
		/// the whole tree is in scope.
		/// </summary>
		private int ConfinementLen() {
			if (Crossing == CrossingScope.FullTree) return 0;
			return Math.Max(0, Depth - 1);
		}

		private void CollectFrontier(List<int> prefix, IReadOnlyList<NavItem> items,
				int curDepth, int targetDepth, List<int[]> outList) {
			for (int i = 0; i < items.Count; i++) {
				var node = items[i];
				prefix.Add(i);
				if (curDepth == targetDepth) {
					if (node.IsNavigable()) outList.Add(prefix.ToArray());
				} else {
					CollectFrontier(prefix, node.GetChildren(), curDepth + 1, targetDepth, outList);
				}
				prefix.RemoveAt(prefix.Count - 1);
			}
		}

		// ========================================
		// CURSOR PLACEMENT
		// ========================================

		/// <summary>
		/// Reset the cursor to the first position at <paramref name="startDepth"/>
		/// (path of zeros). The handler's first announcement reads it live.
		/// </summary>
		public void Reset(int startDepth = 0) {
			_path = new List<int>(startDepth + 1);
			for (int i = 0; i <= startDepth; i++) _path.Add(0);
		}

		/// <summary>
		/// Place the cursor at an explicit path, clamped to the live tree. For jumps
		/// the engine cannot express through stepping: open-on-category, restore after
		/// placement, return-to-permit from a detail tab.
		/// </summary>
		public void SetPath(IReadOnlyList<int> path) {
			_path = new List<int>(path);
			ClampToTree();
		}

		/// <summary>
		/// Clamp the cursor into the current tree: pull each index into range and drop
		/// levels that no longer exist (a pin toggle, a section merge, a slider that
		/// removed a child). Re-resolves against freshly computed children.
		/// </summary>
		public void ClampToTree() {
			if (_path.Count == 0) { _path.Add(0); return; }
			var items = RootItems();
			int d = 0;
			while (d < _path.Count) {
				if (items.Count == 0) {
					if (d == 0) { _path = new List<int> { 0 }; }
					else { _path.RemoveRange(d, _path.Count - d); }
					return;
				}
				if (_path[d] >= items.Count) _path[d] = items.Count - 1;
				if (_path[d] < 0) _path[d] = 0;
				var node = items[_path[d]];
				d++;
				if (d < _path.Count) items = node.GetChildren();
			}
		}

		private NavMove LandAt(int[] newPath, bool wrapped, bool withAncestors) {
			var oldPath = _path;
			_path = new List<int>(newPath);
			return new NavMove {
				Moved = true,
				Wrapped = wrapped,
				Item = Current(),
				ChangedAncestors = withAncestors
					? ComputeChangedAncestors(oldPath, newPath)
					: Array.Empty<NavItem>(),
			};
		}

		/// <summary>
		/// Ancestors whose index differs between old and new same-depth paths,
		/// shallowest first. The spoken context prefix.
		/// </summary>
		private IReadOnlyList<NavItem> ComputeChangedAncestors(List<int> oldPath, int[] newPath) {
			int depth = newPath.Length - 1;
			int firstChanged = -1;
			for (int k = 0; k < depth; k++) {
				bool oldValid = k < oldPath.Count;
				if (!oldValid || oldPath[k] != newPath[k]) { firstChanged = k; break; }
			}
			if (firstChanged < 0) return Array.Empty<NavItem>();

			var result = new List<NavItem>(depth - firstChanged);
			for (int a = firstChanged; a < depth; a++) {
				var node = ResolveNode(newPath, a + 1);
				if (node != null) result.Add(node);
			}
			return result;
		}

		// ========================================
		// NAVIGATION: UP / DOWN (global frontier step)
		// ========================================

		public NavMove Next() => Step(forward: true);
		public NavMove Prev() => Step(forward: false);

		private NavMove Step(bool forward) {
			var f = ConfinedFrontier(Depth);
			if (f.Count == 0) return NavMove.None;

			int cur = IndexOfPath(f, _path);
			int target;
			bool wrapped;
			if (cur >= 0) {
				if (forward) {
					target = cur + 1;
					if (target >= f.Count) { target = 0; wrapped = true; } else wrapped = false;
				} else {
					target = cur - 1;
					if (target < 0) { target = f.Count - 1; wrapped = true; } else wrapped = false;
				}
				if (SamePath(f[target], _path)) return NavMove.None;
			} else {
				// Current path is not in the frontier (its node went non-navigable or
				// vanished). Land on the nearest entry in the travel direction.
				if (forward) {
					target = FirstGreater(f, _path);
					if (target < 0) { target = 0; wrapped = true; } else wrapped = false;
				} else {
					target = LastLess(f, _path);
					if (target < 0) { target = f.Count - 1; wrapped = true; } else wrapped = false;
				}
			}
			return LandAt(f[target], wrapped, withAncestors: true);
		}

		// ========================================
		// NAVIGATION: HOME / END (within the current parent group)
		// ========================================

		public NavMove First() => Edge(last: false);
		public NavMove Last() => Edge(last: true);

		private NavMove Edge(bool last) {
			int depth = Depth;
			var siblings = SiblingsAt(depth);
			int idx = last ? LastNavigable(siblings) : FirstNavigable(siblings);
			if (idx < 0) return NavMove.None;

			var newPath = new int[depth + 1];
			for (int k = 0; k < depth; k++) newPath[k] = _path[k];
			newPath[depth] = idx;
			return LandAt(newPath, wrapped: false, withAncestors: false);
		}

		// ========================================
		// NAVIGATION: CTRL+UP / CTRL+DOWN (jump to next/prev parent group)
		// ========================================

		public NavMove JumpNext() {
			if (Depth == 0) return Next();
			return JumpGroup(forward: true);
		}

		public NavMove JumpPrev() {
			if (Depth == 0) return Prev();
			return JumpGroup(forward: false);
		}

		private NavMove JumpGroup(bool forward) {
			int depth = Depth;
			var f = ConfinedFrontier(depth);
			if (f.Count == 0) return NavMove.None;

			// Group starts: indices where the depth-(d-1) parent prefix changes.
			var starts = new List<int> { 0 };
			for (int i = 1; i < f.Count; i++)
				if (!SamePrefix(f[i], f[i - 1], depth)) starts.Add(i);

			int cur = FindGroup(f, starts, _path, depth);
			int target;
			bool wrapped;

			if (cur >= 0) {
				if (forward) {
					int ng = cur + 1;
					if (ng >= starts.Count) { target = starts[0]; wrapped = true; }
					else { target = starts[ng]; wrapped = false; }
				} else {
					int pg = cur - 1;
					if (pg < 0) { target = starts[starts.Count - 1]; wrapped = true; }
					else { target = starts[pg]; wrapped = false; }
				}
			} else {
				// Current group has no navigable entries; pick the nearest group.
				int g = forward ? FirstGroupGreater(f, starts, _path, depth)
								 : LastGroupLess(f, starts, _path, depth);
				if (g >= 0) { target = starts[g]; wrapped = false; }
				else {
					target = forward ? starts[0] : starts[starts.Count - 1];
					wrapped = true;
				}
			}

			return LandAt(f[target], wrapped, withAncestors: true);
		}

		// ========================================
		// NAVIGATION: RIGHT / LEFT (drill / back)
		// ========================================

		public NavMove Drill() {
			var node = Current();
			if (node == null) return NavMove.None;
			int idx = FirstNavigable(node.GetChildren());
			if (idx < 0) return NavMove.None;

			var newPath = new int[_path.Count + 1];
			for (int k = 0; k < _path.Count; k++) newPath[k] = _path[k];
			newPath[_path.Count] = idx;
			return LandAt(newPath, wrapped: false, withAncestors: false);
		}

		public NavMove Back() {
			if (Depth == 0) return NavMove.None;
			_path.RemoveAt(_path.Count - 1);
			return new NavMove {
				Moved = true, Wrapped = false, Item = Current(),
				ChangedAncestors = Array.Empty<NavItem>(),
			};
		}

		// ========================================
		// SEARCH
		// ========================================

		/// <summary>
		/// Snapshot the search frontier for this type-ahead pass and return its size.
		/// Call before SearchLabel/SearchMoveTo; type-ahead reads the count once per
		/// keystroke, which is exactly when the frontier must be recomputed.
		/// </summary>
		public int SearchCount() {
			_searchFrontier = BuildSearchFrontier();
			return _searchFrontier.Count;
		}

		/// <summary>Raw <see cref="NavItem.Announce"/> text of frontier entry i (the adapter filters it).</summary>
		public string SearchLabel(int i) {
			if (_searchFrontier == null || i < 0 || i >= _searchFrontier.Count) return null;
			var node = ResolveNode(_searchFrontier[i], _searchFrontier[i].Length);
			return node?.Announce();
		}

		/// <summary>Move the cursor onto frontier entry i. The adapter speaks the landed item.</summary>
		public NavMove SearchMoveTo(int i) {
			if (_searchFrontier == null || i < 0 || i >= _searchFrontier.Count) return NavMove.None;
			return LandAt(_searchFrontier[i], wrapped: false, withAncestors: false);
		}

		private List<int[]> BuildSearchFrontier() {
			List<int[]> list;
			if (SearchScope == SearchScope.CurrentLevel) {
				list = Frontier(Depth);
			} else {
				list = new List<int[]>();
				CollectLeaves(new List<int>(), RootItems(), list);
			}
			int len = ConfinementLen();
			if (len > 0)
				list.RemoveAll(p => p.Length < len || !SamePrefix(p, _path, len));
			if (SearchFilter != null) {
				list.RemoveAll(p => {
					var node = ResolveNode(p, p.Length);
					return node == null || !SearchFilter(node);
				});
			}
			return list;
		}

		private void CollectLeaves(List<int> prefix, IReadOnlyList<NavItem> items, List<int[]> outList) {
			for (int i = 0; i < items.Count; i++) {
				var node = items[i];
				prefix.Add(i);
				var children = node.GetChildren();
				bool hasNavChild = FirstNavigable(children) >= 0;
				if (node.IsNavigable() && !hasNavChild)
					outList.Add(prefix.ToArray());
				else if (hasNavChild)
					CollectLeaves(prefix, children, outList);
				prefix.RemoveAt(prefix.Count - 1);
			}
		}

		// ========================================
		// PATH COMPARISON HELPERS
		// ========================================

		private static bool SamePath(IReadOnlyList<int> a, IReadOnlyList<int> b) {
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (a[i] != b[i]) return false;
			return true;
		}

		private static bool SamePrefix(IReadOnlyList<int> a, IReadOnlyList<int> b, int len) {
			for (int i = 0; i < len; i++)
				if (a[i] != b[i]) return false;
			return true;
		}

		/// <summary>Lexicographic comparison of two equal-length paths.</summary>
		private static int CompareLex(IReadOnlyList<int> a, IReadOnlyList<int> b) {
			int n = Math.Min(a.Count, b.Count);
			for (int i = 0; i < n; i++) {
				if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
			}
			return a.Count.CompareTo(b.Count);
		}

		private static int IndexOfPath(List<int[]> frontier, IReadOnlyList<int> path) {
			for (int i = 0; i < frontier.Count; i++)
				if (SamePath(frontier[i], path)) return i;
			return -1;
		}

		private static int FirstGreater(List<int[]> frontier, IReadOnlyList<int> path) {
			for (int i = 0; i < frontier.Count; i++)
				if (CompareLex(frontier[i], path) > 0) return i;
			return -1;
		}

		private static int LastLess(List<int[]> frontier, IReadOnlyList<int> path) {
			for (int i = frontier.Count - 1; i >= 0; i--)
				if (CompareLex(frontier[i], path) < 0) return i;
			return -1;
		}

		// Group helpers: a "group" is the maximal run of frontier entries sharing the
		// depth-(d-1) parent prefix. starts holds each run's first index.

		private static int FindGroup(List<int[]> f, List<int> starts, IReadOnlyList<int> path, int depth) {
			for (int g = 0; g < starts.Count; g++)
				if (SamePrefix(f[starts[g]], path, depth)) return g;
			return -1;
		}

		private static int FirstGroupGreater(List<int[]> f, List<int> starts, IReadOnlyList<int> path, int depth) {
			for (int g = 0; g < starts.Count; g++)
				if (ComparePrefix(f[starts[g]], path, depth) > 0) return g;
			return -1;
		}

		private static int LastGroupLess(List<int[]> f, List<int> starts, IReadOnlyList<int> path, int depth) {
			for (int g = starts.Count - 1; g >= 0; g--)
				if (ComparePrefix(f[starts[g]], path, depth) < 0) return g;
			return -1;
		}

		private static int ComparePrefix(IReadOnlyList<int> a, IReadOnlyList<int> b, int len) {
			for (int i = 0; i < len; i++)
				if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
			return 0;
		}
	}
}
