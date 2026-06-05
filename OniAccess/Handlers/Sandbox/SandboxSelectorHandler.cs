using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Sandbox {
	/// <summary>
	/// Modal menu for sandbox selectors. Adapts to categorized selectors
	/// (Element, Entity: level 0 = categories, level 1 = items) or flat selectors
	/// (Disease, Story: level 0 = all items).
	///
	/// Enter selects the item and pops back to the parameter menu. Type-ahead
	/// searches the items (activatable leaves) across all categories.
	/// </summary>
	public class SandboxSelectorHandler: NavTreeHandler {
		private readonly SandboxToolParameterMenu.SelectorValue _selector;
		private readonly bool _hasCategories;

		// Filtered option lists per category (or one list for flat selectors), built
		// once on activation — the option set is static for the selector's lifetime.
		private List<List<object>> _categoryOptions;
		private List<string> _categoryNames;

		public override string DisplayName => _selector.labelText;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries;

		static SandboxSelectorHandler() {
			var list = new List<HelpEntry>();
			list.AddRange(DrillNavHelpEntries);
			list.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			_helpEntries = list.AsReadOnly();
		}

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		public SandboxSelectorHandler(SandboxToolParameterMenu.SelectorValue selector)
			: base(null) {
			_selector = selector;
			_hasCategories = selector.filters != null && selector.filters.Length > 0;
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>();
			if (_categoryOptions == null) return roots;

			if (!_hasCategories) {
				if (_categoryOptions.Count > 0)
					return BuildItems(_categoryOptions[0]);
				return roots;
			}

			for (int c = 0; c < _categoryOptions.Count; c++) {
				var items = _categoryOptions[c];
				var name = _categoryNames[c];
				roots.Add(new MenuNode(
					() => name,
					children: () => BuildItems(items)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildItems(List<object> items) {
			var list = new List<NavItem>(items.Count);
			foreach (var opt in items) {
				var o = opt;
				list.Add(new MenuNode(
					() => _selector.getOptionName(o),
					activate: () => { Select(o); return true; }));
			}
			return list;
		}

		private void Select(object opt) {
			_selector.onValueChanged(opt);
			SpeechPipeline.SpeakInterrupt((string)STRINGS.ONIACCESS.STATES.SELECTED);
			HandlerStack.Pop();
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			BuildOptionLists();
			PlaySound("HUD_Click_Open");
			// Search the items, not the (possibly empty) category rows.
			Nav.SearchFilter = n => n.IsActivatable();
			base.OnActivate();
			AnnounceCurrent(interrupt: false);
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

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

		private void BuildOptionLists() {
			_categoryOptions = new List<List<object>>();
			_categoryNames = new List<string>();

			if (!_hasCategories) {
				// Flat list: all options in one list
				var all = new List<object>();
				if (_selector.options != null) {
					foreach (var opt in _selector.options)
						all.Add(opt);
				}
				_categoryOptions.Add(all);
				return;
			}

			// Categorized: build one list per top-level filter.
			// Empty categories are kept for consistent indexing; navigation skips
			// them and the IsActivatable search filter keeps them out of type-ahead.
			foreach (var filter in _selector.filters) {
				var items = new List<object>();
				if (_selector.options != null) {
					foreach (var opt in _selector.options) {
						if (filter.condition(opt))
							items.Add(opt);
					}
				}
				_categoryOptions.Add(items);
				_categoryNames.Add(filter.Name);
			}
		}
	}
}
