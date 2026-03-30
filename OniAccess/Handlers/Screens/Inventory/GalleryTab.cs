using System.Collections.Generic;

using Database;

using OniAccess.Input;
using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Gallery tab: 3-level NestedMenuHandler.
	/// Level 0 = top categories (Tops, Bottoms, Buildings, etc.)
	/// Level 1 = subcategories within a category
	/// Level 2 = individual permit items within a subcategory
	///
	/// Enter on a level-2 item jumps to the detail tab.
	/// Type-ahead searches across all level-2 items globally.
	/// Ctrl+F toggles a filter overlay (ownership + DLC cycling).
	/// Empty subcategories/categories are hidden when filtered.
	/// </summary>
	internal class GalleryTab: NestedMenuHandler, IScreenTab {
		private readonly InventoryScreenHandler _parent;
		private List<FlatItem> _flatItems;

		// Filter state
		private int _ownershipFilter; // 0=all, 1=owned, 2=doubles
		private string _dlcFilter;    // null=all, otherwise DLC ID
		private List<string> _dlcIds;

		// Whether filter overlay is active (Ctrl+F mode)
		private bool _filterMode;
		private int _filterCursor; // 0=ownership, 1=DLC

		internal GalleryTab(InventoryScreenHandler parent) : base(screen: null) {
			_parent = parent;
			_search.GroupOf = GetSearchGroup;
		}

		public string TabName => (string)STRINGS.ONIACCESS.INVENTORY.GALLERY_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => NestedNavHelpEntries;

		internal int OwnershipFilter => _ownershipFilter;
		internal string DlcFilter => _dlcFilter;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			_flatItems = null;
			_dlcIds = null;
			_filterMode = false;
			ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(label);
			}
		}

		public void OnTabDeactivated() {
			_search.Clear();
			_filterMode = false;
		}

		public bool HandleInput() {
			if (_filterMode)
				return HandleFilterInput();

			if (InputUtil.CtrlHeld() && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F)) {
				EnterFilterMode();
				return true;
			}

			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			if (_filterMode && e.TryConsume(Action.Escape)) {
				ExitFilterMode();
				return true;
			}
			return base.HandleKeyDown(e);
		}

		/// <summary>
		/// Reactivate the gallery tab, landing on the permit that was
		/// previously being viewed in the detail tab.
		/// </summary>
		internal void OnTabActivatedOnPermit(bool announce, PermitResource permit) {
			_flatItems = null;
			if (permit == null || !NavigateToPermit(permit)) {
				// Don't reset — preserve cursor position
			}
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(label);
			}
		}

		// ========================================
		// NestedMenuHandler abstracts
		// ========================================

		protected override int MaxLevel => 2;
		protected override int SearchLevel => 2;

		protected override bool ShouldDrillOnActivate() {
			// At level 0 and 1, drill down. At level 2, activate (jump to detail).
			return Level < 2;
		}

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0)
				return GetFilteredCategoryCount();

			string catId = GetFilteredCategoryId(indices[0]);
			if (catId == null) return 0;

			if (level == 1)
				return GetFilteredSubcategoryCount(catId);

			string subId = GetFilteredSubcategoryId(catId, indices[1]);
			if (subId == null) return 0;

			return InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter);
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0)
				return GetFilteredCategoryName(indices[0]);

			string catId = GetFilteredCategoryId(indices[0]);
			if (catId == null) return null;

			if (level == 1) {
				string subId = GetFilteredSubcategoryId(catId, indices[1]);
				return InventoryHelper.GetSubcategoryName(subId);
			}

			string subcatId = GetFilteredSubcategoryId(catId, indices[1]);
			if (subcatId == null) return null;

			var permit = InventoryHelper.GetFilteredPermitAt(
				subcatId, indices[2], _ownershipFilter, _dlcFilter);
			return InventoryHelper.GetPermitLabel(permit);
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level <= 0) return null;
			if (level == 1) return GetFilteredCategoryName(indices[0]);

			string catId = GetFilteredCategoryId(indices[0]);
			if (catId == null) return null;
			string subId = GetFilteredSubcategoryId(catId, indices[1]);
			return InventoryHelper.GetSubcategoryName(subId);
		}

		protected override void ActivateLeafItem(int[] indices) {
			string catId = GetFilteredCategoryId(indices[0]);
			if (catId == null) return;
			string subId = GetFilteredSubcategoryId(catId, indices[1]);
			if (subId == null) return;
			var permit = InventoryHelper.GetFilteredPermitAt(
				subId, indices[2], _ownershipFilter, _dlcFilter);
			if (permit == null) return;

			PlaySound("HUD_Click_Open");
			_parent.JumpToDetailTab(permit);
		}

		// ========================================
		// Search across all permit items
		// ========================================

		protected override int GetSearchItemCount(int[] indices) {
			return GetAllSearchableItems().Count;
		}

		protected override string GetSearchItemLabel(int flatIndex) {
			var all = GetAllSearchableItems();
			if (flatIndex < 0 || flatIndex >= all.Count) return null;
			var item = all[flatIndex];
			return item.permit?.Name;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			var all = GetAllSearchableItems();
			if (flatIndex < 0 || flatIndex >= all.Count) return;
			var item = all[flatIndex];
			outIndices[0] = item.catIdx;
			outIndices[1] = item.subIdx;
			outIndices[2] = item.itemIdx;
		}

		// ========================================
		// FILTER MODE (Ctrl+F)
		// ========================================

		private void EnterFilterMode() {
			_filterMode = true;
			_filterCursor = 0;
			SpeakFilterLine();
		}

		private void ExitFilterMode() {
			_filterMode = false;
			_flatItems = null;
			PlaySound("HUD_Click");
			// Reannounce current item after filter change
			if (ItemCount > 0)
				SpeakCurrentItem();
			else
				SpeechPipeline.SpeakInterrupt(TabName);
		}

		private bool HandleFilterInput() {
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow)) {
				if (_filterCursor > 0) {
					_filterCursor--;
					PlaySound("HUD_Mouseover");
					SpeakFilterLine();
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow)) {
				if (_filterCursor < 1) {
					_filterCursor++;
					PlaySound("HUD_Mouseover");
					SpeakFilterLine();
				}
				return true;
			}
			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow) ||
				UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
				int dir = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow) ? 1 : -1;
				if (_filterCursor == 0)
					CycleOwnershipFilter(dir);
				else
					CycleDlcFilter(dir);
				_flatItems = null;
				PlaySound("HUD_Click");
				SpeakFilterLine();
				return true;
			}
			return false;
		}

		private void CycleOwnershipFilter(int dir) {
			_ownershipFilter = (_ownershipFilter + dir + 3) % 3;
		}

		private void CycleDlcFilter(int dir) {
			var ids = GetDlcIdList();
			// ids[0] = null (all), then DLC IDs
			int current = 0;
			for (int i = 0; i < ids.Count; i++) {
				if (ids[i] == _dlcFilter) { current = i; break; }
			}
			current = (current + dir + ids.Count) % ids.Count;
			_dlcFilter = ids[current];
		}

		private void SpeakFilterLine() {
			if (_filterCursor == 0) {
				string label = GetOwnershipFilterLabel();
				SpeechPipeline.SpeakInterrupt(string.Format(
					(string)STRINGS.ONIACCESS.INVENTORY.OWNERSHIP_FILTER, label));
			} else {
				string label = InventoryHelper.GetDlcDisplayName(_dlcFilter);
				SpeechPipeline.SpeakInterrupt(string.Format(
					(string)STRINGS.ONIACCESS.INVENTORY.DLC_FILTER, label));
			}
		}

		private string GetOwnershipFilterLabel() {
			switch (_ownershipFilter) {
				case 0: return (string)STRINGS.ONIACCESS.INVENTORY.FILTER_OWNERSHIP_ALL;
				case 1: return (string)STRINGS.ONIACCESS.INVENTORY.FILTER_OWNERSHIP_OWNED;
				case 2: return (string)STRINGS.ONIACCESS.INVENTORY.FILTER_OWNERSHIP_DOUBLES;
				default: return "";
			}
		}

		private List<string> GetDlcIdList() {
			if (_dlcIds != null) return _dlcIds;
			_dlcIds = new List<string> { null }; // null = "all"
			_dlcIds.AddRange(InventoryHelper.GetActiveDlcIds());
			return _dlcIds;
		}

		// ========================================
		// FILTERED DATA ACCESS
		// ========================================

		/// <summary>
		/// Get count of categories that have at least one visible item after filtering.
		/// </summary>
		private int GetFilteredCategoryCount() {
			if (_ownershipFilter == 0 && _dlcFilter == null)
				return InventoryHelper.CategoryCount;

			int count = 0;
			for (int i = 0; i < InventoryHelper.CategoryCount; i++) {
				if (CategoryHasFilteredItems(InventoryHelper.GetCategoryId(i)))
					count++;
			}
			return count;
		}

		/// <summary>
		/// Map a filtered category index to the real category ID.
		/// </summary>
		private string GetFilteredCategoryId(int filteredIndex) {
			if (_ownershipFilter == 0 && _dlcFilter == null)
				return InventoryHelper.GetCategoryId(filteredIndex);

			int seen = 0;
			for (int i = 0; i < InventoryHelper.CategoryCount; i++) {
				string id = InventoryHelper.GetCategoryId(i);
				if (CategoryHasFilteredItems(id)) {
					if (seen == filteredIndex) return id;
					seen++;
				}
			}
			return null;
		}

		private string GetFilteredCategoryName(int filteredIndex) {
			string id = GetFilteredCategoryId(filteredIndex);
			if (id == null) return null;
			return InventoryOrganization.GetCategoryName(id);
		}

		private bool CategoryHasFilteredItems(string categoryId) {
			var subIds = InventoryHelper.GetSubcategoryIds(categoryId);
			if (subIds == null) return false;
			foreach (string subId in subIds) {
				if (InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter) > 0)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Count subcategories within a category that have filtered items.
		/// </summary>
		private int GetFilteredSubcategoryCount(string categoryId) {
			var subIds = InventoryHelper.GetSubcategoryIds(categoryId);
			if (subIds == null) return 0;

			if (_ownershipFilter == 0 && _dlcFilter == null)
				return subIds.Count;

			int count = 0;
			foreach (string subId in subIds) {
				if (InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter) > 0)
					count++;
			}
			return count;
		}

		/// <summary>
		/// Map a filtered subcategory index to the real subcategory ID.
		/// </summary>
		private string GetFilteredSubcategoryId(string categoryId, int filteredIndex) {
			var subIds = InventoryHelper.GetSubcategoryIds(categoryId);
			if (subIds == null) return null;

			if (_ownershipFilter == 0 && _dlcFilter == null) {
				if (filteredIndex < 0 || filteredIndex >= subIds.Count) return null;
				return subIds[filteredIndex];
			}

			int seen = 0;
			foreach (string subId in subIds) {
				if (InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter) > 0) {
					if (seen == filteredIndex) return subId;
					seen++;
				}
			}
			return null;
		}

		// ========================================
		// FLAT SEARCH INDEX
		// ========================================

		private struct FlatItem {
			internal PermitResource permit;
			internal int catIdx;
			internal int subIdx;
			internal int itemIdx;
		}

		private List<FlatItem> GetAllSearchableItems() {
			if (_flatItems != null) return _flatItems;
			var result = new List<FlatItem>();

			int catFiltered = 0;
			for (int c = 0; c < InventoryHelper.CategoryCount; c++) {
				string catId = InventoryHelper.GetCategoryId(c);
				if (!CategoryHasFilteredItems(catId)) continue;

				var subIds = InventoryHelper.GetSubcategoryIds(catId);
				if (subIds == null) { catFiltered++; continue; }

				int subFiltered = 0;
				foreach (string subId in subIds) {
					int filteredCount = InventoryHelper.GetFilteredPermitCount(
						subId, _ownershipFilter, _dlcFilter);
					if (filteredCount == 0) continue;

					for (int p = 0; p < filteredCount; p++) {
						var permit = InventoryHelper.GetFilteredPermitAt(
							subId, p, _ownershipFilter, _dlcFilter);
						if (permit != null) {
							result.Add(new FlatItem {
								permit = permit,
								catIdx = catFiltered,
								subIdx = subFiltered,
								itemIdx = p
							});
						}
					}
					subFiltered++;
				}
				catFiltered++;
			}

			_flatItems = result;
			return result;
		}

		private int GetSearchGroup(int flatIndex) {
			// All items are in the same group (no category distinction for search ordering)
			return 0;
		}

		// ========================================
		// PERMIT NAVIGATION
		// ========================================

		/// <summary>
		/// Navigate to a specific permit in the hierarchy.
		/// Used when returning from the detail tab.
		/// </summary>
		private bool NavigateToPermit(PermitResource permit) {
			var all = GetAllSearchableItems();
			for (int i = 0; i < all.Count; i++) {
				if (all[i].permit == permit) {
					SetIndex(0, all[i].catIdx);
					SetIndex(1, all[i].subIdx);
					SetIndex(2, all[i].itemIdx);
					Level = 2;
					_search.Clear();
					SuppressSearchThisFrame();
					return true;
				}
			}
			return false;
		}
	}
}
