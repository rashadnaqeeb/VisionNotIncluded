using System.Collections.Generic;

using Database;
using HarmonyLib;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Inventory {
	/// <summary>
	/// Detail tab: flat BaseMenuHandler that displays permit details and
	/// buy/sell actions. Built from PermitResource data each time a permit
	/// is selected. Enter on buy/sell triggers the game's barter confirmation.
	/// </summary>
	internal class DetailTab: BaseMenuHandler, IScreenTab {
		private readonly InventoryScreenHandler _parent;
		private readonly List<DetailItem> _items = new List<DetailItem>();
		private PermitResource _permit;

		internal DetailTab(InventoryScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.INVENTORY.DETAIL_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => null;

		internal PermitResource CurrentPermit => _permit;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			CurrentIndex = 0;
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			SpeakCurrentItemQueued();
		}

		public void OnTabDeactivated() { }

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// PERMIT LOADING
		// ========================================

		internal void LoadPermit(PermitResource permit) {
			_permit = permit;
			RebuildItems();
			CurrentIndex = 0;
		}

		private void RebuildItems() {
			_items.Clear();
			if (_permit == null) return;

			// Name
			_items.Add(new DetailItem { text = _permit.Name });

			// Description (skip placeholder "n/a")
			if (!string.IsNullOrWhiteSpace(_permit.Description) && _permit.Description != "n/a")
				_items.Add(new DetailItem { text = _permit.Description });

			// Rarity or DLC collection
			string dlcId = _permit.GetDlcIdFrom();
			if (DlcManager.IsDlcId(dlcId))
				_items.Add(new DetailItem { text = DlcManager.GetDlcTitleNoFormatting(dlcId) });
			else
				_items.Add(new DetailItem { text = _permit.Rarity.GetLocStringName() });

			// Facade for
			var info = _permit.GetPermitPresentationInfo();
			if (!string.IsNullOrWhiteSpace(info.facadeFor))
				_items.Add(new DetailItem { text = info.facadeFor });

			// Owned count
			if (_permit.IsOwnableOnServer()) {
				int count = PermitItems.GetOwnedCount(_permit);
				_items.Add(new DetailItem {
					text = string.Format((string)STRINGS.ONIACCESS.INVENTORY.OWNED, count)
				});
			}

			// Filament wallet
			if (InventoryHelper.IsOnline()) {
				ulong filaments = KleiItems.GetFilamentAmount();
				_items.Add(new DetailItem {
					text = string.Format((string)STRINGS.ONIACCESS.INVENTORY.FILAMENTS, filaments)
				});
			}

			// Buy
			_items.Add(new DetailItem {
				text = InventoryHelper.GetBuyLabel(_permit),
				action = InventoryHelper.CanBuy(_permit) ? DetailAction.Buy : DetailAction.None
			});

			// Sell
			_items.Add(new DetailItem {
				text = InventoryHelper.GetSellLabel(_permit),
				action = InventoryHelper.CanSell(_permit) ? DetailAction.Sell : DetailAction.None
			});
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount => _items.Count;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].text;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_items.Count == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (string.IsNullOrEmpty(text)) return;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(text));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			var item = _items[CurrentIndex];

			if (item.action == DetailAction.Buy) {
				TriggerBarter(isPurchase: true);
			} else if (item.action == DetailAction.Sell) {
				TriggerBarter(isPurchase: false);
			}
		}

		// ========================================
		// BARTER
		// ========================================

		private void TriggerBarter(bool isPurchase) {
			if (_permit == null) return;

			var inventoryScreen = _parent.InventoryScreen;
			if (inventoryScreen == null) return;

			var prefab = Traverse.Create(inventoryScreen)
				.Field("barterConfirmationScreenPrefab")
				.GetValue<UnityEngine.GameObject>();
			if (prefab == null) {
				Util.Log.Warn("DetailTab: barterConfirmationScreenPrefab is null");
				return;
			}

			var obj = global::Util.KInstantiateUI(prefab, LockerNavigator.Instance.gameObject);
			obj.rectTransform().sizeDelta = UnityEngine.Vector2.zero;
			obj.GetComponent<BarterConfirmationScreen>().Present(_permit, isPurchase);
			PlaySound("HUD_Click_Open");
		}

		// ========================================
		// HELPERS
		// ========================================

		private void SpeakCurrentItemQueued() {
			if (_items.Count == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			string text = _items[CurrentIndex].text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(text));
		}

		private enum DetailAction { None, Buy, Sell }

		private struct DetailItem {
			internal string text;
			internal DetailAction action;
		}
	}
}
