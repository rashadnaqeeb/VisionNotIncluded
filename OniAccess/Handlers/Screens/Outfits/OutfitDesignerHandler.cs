using System.Collections.Generic;

using Database;
using HarmonyLib;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Handler for OutfitDesignerScreen (create/edit outfits in the Supply Closet).
	/// Two-level NestedMenuHandler:
	///   Level 0 = slot categories (Helmet, Body, Gloves, etc.) + Save/Copy buttons
	///   Level 1 = items available for the selected slot
	///
	/// Enter at level 1 selects the item into the slot. A "None" entry at
	/// index 0 clears the slot. Save/Copy at level 0 are leaf actions.
	///
	/// OutfitDesignerScreen extends KMonoBehaviour (not KScreen), so this
	/// handler bypasses ContextDetector. Harmony patches on OnCmpEnable/
	/// OnCmpDisable push and pop it directly on the HandlerStack.
	/// </summary>
	public class OutfitDesignerHandler : NestedMenuHandler {
		private readonly OutfitDesignerScreen _designerScreen;

		public OutfitDesignerHandler(OutfitDesignerScreen screen) : base(screen: null) {
			_designerScreen = screen;
			_search.GroupOf = GetSearchGroup;
		}

		internal OutfitDesignerScreen DesignerScreen => _designerScreen;

		public override string DisplayName =>
			(string)STRINGS.ONIACCESS.HANDLERS.OUTFIT_DESIGNER;

		public override bool CapturesAllInput => true;

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Ctrl+Up/Down", STRINGS.ONIACCESS.HELP.JUMP_GROUP),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter/Right", STRINGS.ONIACCESS.HELP.OPEN_GROUP),
			new HelpEntry("Left", STRINGS.ONIACCESS.HELP.GO_BACK),
		};

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();

			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.ONIACCESS.HANDLERS.OUTFIT_DESIGNER);

			if (ItemCount > 0) {
				string label = GetItemLabel(0, new int[8]);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(label);
			}
		}

		// ========================================
		// NestedMenuHandler abstracts
		// ========================================

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 1;

		protected override int GetItemCount(int level, int[] indices) {
			var categories = GetCategories();
			if (level == 0)
				return categories.Length + GetActionCount();

			int catIndex = indices[0];
			if (catIndex >= categories.Length) return 0;
			return GetItemsForSlot(catIndex).Count + 1; // +1 for "None" at index 0
		}

		protected override string GetItemLabel(int level, int[] indices) {
			var categories = GetCategories();
			if (level == 0) {
				int idx = indices[0];
				if (idx < categories.Length) {
					string slotName = PermitCategories.GetDisplayName(categories[idx]);
					var currentItem = _designerScreen.outfitState
						.GetItemForCategory(categories[idx]);
					if (currentItem.HasValue)
						return slotName + ": " + currentItem.Unwrap().Name;
					return slotName + ": " + KleiItemsUI.GetNoneClothingItemStrings(categories[idx]).name;
				}
				return GetActionLabel(idx - categories.Length);
			}

			int slotIndex = indices[0];
			if (slotIndex >= categories.Length) return null;

			int itemIndex = indices[1];
			if (itemIndex == 0) {
				var noneStrings = KleiItemsUI.GetNoneClothingItemStrings(categories[slotIndex]);
				return noneStrings.name;
			}

			var items = GetItemsForSlot(slotIndex);
			int realIndex = itemIndex - 1;
			if (realIndex < 0 || realIndex >= items.Count) return null;

			var item = items[realIndex];
			string label = OutfitHelper.GetItemLabel(item);

			// Mark the currently selected item
			var current = _designerScreen.outfitState.GetItemForCategory(categories[slotIndex]);
			if (current.HasValue && current.Unwrap().Id == item.Id)
				label += ", " + (string)STRINGS.ONIACCESS.OUTFIT_DESIGNER.SELECTED;

			return label;
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level <= 0) return null;
			var categories = GetCategories();
			int idx = indices[0];
			if (idx < categories.Length)
				return PermitCategories.GetDisplayName(categories[idx]);
			return null;
		}

		protected override bool ShouldDrillOnActivate() {
			if (Level == 0) {
				int idx = GetIndex(0);
				return idx < GetCategories().Length;
			}
			return false;
		}

		protected override void ActivateLeafItem(int[] indices) {
			var categories = GetCategories();
			if (Level == 0) {
				int actionIdx = indices[0] - categories.Length;
				ActivateAction(actionIdx);
				return;
			}

			int slotIndex = indices[0];
			if (slotIndex >= categories.Length) return;

			var category = categories[slotIndex];
			int itemIndex = indices[1];

			if (itemIndex == 0) {
				_designerScreen.outfitState.SetItemForCategory(category, Option.None);
				_designerScreen.SelectCategory(category);
				_designerScreen.SelectPermit(null);
				PlaySound("HUD_Click");
				SpeakCurrentItem();
				return;
			}

			var items = GetItemsForSlot(slotIndex);
			int realIndex = itemIndex - 1;
			if (realIndex < 0 || realIndex >= items.Count) return;

			var item = items[realIndex];
			_designerScreen.SelectCategory(category);
			_designerScreen.SelectPermit(item);
			PlaySound("HUD_Click");
			SpeakCurrentItem();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override int GetSearchItemCount(int[] indices) {
			var categories = GetCategories();
			int count = 0;
			for (int i = 0; i < categories.Length; i++)
				count += GetItemsForSlot(i).Count;
			return count;
		}

		protected override string GetSearchItemLabel(int flatIndex) {
			var categories = GetCategories();
			int remaining = flatIndex;
			for (int i = 0; i < categories.Length; i++) {
				var items = GetItemsForSlot(i);
				if (remaining < items.Count)
					return items[remaining].Name;
				remaining -= items.Count;
			}
			return null;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			var categories = GetCategories();
			int remaining = flatIndex;
			for (int i = 0; i < categories.Length; i++) {
				var items = GetItemsForSlot(i);
				if (remaining < items.Count) {
					outIndices[0] = i;
					outIndices[1] = remaining + 1; // +1 because index 0 is "None"
					return;
				}
				remaining -= items.Count;
			}
		}

		private int GetSearchGroup(int flatIndex) {
			var categories = GetCategories();
			int remaining = flatIndex;
			for (int i = 0; i < categories.Length; i++) {
				var items = GetItemsForSlot(i);
				if (remaining < items.Count) return i;
				remaining -= items.Count;
			}
			return 0;
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
			if (button != null && button.isInteractable) {
				button.SignalClick(KKeyCode.None);
				PlaySound("HUD_Click");
			}
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
