using System.Collections.Generic;

using OniAccess.Speech;

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
	public class CustomCategoryEditorHandler: NestedMenuHandler {
		private readonly string _id;
		private readonly CustomCategoryManagerHandler _manager;
		private string _pendingAnnouncement;

		private static int CategoryCount => ScannerTaxonomy.CategoryOrder.Length;
		private static int RenameIndex => CategoryCount;
		private static int DeleteIndex => CategoryCount + 1;

		public override string DisplayName {
			get {
				var category = CustomCategoryStore.Find(_id);
				return category != null ? category.Name : "";
			}
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 0;

		public CustomCategoryEditorHandler(string id, CustomCategoryManagerHandler manager) : base(null) {
			_id = id;
			_manager = manager;
			var help = new List<HelpEntry>();
			help.AddRange(NestedNavHelpEntries);
			help.Add(new HelpEntry("Enter", STRINGS.ONIACCESS.CUSTOM_CATEGORY.HELP_TOGGLE));
			help.Add(new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE));
			HelpEntries = help.AsReadOnly();
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
			var indices = new int[] { GetIndex(0), GetIndex(1) };
			string label = GetItemLabel(Level, indices);
			if (!string.IsNullOrWhiteSpace(label))
				SpeechPipeline.SpeakQueued(label);
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;
			if (e.TryConsume(Action.Escape)) {
				if (Level > 0) {
					HandleLeftRight(-1, 0);
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
		// LEVEL DESCRIPTION
		// ========================================

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return CategoryCount + 2;
			if (level == 1) {
				if (indices[0] < 0 || indices[0] >= CategoryCount) return 0;
				string category = ScannerTaxonomy.CategoryOrder[indices[0]];
				return 1 + ScannerTaxonomy.NamedSubcategories(category).Length;
			}
			return 0;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0) {
				int i = indices[0];
				if (i == RenameIndex) return (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.RENAME;
				if (i == DeleteIndex) return (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.DELETE;
				return ScannerNavigator.GetCategoryName(ScannerTaxonomy.CategoryOrder[i]);
			}
			if (level == 1) {
				string category = ScannerTaxonomy.CategoryOrder[indices[0]];
				if (indices[1] == 0)
					return WithState(
						(string)STRINGS.ONIACCESS.SCANNER.SUBCATEGORIES.ALL,
						CustomCategoryStore.IsAll(_id, category));
				string sub = ScannerTaxonomy.NamedSubcategories(category)[indices[1] - 1];
				return WithState(
					ScannerNavigator.GetSubcategoryName(sub),
					CustomCategoryStore.IsSub(_id, category, sub));
			}
			return null;
		}

		private static string WithState(string label, bool on) {
			return label + ", " + (on
				? (string)STRINGS.ONIACCESS.STATES.ON
				: (string)STRINGS.ONIACCESS.STATES.OFF);
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level >= 1 && indices[0] >= 0 && indices[0] < CategoryCount)
				return ScannerNavigator.GetCategoryName(ScannerTaxonomy.CategoryOrder[indices[0]]);
			return null;
		}

		// ========================================
		// LEAF ACTIVATION
		// ========================================

		protected override void ActivateLeafItem(int[] indices) {
			if (Level == 0) {
				// Taxonomy categories drill rather than leaf-activate; only the
				// Rename and Delete rows reach here at level 0.
				if (indices[0] == RenameIndex) OpenRenamePrompt();
				else if (indices[0] == DeleteIndex) DeleteCategory();
				return;
			}

			string category = ScannerTaxonomy.CategoryOrder[indices[0]];
			if (indices[1] == 0) {
				CustomCategoryStore.SetAll(_id, category, !CustomCategoryStore.IsAll(_id, category));
			} else {
				string sub = ScannerTaxonomy.NamedSubcategories(category)[indices[1] - 1];
				CustomCategoryStore.SetSub(_id, category, sub,
					!CustomCategoryStore.IsSub(_id, category, sub));
			}
			ScannerNavigator.Instance?.InvalidateSnapshot();
			PlaySound("HUD_Click");
			SpeakCurrentItem();
		}

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

		// ========================================
		// SEARCH (taxonomy categories at level 0)
		// ========================================

		protected override int GetSearchItemCount(int[] indices) => CategoryCount;

		protected override string GetSearchItemLabel(int flatIndex) {
			if (flatIndex < 0 || flatIndex >= CategoryCount) return null;
			return ScannerNavigator.GetCategoryName(ScannerTaxonomy.CategoryOrder[flatIndex]);
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			outIndices[0] = flatIndex;
		}
	}
}
