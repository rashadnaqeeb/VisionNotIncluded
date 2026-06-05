using System.Collections.Generic;

using Database;

using OniAccess.Input;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Gallery tab: 3-level tree.
	/// Level 0 = top categories (Tops, Bottoms, Buildings, etc.)
	/// Level 1 = subcategories within a category
	/// Level 2 = individual permit items within a subcategory
	///
	/// Enter on a level-2 item jumps to the detail tab. Type-ahead searches across
	/// all permit items. Ctrl+F toggles a filter overlay (ownership + DLC cycling).
	/// The tree includes only categories/subcategories/permits that survive the
	/// active filter, so empty ones are hidden.
	/// </summary>
	internal class GalleryTab: NavTreeHandler, IScreenTab {
		private readonly InventoryScreenHandler _parent;

		// Filter state
		private int _ownershipFilter; // 0=all, 1=owned, 2=doubles
		private string _dlcFilter;    // null=all, otherwise DLC ID
		private List<string> _dlcIds;

		// Whether filter overlay is active (Ctrl+F mode)
		private bool _filterMode;
		private int _filterCursor; // 0=ownership, 1=DLC

		internal GalleryTab(InventoryScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.INVENTORY.GALLERY_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => DrillNavHelpEntries;

		internal int OwnershipFilter => _ownershipFilter;
		internal string DlcFilter => _dlcFilter;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			_dlcIds = null;
			_filterMode = false;
			ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				AnnounceCurrent(interrupt: false);
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
			if (base.HandleKeyDown(e))
				return true;
			if (_filterMode && e.TryConsume(Action.Escape)) {
				ExitFilterMode();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Reactivate the gallery tab, landing on the permit that was
		/// previously being viewed in the detail tab.
		/// </summary>
		internal void OnTabActivatedOnPermit(bool announce, PermitResource permit) {
			if (permit != null)
				NavigateToPermit(permit);
			// else: preserve cursor position
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				AnnounceCurrent(interrupt: false);
		}

		// ========================================
		// TREE CONSTRUCTION (only filtered-visible items)
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>();
			for (int c = 0; c < InventoryHelper.CategoryCount; c++) {
				string catId = InventoryHelper.GetCategoryId(c);
				if (!CategoryHasFilteredItems(catId)) continue;
				var id = catId;
				roots.Add(new MenuNode(
					() => InventoryOrganization.GetCategoryName(id),
					children: () => BuildSubcategories(id)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildSubcategories(string categoryId) {
			var subIds = InventoryHelper.GetSubcategoryIds(categoryId);
			var list = new List<NavItem>();
			if (subIds == null) return list;
			foreach (var subId in subIds) {
				if (InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter) == 0)
					continue;
				var sid = subId;
				list.Add(new MenuNode(
					() => InventoryHelper.GetSubcategoryName(sid),
					children: () => BuildPermits(sid)));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildPermits(string subId) {
			int count = InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter);
			var list = new List<NavItem>(count);
			for (int p = 0; p < count; p++) {
				var permit = InventoryHelper.GetFilteredPermitAt(subId, p, _ownershipFilter, _dlcFilter);
				if (permit == null) continue;
				list.Add(new MenuNode(
					() => InventoryHelper.GetPermitLabel(permit),
					activate: () => { ActivatePermit(permit); return true; },
					searchText: () => permit.Name));
			}
			return list;
		}

		private void ActivatePermit(PermitResource permit) {
			PlaySound("HUD_Click_Open");
			_parent.JumpToDetailTab(permit);
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
			PlaySound("HUD_Click");
			// Filter changes may have shrunk the tree; keep the cursor valid.
			Nav.ClampToTree();
			if (ItemCount > 0)
				AnnounceCurrent();
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

		private bool CategoryHasFilteredItems(string categoryId) {
			var subIds = InventoryHelper.GetSubcategoryIds(categoryId);
			if (subIds == null) return false;
			foreach (string subId in subIds) {
				if (InventoryHelper.GetFilteredPermitCount(subId, _ownershipFilter, _dlcFilter) > 0)
					return true;
			}
			return false;
		}

		// ========================================
		// PERMIT NAVIGATION
		// ========================================

		/// <summary>
		/// Navigate to a specific permit in the filtered hierarchy.
		/// Used when returning from the detail tab.
		/// </summary>
		private bool NavigateToPermit(PermitResource permit) {
			int cf = 0;
			for (int c = 0; c < InventoryHelper.CategoryCount; c++) {
				string catId = InventoryHelper.GetCategoryId(c);
				if (!CategoryHasFilteredItems(catId)) continue;
				var subIds = InventoryHelper.GetSubcategoryIds(catId);
				if (subIds != null) {
					int sf = 0;
					foreach (string subId in subIds) {
						int count = InventoryHelper.GetFilteredPermitCount(
							subId, _ownershipFilter, _dlcFilter);
						if (count == 0) continue;
						for (int p = 0; p < count; p++) {
							if (InventoryHelper.GetFilteredPermitAt(
									subId, p, _ownershipFilter, _dlcFilter) == permit) {
								Nav.SetPath(new[] { cf, sf, p });
								_search.Clear();
								SuppressSearchThisFrame();
								return true;
							}
						}
						sf++;
					}
				}
				cf++;
			}
			return false;
		}
	}
}
