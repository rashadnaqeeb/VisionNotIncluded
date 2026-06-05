using System.Collections.Generic;

using Database;
using HarmonyLib;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Handler for OutfitDesignerScreen (create/edit outfits in the Supply Closet).
	/// Two-level tree:
	///   Level 0 = slot categories (Helmet, Body, Gloves, etc.) + Save/Copy buttons
	///   Level 1 = items available for the selected slot
	///
	/// Enter at level 1 selects the item into the slot. A "None" entry at index 0
	/// clears the slot. Save/Copy at level 0 are leaf actions. Type-ahead searches
	/// the clothing items by name, grouped by slot; the None and command rows are
	/// excluded.
	///
	/// OutfitDesignerScreen extends KMonoBehaviour (not KScreen), so this
	/// handler bypasses ContextDetector. Harmony patches on OnCmpEnable/
	/// OnCmpDisable push and pop it directly on the HandlerStack.
	/// </summary>
	public class OutfitDesignerHandler: NavTreeHandler {
		private readonly OutfitDesignerScreen _designerScreen;

		public OutfitDesignerHandler(OutfitDesignerScreen screen) : base(screen: null) {
			_designerScreen = screen;
			// Search the clothing items (by name), not the None or command rows.
			Nav.SearchFilter = n => n.RoleKey != NavRoles.Button;
		}

		internal OutfitDesignerScreen DesignerScreen => _designerScreen;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.OUTFIT_DESIGNER;

		public override bool CapturesAllInput => true;

		// Keep clothing-item search results grouped by slot.
		protected override bool GroupSearchByRoot => true;

		public override IReadOnlyList<HelpEntry> HelpEntries => DrillNavHelpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			AnnounceCurrent(interrupt: false);
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var categories = GetCategories();
			var roots = new List<NavItem>(categories.Length + 2);
			for (int s = 0; s < categories.Length; s++) {
				int slot = s;
				roots.Add(new MenuNode(
					() => SlotLabel(slot),
					children: () => BuildSlotItems(slot),
					contextLabel: () => PermitCategories.GetDisplayName(categories[slot])));
			}
			int actions = GetActionCount();
			for (int a = 0; a < actions; a++) {
				int action = a;
				roots.Add(new MenuNode(
					() => GetActionLabel(action),
					activate: () => { ActivateAction(action); return true; },
					roleKey: NavRoles.Button));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildSlotItems(int slotIndex) {
			var categories = GetCategories();
			if (slotIndex < 0 || slotIndex >= categories.Length)
				return System.Array.Empty<NavItem>();
			var category = categories[slotIndex];

			var list = new List<NavItem>();
			// "None" clears the slot — a command, not a searchable item.
			list.Add(new MenuNode(
				() => KleiItemsUI.GetNoneClothingItemStrings(category).name,
				activate: () => { ClearSlot(category); return true; },
				roleKey: NavRoles.Button));

			foreach (var item in GetItemsForSlot(slotIndex)) {
				var it = item;
				list.Add(new MenuNode(
					() => ItemLabel(category, it),
					activate: () => { SelectItem(category, it); return true; },
					searchText: () => it.Name));
			}
			return list;
		}

		private string SlotLabel(int slotIndex) {
			var categories = GetCategories();
			var category = categories[slotIndex];
			string slotName = PermitCategories.GetDisplayName(category);
			var currentItem = _designerScreen.outfitState.GetItemForCategory(category);
			if (currentItem.HasValue)
				return slotName + ": " + currentItem.Unwrap().Name;
			return slotName + ": " + KleiItemsUI.GetNoneClothingItemStrings(category).name;
		}

		private string ItemLabel(PermitCategory category, ClothingItemResource item) {
			string label = OutfitHelper.GetItemLabel(item);
			var current = _designerScreen.outfitState.GetItemForCategory(category);
			if (current.HasValue && current.Unwrap().Id == item.Id)
				label += ", " + (string)STRINGS.ONIACCESS.OUTFIT_DESIGNER.SELECTED;
			return label;
		}

		private void ClearSlot(PermitCategory category) {
			_designerScreen.outfitState.SetItemForCategory(category, Option.None);
			_designerScreen.SelectCategory(category);
			_designerScreen.SelectPermit(null);
			PlaySound("HUD_Click");
			AnnounceCurrent();
		}

		private void SelectItem(PermitCategory category, ClothingItemResource item) {
			_designerScreen.SelectCategory(category);
			_designerScreen.SelectPermit(item);
			PlaySound("HUD_Click");
			AnnounceCurrent();
		}

		// ========================================
		// ESCAPE
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			if (!e.TryConsume(Action.Escape)) return false;

			// Dismiss via LockerNavigator — dirty-state guard is handled
			// by the game's preventScreenPop mechanism
			LockerNavigator.Instance?.PopScreen();
			return true;
		}

		// ========================================
		// ACTIONS (Save, Copy)
		// ========================================

		private string GetActionLabel(int actionIndex) {
			if (_designerScreen.Config.targetMinionInstance.HasValue) {
				if (actionIndex == 0) {
					string minionName = _designerScreen.Config.targetMinionInstance.Value.GetProperName();
					return ((string)STRINGS.UI.OUTFIT_DESIGNER_SCREEN.MINION_INSTANCE.BUTTON_APPLY_TO_MINION)
						.Replace("{MinionName}", minionName);
				}
				if (actionIndex == 1)
					return (string)STRINGS.UI.OUTFIT_DESIGNER_SCREEN.MINION_INSTANCE.BUTTON_APPLY_TO_TEMPLATE;
			} else {
				if (actionIndex == 0)
					return (string)STRINGS.UI.OUTFIT_DESIGNER_SCREEN.OUTFIT_TEMPLATE.BUTTON_SAVE;
				if (actionIndex == 1)
					return (string)STRINGS.UI.OUTFIT_DESIGNER_SCREEN.OUTFIT_TEMPLATE.BUTTON_COPY;
			}
			return null;
		}

		private void ActivateAction(int actionIndex) {
			var fieldName = actionIndex == 0 ? "primaryButton" : "secondaryButton";
			var button = Traverse.Create(_designerScreen)
				.Field<KButton>(fieldName).Value;
			if (button == null) return;

			if (!button.isInteractable) {
				PlaySound("Negative");
				var tooltip = button.gameObject.GetComponent<ToolTip>();
				if (tooltip != null) {
					string reason = Widgets.WidgetOps.ReadAllTooltipText(tooltip);
					if (!string.IsNullOrEmpty(reason)) {
						SpeechPipeline.SpeakInterrupt(reason);
						return;
					}
				}
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE);
				return;
			}

			button.SignalClick(KKeyCode.None);
			PlaySound("HUD_Click");
		}

		// ========================================
		// DATA
		// ========================================

		private PermitCategory[] GetCategories() {
			return OutfitHelper.GetSlotCategories(_designerScreen.outfitState.outfitType);
		}

		private List<ClothingItemResource> GetItemsForSlot(int categoryIndex) {
			var categories = GetCategories();
			if (categoryIndex < 0 || categoryIndex >= categories.Length)
				return new List<ClothingItemResource>();
			return OutfitHelper.GetItemsForCategory(
				categories[categoryIndex], _designerScreen.outfitState.outfitType);
		}

		private int GetActionCount() {
			int count = 0;
			var primary = Traverse.Create(_designerScreen)
				.Field<KButton>("primaryButton").Value;
			var secondary = Traverse.Create(_designerScreen)
				.Field<KButton>("secondaryButton").Value;
			if (primary != null && primary.gameObject.activeInHierarchy) count++;
			if (secondary != null && secondary.gameObject.activeInHierarchy) count++;
			return count;
		}
	}
}
