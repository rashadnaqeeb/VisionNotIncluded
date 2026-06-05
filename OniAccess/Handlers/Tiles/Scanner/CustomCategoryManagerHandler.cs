using System.Collections.Generic;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Tiles.Scanner {
	/// <summary>
	/// Modal menu for managing custom scanner categories, pushed from the
	/// scanner config section.
	///
	/// A flat list: one row per custom category (creation order) plus a
	/// "Create new" row at the end. Enter on a category opens its editor
	/// (filter toggles plus Rename and Delete). Enter on "Create new" makes a
	/// category named "Custom category N" and opens its editor immediately;
	/// the editor's Rename is how the user gives it a real name.
	///
	/// The list is read from CustomCategoryStore on every access, so it never holds
	/// stale config. The editor performs Rename and Delete itself, then hands this
	/// handler a message to speak when control returns via AnnounceOnReturn.
	/// </summary>
	public class CustomCategoryManagerHandler: NavTreeHandler {
		private string _pendingFocusId;
		private string _pendingAnnouncement;

		public override string DisplayName => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.TITLE;
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		// Type-ahead targets the categories; the "Create new" command row is excluded.
		protected override SearchScope SearchScope => SearchScope.Roots;

		public CustomCategoryManagerHandler() : base(null) {
			Nav.SearchFilter = n => n.RoleKey != NavRoles.Button;
			var help = new List<HelpEntry>();
			help.AddRange(DrillNavHelpEntries);
			help.Add(new HelpEntry("Enter", STRINGS.ONIACCESS.CUSTOM_CATEGORY.HELP_EDIT));
			help.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			HelpEntries = help.AsReadOnly();
		}

		/// <summary>Speak <paramref name="message"/> instead of the menu title
		/// the next time this handler activates. The editor calls this before
		/// popping after a delete, so the confirmation survives the return.</summary>
		public void AnnounceOnReturn(string message) {
			_pendingAnnouncement = message;
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var cats = CustomCategoryStore.GetAll();
			var roots = new List<NavItem>(cats.Count + 1);
			foreach (var c in cats) {
				var id = c.Id;
				roots.Add(new MenuNode(
					() => CustomCategoryStore.Find(id)?.Name,
					activate: () => { OpenEditor(id); return true; }));
			}
			roots.Add(new MenuNode(
				() => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.CREATE_NEW,
				activate: () => { CreateAndEdit(); return true; },
				roleKey: NavRoles.Button));
			return roots;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");

			if (_pendingFocusId != null) {
				ApplyFocus(_pendingFocusId);
				_pendingFocusId = null;
			} else {
				Nav.ClampToTree();
			}
			_search.Clear();
			SuppressSearchThisFrame();

			string opening = _pendingAnnouncement ?? (string)DisplayName;
			_pendingAnnouncement = null;
			SpeechPipeline.SpeakInterrupt(opening);
			AnnounceCurrent(interrupt: false);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
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
		// ACTIONS
		// ========================================

		private void OpenEditor(string id) {
			HandlerStack.Push(new CustomCategoryEditorHandler(id, this));
		}

		private void CreateAndEdit() {
			var added = CustomCategoryStore.Add(NextDefaultName());
			ScannerNavigator.Instance?.InvalidateSnapshot();
			// Land on the new category when the editor pops back here.
			_pendingFocusId = added.Id;
			HandlerStack.Push(new CustomCategoryEditorHandler(added.Id, this));
		}

		// The lowest "Custom category N" not already taken, so fresh categories
		// read 1, 2, 3 and a number freed by a delete is reused rather than
		// colliding with a surviving default name.
		private static string NextDefaultName() {
			var existing = new HashSet<string>();
			foreach (var c in CustomCategoryStore.GetAll())
				existing.Add(c.Name);
			int n = 1;
			string name;
			do {
				name = string.Format(STRINGS.ONIACCESS.CUSTOM_CATEGORY.DEFAULT_NAME, n);
				n++;
			} while (existing.Contains(name));
			return name;
		}

		// ========================================
		// FOCUS RETENTION ACROSS THE EDITOR
		// ========================================

		private void ApplyFocus(string id) {
			var cats = CustomCategoryStore.GetAll();
			for (int i = 0; i < cats.Count; i++) {
				if (cats[i].Id == id) {
					Nav.SetPath(new[] { i });
					return;
				}
			}
			Nav.ClampToTree();
		}
	}
}
