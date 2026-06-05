using System.Collections.Generic;

using OniAccess.Handlers.Sandbox;
using OniAccess.Handlers.Tools;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Unified action menu combining tools and build categories, driven by the
	/// NavTree engine. The item tree is: Tools (level 0) with individual tools as
	/// leaves (level 1); optionally Sandbox Tools (level 0) with sandbox tool leaves
	/// (level 1) when sandbox mode is active; then build categories (level 0) with
	/// subcategories (level 1) and buildings (level 2 leaves). Type-ahead searches the
	/// activatable leaves: tools, sandbox tools, and buildings.
	/// </summary>
	public class ActionMenuHandler: NavTreeHandler {
		private readonly HashedString _initialCategory;
		private readonly BuildingDef _initialDef;
		private List<BuildMenuData.CategoryGroup> _tree;

		private bool _sandboxActive;
		// Number of fixed categories before build categories (1 or 2).
		private int _fixedCategoryCount = 1;
		private int[] _restorePath;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries;

		static ActionMenuHandler() {
			var list = new List<HelpEntry>();
			list.AddRange(DrillNavHelpEntries);
			list.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			_helpEntries = list.AsReadOnly();
		}

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		public override string DisplayName => (string)STRINGS.ONIACCESS.BUILD_MENU.ACTION_MENU;

		/// <summary>
		/// Open fresh from the tile cursor.
		/// </summary>
		public ActionMenuHandler() {
			_initialCategory = HashedString.Invalid;
			_initialDef = null;
		}

		/// <summary>
		/// Open focused on a specific category (e.g., from tutorial notification).
		/// Cursor starts on the category at level 0.
		/// </summary>
		public ActionMenuHandler(HashedString category) {
			_initialCategory = category;
			_initialDef = null;
		}

		/// <summary>
		/// Return from placement (Tab in BuildToolHandler). Cursor starts on
		/// the building matching initialDef within the given category.
		/// </summary>
		public ActionMenuHandler(HashedString category, BuildingDef initialDef) {
			_initialCategory = category;
			_initialDef = initialDef;
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>();
			if (_tree == null) return roots;

			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.BUILD_MENU.TOOLS_CATEGORY,
				children: BuildToolChildren));

			if (_sandboxActive)
				roots.Add(new MenuNode(
					() => (string)STRINGS.ONIACCESS.SANDBOX.TOOLS_CATEGORY,
					children: BuildSandboxChildren));

			for (int c = 0; c < _tree.Count; c++) {
				var cat = _tree[c];
				roots.Add(new MenuNode(
					() => cat.DisplayName,
					children: () => BuildSubcategoryChildren(cat)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildToolChildren() {
			var list = new List<NavItem>();
			var tools = ToolHandler.AllTools;
			for (int i = 0; i < tools.Count; i++) {
				var tool = tools[i];
				list.Add(new MenuNode(() => tool.Label,
					activate: () => { ActivateToolItem(tool); return true; }));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildSandboxChildren() {
			var list = new List<NavItem>();
			var sbTools = GetSandboxToolInfos();
			for (int i = 0; i < sbTools.Count; i++) {
				var ti = sbTools[i];
				list.Add(new MenuNode(() => ti.text,
					activate: () => { ActivateSandboxToolItem(ti); return true; }));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildSubcategoryChildren(BuildMenuData.CategoryGroup cat) {
			var list = new List<NavItem>();
			var subs = cat.Subcategories;
			for (int s = 0; s < subs.Count; s++) {
				var sub = subs[s];
				list.Add(new MenuNode(() => sub.Name,
					children: () => BuildBuildingChildren(cat.Category, sub)));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildBuildingChildren(
				HashedString category, BuildMenuData.SubcategoryGroup sub) {
			var list = new List<NavItem>();
			var buildings = sub.Buildings;
			for (int b = 0; b < buildings.Count; b++) {
				var entry = buildings[b];
				list.Add(new MenuNode(() => entry.Label,
					activate: () => { ActivateBuilding(category, entry); return true; }));
			}
			return list;
		}

		// ========================================
		// ACTIVATION
		// ========================================

		private void ActivateBuilding(HashedString category, BuildMenuData.BuildingEntry entry) {
			var handler = new BuildToolHandler(category, entry.Def);
			HandlerStack.Replace(handler);
			handler.SuppressToolEvents = true;
			if (!BuildMenuData.SelectBuilding(entry.Def, category)) {
				handler.SuppressToolEvents = false;
				HandlerStack.Pop();
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.BUILD_MENU.NOT_BUILDABLE);
				return;
			}
			handler.SuppressToolEvents = false;
			handler.AnnounceInitialState();
		}

		private void ActivateToolItem(ModToolInfo tool) {
			if (tool.RequiresModeFirst) {
				HandlerStack.Replace(new ToolFilterHandler(tool));
			} else {
				ToolPickerHandler.ActivateTool(tool);
				HandlerStack.Replace(new ToolHandler());
			}
		}

		private void ActivateSandboxToolItem(ToolMenu.ToolInfo ti) {
			ActivateSandboxTool(ti);
			HandlerStack.Replace(new SandboxToolHandler());
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			_tree = BuildMenuData.GetFullBuildTree();
			_sandboxActive = Game.Instance != null && Game.Instance.SandboxModeActive;
			_fixedCategoryCount = _sandboxActive ? 2 : 1;
			Nav.SearchFilter = node => node.IsActivatable();

			if (_initialDef != null && _restorePath == null)
				_restorePath = FindDefPath(_initialDef, _initialCategory);

			base.OnActivate();

			if (_restorePath != null) {
				Nav.SetPath(_restorePath);
				_restorePath = null;
				AnnounceCurrentWithParent(interrupt: true);
			} else if (_initialCategory.IsValid && _initialDef == null) {
				MoveToCategory(_initialCategory);
			} else {
				AnnounceCurrent(interrupt: false);
			}
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		// ========================================
		// ESCAPE
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.TOOLTIP.CLOSED);
				HandlerStack.Pop();
				return true;
			}
			return false;
		}

		// ========================================
		// PRIVATE HELPERS
		// ========================================

		private void MoveToCategory(HashedString category) {
			for (int c = 0; c < _tree.Count; c++) {
				if (_tree[c].Category == category) {
					Nav.SetPath(new[] { _fixedCategoryCount + c });
					AnnounceCurrent(interrupt: true);
					return;
				}
			}
			// Category not found (e.g., all buildings behind unresearched tech).
			// Fall back to Tools.
			SpeechPipeline.SpeakQueued((string)STRINGS.ONIACCESS.BUILD_MENU.TOOLS_CATEGORY);
		}

		/// <summary>
		/// Path to the building matching def, preferring the given category. Returns
		/// null if not found. Matches the old flat-index restore: try within the named
		/// category first, then anywhere.
		/// </summary>
		private int[] FindDefPath(BuildingDef def, HashedString category) {
			if (_tree == null) return null;
			for (int c = 0; c < _tree.Count; c++) {
				bool categoryMatch = category.IsValid && _tree[c].Category == category;
				if (category.IsValid && !categoryMatch) continue;
				var subs = _tree[c].Subcategories;
				for (int s = 0; s < subs.Count; s++) {
					var buildings = subs[s].Buildings;
					for (int b = 0; b < buildings.Count; b++) {
						if (buildings[b].Def == def)
							return new[] { _fixedCategoryCount + c, s, b };
					}
				}
			}
			if (category.IsValid)
				return FindDefPath(def, HashedString.Invalid);
			return null;
		}

		// ========================================
		// SANDBOX TOOL HELPERS
		// ========================================

		/// <summary>
		/// Build a flat list of ToolInfo from all sandbox tool collections.
		/// Re-queries ToolMenu each call to avoid caching game state.
		/// </summary>
		private static List<ToolMenu.ToolInfo> GetSandboxToolInfos() {
			var list = new List<ToolMenu.ToolInfo>();
			if (ToolMenu.Instance != null) {
				foreach (var collection in ToolMenu.Instance.sandboxTools)
					foreach (var ti in collection.tools)
						if (ti.toolName != "SandboxSampleTool")
							list.Add(ti);
			}
			return list;
		}

		private static void ActivateSandboxTool(ToolMenu.ToolInfo ti) {
			ToolPickerHandler.ActivateSandboxTool(ti);
		}
	}
}
