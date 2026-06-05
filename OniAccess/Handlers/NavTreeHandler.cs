using System.Collections.Generic;
using System.Text;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers {
	/// <summary>
	/// List-and-drill navigation on top of BaseMenuHandler, driven by the
	/// <see cref="NavTree"/> engine instead of per-level index callbacks. A subclass
	/// supplies its item tree through <see cref="BuildRoots"/> (recomputed on demand,
	/// never stored) and the engine handles traversal, boundary crossing, group jumps,
	/// drill/back, type-ahead search, and cursor clamping.
	///
	/// The engine is silent: each operation returns a <see cref="NavMove"/>, and this
	/// adapter renders it into sound and speech. Up/Down/Home/End/jump play the cursor
	/// sounds; drill, back, and search announce without a navigation sound. Subclasses
	/// override <see cref="FormatWithContext"/>,
	/// <see cref="GetTooltip"/>, <see cref="ShouldDrillOnActivate"/>, or the interaction
	/// hooks where their behavior differs.
	/// </summary>
	public abstract class NavTreeHandler: BaseMenuHandler, ISearchable {
		protected readonly NavTree Nav;

		protected NavTreeHandler(KScreen screen = null) : base(screen) {
			Nav = new NavTree(BuildRoots);
			Nav.SearchScope = SearchScope;
			Nav.Crossing = Crossing;
			Nav.SearchFixedDepth = SearchFixedDepth;
			if (GroupSearchByRoot)
				_search.GroupOf = Nav.SearchGroup;
		}

		/// <summary>
		/// Build the item tree's roots, recomputed live on every engine access. Wrap
		/// game data in <see cref="NavItem"/>s here; do not store the result.
		/// </summary>
		protected abstract IReadOnlyList<NavItem> BuildRoots();

		/// <summary>Search frontier scope. Leaves by default; the details screen searches the current level.</summary>
		protected virtual SearchScope SearchScope => SearchScope.Leaves;

		/// <summary>
		/// How far horizontal navigation and search may cross. The whole tree by
		/// default; the details screen confines level-2 moves to the current section.
		/// </summary>
		protected virtual CrossingScope Crossing => CrossingScope.FullTree;

		/// <summary>
		/// When true, type-ahead keeps results from the same top-level group together
		/// rather than interleaving them by match quality (clothing items by slot).
		/// </summary>
		protected virtual bool GroupSearchByRoot => false;

		/// <summary>
		/// When >= 0, type-ahead always searches the nodes at this exact depth (the
		/// report screen searches its stat level from anywhere). -1 uses SearchScope.
		/// </summary>
		protected virtual int SearchFixedDepth => -1;

		/// <summary>Standard help entries for a drillable list (search, navigate, jump, drill, back).</summary>
		protected static readonly List<HelpEntry> DrillNavHelpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.HELP.JUMP_GROUP),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter/Right", STRINGS.ONIACCESS.HELP.OPEN_GROUP),
			new HelpEntry("Left", STRINGS.ONIACCESS.HELP.GO_BACK),
		};

		/// <summary>Cursor depth set on activation (0 for a list, deeper for a screen that opens drilled in).</summary>
		protected virtual int StartDepth => 0;

		// ========================================
		// BaseMenuHandler ABSTRACT IMPLEMENTATIONS
		// (the engine drives navigation, so these are only a safety net — base list
		// navigation and the public ISearchable members are all overridden below.)
		// ========================================

		public override int ItemCount {
			get {
				int c = 0;
				foreach (var n in Nav.SiblingsAtCurrent())
					if (n.IsNavigable()) c++;
				return c;
			}
		}

		public override string GetItemLabel(int index) {
			var siblings = Nav.SiblingsAtCurrent();
			int c = 0;
			foreach (var n in siblings) {
				if (!n.IsNavigable()) continue;
				if (c == index) return n.Announce();
				c++;
			}
			return null;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			AnnounceCurrent(interrupt: true);
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			Nav.Reset(StartDepth);
		}

		/// <summary>
		/// Reset the cursor to the start depth and clear search, without the rest of
		/// activation. For tabs that re-enter via OnTabActivated rather than OnActivate.
		/// </summary>
		protected void ResetState() {
			Nav.Reset(StartDepth);
			_search.Clear();
			SuppressSearchThisFrame();
		}

		// ========================================
		// NAVIGATION (drive the engine, render the result)
		// ========================================

		protected override void NavigateNext() => Announce(Nav.Next(), sound: true);
		protected override void NavigatePrev() => Announce(Nav.Prev(), sound: true);
		protected override void NavigateFirst() => Announce(Nav.First(), sound: true);
		protected override void NavigateLast() => Announce(Nav.Last(), sound: true);
		protected override void JumpNextGroup() => Announce(Nav.JumpNext(), sound: true);
		protected override void JumpPrevGroup() => Announce(Nav.JumpPrev(), sound: true);

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (direction > 0) Drill();
			else Back();
		}

		/// <summary>Drill into the current node's children. No-op (silent) when it is a leaf.</summary>
		protected virtual void Drill() {
			if (!Nav.CanDrill()) return;
			_search.Clear();
			Announce(Nav.Drill(), sound: false);
		}

		/// <summary>Pop one level. No-op (silent) at the root.</summary>
		protected virtual void Back() {
			if (Nav.Depth == 0) return;
			_search.Clear();
			Announce(Nav.Back(), sound: false);
		}

		protected override void ActivateCurrentItem() {
			var node = Nav.Current();
			if (node == null) return;
			if (Nav.CanDrill() && ShouldDrillOnActivate()) {
				Announce(Nav.Drill(), sound: false);
				return;
			}
			node.Activate();
		}

		/// <summary>
		/// Whether Enter on a drillable item drills (true) or activates it as a leaf
		/// (false). Default drills. Override when some drillable items open directly.
		/// </summary>
		protected virtual bool ShouldDrillOnActivate() => true;

		// ========================================
		// SEARCH (explicit ISearchable, routed to the engine)
		// ========================================

		int ISearchable.SearchItemCount => SearchCount();

		string ISearchable.GetSearchLabel(int index) {
			string label = SearchEntryText(index);
			return label == null ? null : TextFilter.FilterForSpeech(label);
		}

		void ISearchable.SearchMoveTo(int index) {
			MoveSearchCursor(index);
		}

		// Search is driven by the engine frontier by default. A handler whose search
		// spans multiple levels (the codex categories tab) overrides these three to
		// supply its own projection while still using the engine for navigation.

		/// <summary>Number of search results. Defaults to the engine's frontier size.</summary>
		protected virtual int SearchCount() => Nav.SearchCount();

		/// <summary>Raw (unfiltered) search text for result i. Defaults to the engine's frontier.</summary>
		protected virtual string SearchEntryText(int index) => Nav.SearchLabel(index);

		/// <summary>Move the cursor onto search result i and announce it.</summary>
		protected virtual void MoveSearchCursor(int index) {
			Announce(Nav.SearchMoveTo(index), sound: false);
		}

		// ========================================
		// RENDERING
		// ========================================

		/// <summary>
		/// Render a move into sound and speech. No-op moves (empty list, single item,
		/// drill into a leaf) produce nothing. Navigation moves play the cursor sound;
		/// drill, back, and search pass sound=false.
		/// </summary>
		protected void Announce(NavMove m, bool sound, bool interrupt = true) {
			if (!m.Moved || m.Item == null) return;
			if (sound) PlaySound(m.Wrapped ? "HUD_Click" : "HUD_Mouseover");
			string text = ComposeMove(m);
			if (string.IsNullOrWhiteSpace(text)) return;
			if (interrupt) SpeechPipeline.SpeakInterrupt(text);
			else SpeechPipeline.SpeakQueued(text);
		}

		/// <summary>Announce the current item with no context prefix and no navigation sound.</summary>
		protected void AnnounceCurrent(bool interrupt = true) {
			var node = Nav.Current();
			if (node == null) return;
			Announce(new NavMove {
				Moved = true, Wrapped = false, Item = node,
				ChangedAncestors = System.Array.Empty<NavItem>(),
			}, sound: false, interrupt: interrupt);
		}

		/// <summary>
		/// Announce the current item prefixed with its immediate parent as context, no
		/// navigation sound. For landing a cursor on a deep item out of band (a restore
		/// or a search result) where the parent group should be spoken.
		/// </summary>
		protected void AnnounceCurrentWithParent(bool interrupt = true) {
			var node = Nav.Current();
			if (node == null) return;
			var parent = Nav.CurrentParent();
			var ancestors = parent != null
				? (IReadOnlyList<NavItem>)new[] { parent }
				: System.Array.Empty<NavItem>();
			Announce(new NavMove {
				Moved = true, Wrapped = false, Item = node, ChangedAncestors = ancestors,
			}, sound: false, interrupt: interrupt);
		}

		private string ComposeMove(NavMove m) {
			string body = WidgetSpeech.Compose(m.Item, NavContext.None, GetTooltip(m.Item));
			return FormatWithContext(body, m.ChangedAncestors);
		}

		/// <summary>
		/// Prefix the spoken body with the context of ancestors that changed during the
		/// move. Default joins their announcements with ", "; the details screen overrides
		/// to use its parent-item phrasing.
		/// </summary>
		protected virtual string FormatWithContext(string body, IReadOnlyList<NavItem> ancestors) {
			if (ancestors.Count == 0) return body;
			return JoinAncestors(ancestors) + ", " + body;
		}

		protected static string JoinAncestors(IReadOnlyList<NavItem> ancestors) {
			var sb = new StringBuilder();
			for (int i = 0; i < ancestors.Count; i++) {
				if (i > 0) sb.Append(", ");
				sb.Append(ancestors[i].ContextLabel);
			}
			return sb.ToString();
		}

		/// <summary>Tooltip appended to an item's body. Null by default; the details screen reads live widget tooltips.</summary>
		protected virtual string GetTooltip(NavItem item) => null;
	}
}
