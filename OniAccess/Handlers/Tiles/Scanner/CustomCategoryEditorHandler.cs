using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tiles.Scanner {
	/// <summary>
	/// Editor for one custom scanner category, pushed from the manager.
	///
	/// Level 0: one row per taxonomy category (drill in to edit its filters),
	/// then a Rename row and a Delete row.
	///
	/// Level 1 (taxonomy categories only): an "All" toggle (the whole category)
	/// plus a checkbox per named subcategory. Enter toggles in place. State is
	/// read live from the store each time a row is spoken, so the supersede
	/// behaviour (turning All on checks every sub) always reads truthfully.
	///
	/// Every toggle invalidates the scanner snapshot so the change takes effect
	/// on the next scan. Rename edits in place and stays here; Delete removes
	/// the category and pops back to the manager.
	/// </summary>
	public class CustomCategoryEditorHandler: NavTreeHandler {
		private readonly string _id;
		private readonly CustomCategoryManagerHandler _manager;
		private string _pendingAnnouncement;

		public override string DisplayName {
			get {
				var category = CustomCategoryStore.Find(_id);
				return category != null ? category.Name : "";
			}
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		// Type-ahead always targets the taxonomy categories (level 0), even while
		// drilled into one's subcategories; the Rename/Delete command rows are excluded.
		protected override SearchScope SearchScope => SearchScope.Roots;

		public CustomCategoryEditorHandler(string id, CustomCategoryManagerHandler manager) : base(null) {
			_id = id;
			_manager = manager;
			Nav.SearchFilter = n => n.RoleKey != NavRoles.Button;
			var help = new List<HelpEntry>();
			help.AddRange(DrillNavHelpEntries);
			help.Add(new HelpEntry("Enter", STRINGS.ONIACCESS.CUSTOM_CATEGORY.HELP_TOGGLE));
			help.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			HelpEntries = help.AsReadOnly();
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>();
			// Keyword rows first, mirroring the scan cycle where keyword
			// subcategories sort ahead of the taxonomy ones. Enter removes.
			foreach (var keyword in CustomCategoryStore.GetKeywords(_id)) {
				var k = keyword;
				roots.Add(new MenuNode(
					() => k,
					activate: () => { RemoveKeyword(k); return true; }));
			}
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.ADD_KEYWORD,
				activate: () => { OpenKeywordPrompt(); return true; },
				roleKey: NavRoles.Button));
			foreach (var category in ScannerTaxonomy.CategoryOrder) {
				var cat = category;
				roots.Add(new MenuNode(
					() => ScannerNavigator.GetCategoryName(cat),
					children: () => BuildSubcategories(cat)));
			}
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.RENAME,
				activate: () => { OpenRenamePrompt(); return true; },
				roleKey: NavRoles.Button));
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.DELETE,
				activate: () => { DeleteCategory(); return true; },
				roleKey: NavRoles.Button));
			return roots;
		}

		private IReadOnlyList<NavItem> BuildSubcategories(string category) {
			var subs = ScannerTaxonomy.NamedSubcategories(category);
			var list = new List<NavItem>(1 + subs.Length);
			list.Add(new MenuNode(
				() => WithState((string)STRINGS.ONIACCESS.SCANNER.SUBCATEGORIES.ALL,
					CustomCategoryStore.IsAll(_id, category)),
				activate: () => { ToggleAll(category); return true; }));
			foreach (var sub in subs) {
				var s = sub;
				list.Add(new MenuNode(
					() => WithState(ScannerNavigator.GetSubcategoryName(s),
						CustomCategoryStore.IsSub(_id, category, s)),
					activate: () => { ToggleSub(category, s); return true; }));
			}
			return list;
		}

		private static string WithState(string label, bool on) {
			return label + ", " + (on
				? (string)STRINGS.ONIACCESS.STATES.ON
				: (string)STRINGS.ONIACCESS.STATES.OFF);
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			base.OnActivate();
			string opening = _pendingAnnouncement ?? (string)DisplayName;
			_pendingAnnouncement = null;
			SpeechPipeline.SpeakInterrupt(opening);
			AnnounceCurrent(interrupt: false);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				if (Nav.Depth > 0) {
					Back();
					return true;
				}
				Close();
				return true;
			}
			return false;
		}

		private void Close() {
			SpeechPipeline.SpeakInterrupt(STRINGS.ONIACCESS.TOOLTIP.CLOSED);
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}

		// ========================================
		// TOGGLES
		// ========================================

		private void ToggleAll(string category) {
			CustomCategoryStore.SetAll(_id, category, !CustomCategoryStore.IsAll(_id, category));
			ScannerNavigator.Instance?.InvalidateSnapshot();
			PlaySound("HUD_Click");
			AnnounceCurrent();
		}

		private void ToggleSub(string category, string sub) {
			CustomCategoryStore.SetSub(_id, category, sub,
				!CustomCategoryStore.IsSub(_id, category, sub));
			ScannerNavigator.Instance?.InvalidateSnapshot();
			PlaySound("HUD_Click");
			AnnounceCurrent();
		}

		// ========================================
		// KEYWORDS
		// ========================================

		private void OpenKeywordPrompt() {
			string prompt = (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.KEYWORD_PROMPT;
			HandlerStack.Push(new TextPromptHandler(prompt, "", keyword => {
				if (string.IsNullOrWhiteSpace(keyword)) return;
				bool added = CustomCategoryStore.AddKeyword(_id, keyword);
				if (added) ScannerNavigator.Instance?.InvalidateSnapshot();
				// Spoken when this editor reactivates as the prompt pops. Don't
				// claim an add that the store rejected as a duplicate.
				_pendingAnnouncement = string.Format(
					added
						? (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.KEYWORD_ADDED
						: (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.KEYWORD_DUPLICATE,
					keyword.Trim());
			}));
		}

		private void RemoveKeyword(string keyword) {
			CustomCategoryStore.RemoveKeyword(_id, keyword);
			ScannerNavigator.Instance?.InvalidateSnapshot();
			PlaySound("HUD_Click_Close");
			SpeechPipeline.SpeakInterrupt(string.Format(
				STRINGS.ONIACCESS.CUSTOM_CATEGORY.KEYWORD_REMOVED, keyword));
			// The removed row is gone; pull the cursor back into the live tree and
			// read where it landed so the user isn't left on a silent unknown row.
			Nav.ClampToTree();
			AnnounceCurrent(interrupt: false);
		}

		// ========================================
		// RENAME / DELETE
		// ========================================

		private void OpenRenamePrompt() {
			string prompt = (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.RENAME_PROMPT;
			HandlerStack.Push(new TextPromptHandler(prompt, DisplayName, name => {
				if (string.IsNullOrWhiteSpace(name)) return;
				CustomCategoryStore.Rename(_id, name);
				ScannerNavigator.Instance?.InvalidateSnapshot();
				// Spoken when this editor reactivates as the prompt pops.
				_pendingAnnouncement = string.Format(
					STRINGS.ONIACCESS.CUSTOM_CATEGORY.RENAMED, name);
			}));
		}

		private void DeleteCategory() {
			string name = DisplayName;
			CustomCategoryStore.Delete(_id);
			ScannerNavigator.Instance?.InvalidateSnapshot();
			_manager.AnnounceOnReturn(string.Format(
				STRINGS.ONIACCESS.CUSTOM_CATEGORY.DELETED, name));
			PlaySound("HUD_Click_Close");
			HandlerStack.Pop();
		}
	}
}
