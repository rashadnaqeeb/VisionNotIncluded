using System.Collections.Generic;

using Database;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Data access layer for the inventory/blueprint gallery.
	/// Reads from InventoryOrganization (categories, subcategories, permit IDs)
	/// and Db.Get().Permits (permit metadata). All data is re-queried on each
	/// call — no caching of game state.
	/// </summary>
	internal static class InventoryHelper {
		// ========================================
		// CATEGORIES
		// ========================================

		private static readonly string[] CategoryOrder = {
			"CLOTHING_TOPS", "CLOTHING_BOTTOMS", "CLOTHING_GLOVES", "CLOTHING_SHOES",
			"ATMOSUITS", "JETSUITS", "BUILDINGS", "WALLPAPERS", "ARTWORK", "JOY_RESPONSES"
		};

		internal static int CategoryCount => CategoryOrder.Length;

		internal static string GetCategoryId(int index) {
			if (index < 0 || index >= CategoryOrder.Length) return null;
			return CategoryOrder[index];
		}

		internal static string GetCategoryName(int index) {
			string id = GetCategoryId(index);
			if (id == null) return null;
			return InventoryOrganization.GetCategoryName(id);
		}

		// ========================================
		// SUBCATEGORIES
		// ========================================

		internal static List<string> GetSubcategoryIds(string categoryId) {
			if (categoryId == null) return null;
			if (!InventoryOrganization.categoryIdToSubcategoryIdsMap.TryGetValue(categoryId, out var ids))
				return null;
			return ids;
		}

		internal static int GetSubcategoryCount(string categoryId) {
			var ids = GetSubcategoryIds(categoryId);
			return ids?.Count ?? 0;
		}

		internal static string GetSubcategoryId(string categoryId, int subIndex) {
			var ids = GetSubcategoryIds(categoryId);
			if (ids == null || subIndex < 0 || subIndex >= ids.Count) return null;
			return ids[subIndex];
		}

		internal static string GetSubcategoryName(string subcategoryId) {
			if (subcategoryId == null) return null;
			return InventoryOrganization.GetSubcategoryName(subcategoryId);
		}

		// ========================================
		// PERMITS (items within subcategories)
		// ========================================

		internal static List<string> GetPermitIds(string subcategoryId) {
			if (subcategoryId == null) return null;
			if (!InventoryOrganization.subcategoryIdToPermitIdsMap.TryGetValue(subcategoryId, out var ids))
				return null;
			return ids;
		}

		internal static int GetPermitCount(string subcategoryId) {
			var ids = GetPermitIds(subcategoryId);
			return ids?.Count ?? 0;
		}

		internal static PermitResource GetPermit(string permitId) {
			if (permitId == null) return null;
			return Db.Get().Permits.TryGet(permitId);
		}

		internal static PermitResource GetPermitAt(string subcategoryId, int itemIndex) {
			var ids = GetPermitIds(subcategoryId);
			if (ids == null || itemIndex < 0 || itemIndex >= ids.Count) return null;
			return GetPermit(ids[itemIndex]);
		}

		// ========================================
		// ITEM LABEL
		// ========================================

		internal static string GetPermitLabel(PermitResource permit) {
			if (permit == null) return null;
			string name = permit.Name;
			string rarity = permit.Rarity.GetLocStringName();
			int owned = permit.IsOwnableOnServer() ? PermitItems.GetOwnedCount(permit) : -1;

			if (owned > 0)
				return name + ", " + rarity + ", " + string.Format(
					(string)STRINGS.ONIACCESS.INVENTORY.OWNED, owned);
			if (owned == 0)
				return name + ", " + rarity + ", " +
					(string)STRINGS.ONIACCESS.INVENTORY.UNOWNED;
			// Not ownable (Universal, etc.)
			return name + ", " + rarity;
		}

		internal static string GetPermitLabelAt(string subcategoryId, int itemIndex) {
			return GetPermitLabel(GetPermitAt(subcategoryId, itemIndex));
		}

		// ========================================
		// FILTERING
		// ========================================

		/// <summary>
		/// Check if a permit passes the current filter criteria.
		/// filterState: 0=all, 1=owned only, 2=doubles only.
		/// dlcId: null=all, otherwise specific DLC.
		/// </summary>
		internal static bool PassesFilter(PermitResource permit, int filterState, string dlcId) {
			if (permit == null) return false;

			if (filterState > 0) {
				int count = PermitItems.GetOwnedCount(permit);
				if (filterState == 1 && count < 1) return false;
				if (filterState == 2 && count < 2) return false;
			}

			if (dlcId != null && permit.GetDlcIdFrom() != dlcId)
				return false;

			return true;
		}

		/// <summary>
		/// Count permits in a subcategory that pass the filter.
		/// </summary>
		internal static int GetFilteredPermitCount(string subcategoryId, int filterState, string dlcId) {
			var ids = GetPermitIds(subcategoryId);
			if (ids == null) return 0;
			int count = 0;
			foreach (string id in ids) {
				var permit = GetPermit(id);
				if (PassesFilter(permit, filterState, dlcId))
					count++;
			}
			return count;
		}

		/// <summary>
		/// Get the nth permit (by filtered index) in a subcategory.
		/// </summary>
		internal static PermitResource GetFilteredPermitAt(
			string subcategoryId, int filteredIndex, int filterState, string dlcId
		) {
			var ids = GetPermitIds(subcategoryId);
			if (ids == null) return null;
			int seen = 0;
			foreach (string id in ids) {
				var permit = GetPermit(id);
				if (PassesFilter(permit, filterState, dlcId)) {
					if (seen == filteredIndex) return permit;
					seen++;
				}
			}
			return null;
		}

		// ========================================
		// DLC LIST
		// ========================================

		internal static List<string> GetActiveDlcIds() {
			return DlcManager.GetActiveDLCIds();
		}

		internal static string GetDlcDisplayName(string dlcId) {
			if (dlcId == null) return (string)STRINGS.ONIACCESS.INVENTORY.FILTER_DLC_ALL;
			return DlcManager.GetDlcTitleNoFormatting(dlcId);
		}

		// ========================================
		// BARTER
		// ========================================

		internal static bool IsOnline() {
			return ThreadedHttps<KleiAccount>.Instance.HasValidTicket();
		}

		internal static string GetBuyLabel(PermitResource permit) {
			if (!IsOnline()) return (string)STRINGS.ONIACCESS.SUPPLY_CLOSET.OFFLINE;
			if (!PermitItems.TryGetBarterPrice(permit.Id, out ulong buyPrice, out _))
				return (string)STRINGS.ONIACCESS.INVENTORY.NOT_FOR_SALE;

			if (buyPrice == 0) {
				if (permit.Rarity == PermitRarity.Universal ||
					permit.Rarity == PermitRarity.UniversalLocked ||
					permit.Rarity == PermitRarity.Loyalty ||
					permit.Rarity == PermitRarity.Unknown)
					return (string)STRINGS.ONIACCESS.INVENTORY.NOT_FOR_SALE;
				return (string)STRINGS.ONIACCESS.INVENTORY.NOT_FOR_SALE_YET;
			}

			if (PermitItems.GetOwnedCount(permit) > 0)
				return (string)STRINGS.ONIACCESS.INVENTORY.ALREADY_OWNED;

			if (KleiItems.GetFilamentAmount() < buyPrice)
				return string.Format((string)STRINGS.ONIACCESS.INVENTORY.TOO_EXPENSIVE, buyPrice);

			return string.Format((string)STRINGS.ONIACCESS.INVENTORY.BUY, buyPrice);
		}

		internal static string GetSellLabel(PermitResource permit) {
			if (!IsOnline()) return (string)STRINGS.ONIACCESS.SUPPLY_CLOSET.OFFLINE;
			if (!PermitItems.TryGetBarterPrice(permit.Id, out _, out ulong sellPrice) || sellPrice == 0)
				return (string)STRINGS.ONIACCESS.INVENTORY.NOT_FOR_SALE;

			if (PermitItems.GetOwnedCount(permit) <= 0)
				return (string)STRINGS.ONIACCESS.INVENTORY.SELL_NONE;

			return string.Format((string)STRINGS.ONIACCESS.INVENTORY.SELL, sellPrice);
		}

		internal static bool CanBuy(PermitResource permit) {
			if (!IsOnline()) return false;
			if (!PermitItems.TryGetBarterPrice(permit.Id, out ulong buyPrice, out _))
				return false;
			if (buyPrice == 0) return false;
			if (PermitItems.GetOwnedCount(permit) > 0) return false;
			return KleiItems.GetFilamentAmount() >= buyPrice;
		}

		internal static bool CanSell(PermitResource permit) {
			if (!IsOnline()) return false;
			if (!PermitItems.TryGetBarterPrice(permit.Id, out _, out ulong sellPrice))
				return false;
			if (sellPrice == 0) return false;
			return PermitItems.GetOwnedCount(permit) > 0;
		}
	}
}
