using System.Collections.Generic;

using OniAccess.Speech;

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
	/// The list is re-read from CustomCategoryStore on every activation, so it
	/// never holds stale config. The editor performs Rename and Delete itself,
	/// then hands this handler a message to speak when control returns via
	/// AnnounceOnReturn.
	/// </summary>
	public class CustomCategoryManagerHandler: NestedMenuHandler {
		private List<CustomScannerCategory> _categories = new List<CustomScannerCategory>();

		public override string DisplayName => (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.TITLE;
		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		protected override int MaxLevel => 0;
		protected override int SearchLevel => 0;

		public CustomCategoryManagerHandler() : base(null) {
			var help = new List<HelpEntry>();
			help.AddRange(NestedNavHelpEntries);
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
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			RefreshList();

			if (_pendingFocusId != null) {
				ApplyFocus(_pendingFocusId);
				_pendingFocusId = null;
			} else {
				ClampIndices();
			}
			_search.Clear();
			SuppressSearchThisFrame();

			string opening = _pendingAnnouncement ?? (string)DisplayName;
			_pendingAnnouncement = null;
			SpeechPipeline.SpeakInterrupt(opening);
			SpeakCurrentItemQueued();
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

		// SpeakCurrentItem always interrupts; on open we want the title (or the
		// pending announcement) first and the focused item queued behind it.
		private void SpeakCurrentItemQueued() {
			var indices = new int[] { GetIndex(0), GetIndex(1) };
			int count = GetItemCount(Level, indices);
			if (count == 0) return;
			string label = GetItemLabel(Level, indices);
			if (string.IsNullOrWhiteSpace(label)) return;
			SpeechPipeline.SpeakQueued(label);
		}

		private void RefreshList() {
			_categories = new List<CustomScannerCategory>(CustomCategoryStore.GetAll());
		}

		private void ClampIndices() {
			int level0Count = _categories.Count + 1;
			int idx0 = GetIndex(0);
			if (idx0 >= level0Count) SetIndex(0, level0Count - 1);
			if (idx0 < 0) SetIndex(0, 0);
		}

		// ========================================
		// LEVEL DESCRIPTION
		// ========================================

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return _categories.Count + 1;
			return 0;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level != 0) return null;
			int i = indices[0];
			if (i == _categories.Count) return (string)STRINGS.ONIACCESS.CUSTOM_CATEGORY.CREATE_NEW;
			if (i < 0 || i >= _categories.Count) return null;
			return _categories[i].Name;
		}

		protected override string GetParentLabel(int level, int[] indices) => null;

		// ========================================
		// LEAF ACTIVATION
		// ========================================

		protected override void ActivateLeafItem(int[] indices) {
			int i = indices[0];
			if (i == _categories.Count) {
				CreateAndEdit();
				return;
			}
			if (i < 0 || i >= _categories.Count) return;
			OpenEditor(_categories[i].Id);
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
		// SEARCH
		// ========================================

		protected override int GetSearchItemCount(int[] indices) => _categories.Count;

		protected override string GetSearchItemLabel(int flatIndex) {
			if (flatIndex < 0 || flatIndex >= _categories.Count) return null;
			return _categories[flatIndex].Name;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			outIndices[0] = flatIndex;
		}

		// ========================================
		// FOCUS RETENTION ACROSS THE EDITOR
		// ========================================

		private string _pendingFocusId;
		private string _pendingAnnouncement;

		private void ApplyFocus(string id) {
			for (int i = 0; i < _categories.Count; i++) {
				if (_categories[i].Id == id) {
					SetIndex(0, i);
					return;
				}
			}
			ClampIndices();
		}
	}
}
