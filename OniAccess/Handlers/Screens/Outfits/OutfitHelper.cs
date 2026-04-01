using System.Collections.Generic;
using System.Linq;

using Database;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Data access layer for the wardrobe and outfit designer screens.
	/// Reads from ClothingOutfitTarget, Db.Get().Permits, and PermitCategories.
	/// All data is re-queried on each call — no caching of game state.
	/// </summary>
	internal static class OutfitHelper {
		// ========================================
		// OUTFIT TYPE LABELS
		// ========================================

		private static readonly ClothingOutfitUtility.OutfitType[] OutfitTypes = {
			ClothingOutfitUtility.OutfitType.Clothing,
			ClothingOutfitUtility.OutfitType.AtmoSuit,
			ClothingOutfitUtility.OutfitType.JetSuit
		};

		internal static int OutfitTypeCount => OutfitTypes.Length;

		internal static ClothingOutfitUtility.OutfitType GetOutfitType(int index) {
			return OutfitTypes[index % OutfitTypes.Length];
		}

		internal static int IndexOfOutfitType(ClothingOutfitUtility.OutfitType type) {
			for (int i = 0; i < OutfitTypes.Length; i++) {
				if (OutfitTypes[i] == type) return i;
			}
			return 0;
		}

		internal static string GetOutfitTypeLabel(ClothingOutfitUtility.OutfitType type) {
			string typeName;
			switch (type) {
				case ClothingOutfitUtility.OutfitType.Clothing:
					typeName = (string)STRINGS.ONIACCESS.WARDROBE.TYPE_CLOTHING;
					break;
				case ClothingOutfitUtility.OutfitType.AtmoSuit:
					typeName = (string)STRINGS.ONIACCESS.WARDROBE.TYPE_ATMO_SUIT;
					break;
				case ClothingOutfitUtility.OutfitType.JetSuit:
					typeName = (string)STRINGS.ONIACCESS.WARDROBE.TYPE_JET_SUIT;
					break;
				default:
					typeName = type.ToString();
					break;
			}
			return string.Format((string)STRINGS.ONIACCESS.WARDROBE.OUTFIT_TYPE, typeName);
		}

		// ========================================
		// OUTFIT LIST
		// ========================================

		/// <summary>
		/// Get all template outfits of the given type, in the same order
		/// the game populates them (database-authored first, then user-authored).
		/// </summary>
		internal static List<ClothingOutfitTarget> GetOutfitsForType(
			ClothingOutfitUtility.OutfitType outfitType
		) {
			return ClothingOutfitTarget.GetAllTemplates()
				.Where(o => o.OutfitType == outfitType)
				.ToList();
		}

		// ========================================
		// OUTFIT LABELS
		// ========================================

		internal static string GetOutfitLabel(ClothingOutfitTarget outfit) {
			string name = outfit.ReadName();
			if (outfit.DoesContainLockedItems())
				return name + ", " + (string)STRINGS.ONIACCESS.WARDROBE.CONTAINS_LOCKED;
			return name;
		}

		// ========================================
		// SLOT COMPOSITION
		// ========================================

		/// <summary>
		/// Get the slot categories for an outfit type.
		/// </summary>
		internal static PermitCategory[] GetSlotCategories(ClothingOutfitUtility.OutfitType outfitType) {
			if (OutfitDesignerScreen.outfitTypeToCategoriesDict != null
				&& OutfitDesignerScreen.outfitTypeToCategoriesDict.TryGetValue(outfitType, out var categories))
				return categories;

			// Fallback if dict not yet initialized
			switch (outfitType) {
				case ClothingOutfitUtility.OutfitType.Clothing:
					return ClothingOutfitUtility.PERMIT_CATEGORIES_FOR_CLOTHING;
				case ClothingOutfitUtility.OutfitType.AtmoSuit:
					return ClothingOutfitUtility.PERMIT_CATEGORIES_FOR_ATMO_SUITS;
				case ClothingOutfitUtility.OutfitType.JetSuit:
					return ClothingOutfitUtility.PERMIT_CATEGORIES_FOR_JET_SUITS;
				default:
					return System.Array.Empty<PermitCategory>();
			}
		}

		/// <summary>
		/// Build a list of "Slot: Item" strings describing an outfit's composition.
		/// </summary>
		internal static List<string> GetOutfitComposition(ClothingOutfitTarget outfit) {
			var result = new List<string>();
			var categories = GetSlotCategories(outfit.OutfitType);
			var items = outfit.ReadItems();
			var itemLookup = new Dictionary<PermitCategory, ClothingItemResource>();

			foreach (string itemId in items) {
				var item = Db.Get().Permits.ClothingItems.TryGet(itemId);
				if (item != null)
					itemLookup[item.Category] = item;
			}

			foreach (var category in categories) {
				string slotName = PermitCategories.GetDisplayName(category);
				if (itemLookup.TryGetValue(category, out var item)) {
					string itemText = item.Name;
					if (!string.IsNullOrEmpty(item.Description)
						&& !item.Description.Equals("n/a", System.StringComparison.OrdinalIgnoreCase))
						itemText += ", " + item.Description;
					result.Add(string.Format(
						(string)STRINGS.ONIACCESS.WARDROBE.SLOT_ITEM,
						slotName, itemText));
				} else {
					string defaultName = KleiItemsUI.GetNoneClothingItemStrings(category).name;
					result.Add(string.Format(
						(string)STRINGS.ONIACCESS.WARDROBE.SLOT_ITEM,
						slotName, defaultName));
				}
			}

			return result;
		}

		// ========================================
		// DESIGNER: ITEMS FOR CATEGORY
		// ========================================

		/// <summary>
		/// Get all clothing items available for a given category and outfit type.
		/// Filters out visonly_ items (same as the game does).
		/// </summary>
		internal static List<ClothingItemResource> GetItemsForCategory(
			PermitCategory category, ClothingOutfitUtility.OutfitType outfitType
		) {
			var result = new List<ClothingItemResource>();
			foreach (var resource in Db.Get().Permits.ClothingItems.resources) {
				if (resource.Category == category
					&& resource.outfitType == outfitType
					&& !resource.Id.StartsWith("visonly_"))
					result.Add(resource);
			}
			return result;
		}

		/// <summary>
		/// Build a label for a clothing item in the designer gallery.
		/// Shows name, rarity, and owned/unowned status.
		/// </summary>
		internal static string GetItemLabel(ClothingItemResource item) {
			if (item == null)
				return KleiItemsUI.GetNoneClothingItemStrings(PermitCategory.DupeTops).name;

			string name = item.Name;
			string rarity = item.Rarity.GetLocStringName();
			string label;
			if (item.IsOwnableOnServer()) {
				int count = PermitItems.GetOwnedCount(item);
				if (count > 0)
					label = name + ", " + rarity;
				else
					label = name + ", " + rarity + ", "
						+ (string)STRINGS.ONIACCESS.INVENTORY.UNOWNED;
			} else {
				label = name + ", " + rarity;
			}

			if (!string.IsNullOrEmpty(item.Description)
				&& !item.Description.Equals("n/a", System.StringComparison.OrdinalIgnoreCase))
				label += ", " + item.Description;

			return label;
		}
	}
}
