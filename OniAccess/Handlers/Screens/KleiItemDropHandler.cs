using System.Collections.Generic;
using Database;
using HarmonyLib;
using OniAccess.Util;

using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for KleiItemDropScreen: cosmetic item reveal triggered from Supply Closet.
	///
	/// Uses Harmony postfix patches on PresentItem, OnOpenItemRequestResponse, and
	/// PresentNoItemAvailablePrompt to detect state changes, replacing the old
	/// polling approach. Item data is resolved directly from the permit database
	/// rather than waiting for coroutine-driven label population.
	///
	/// Lifecycle note: Like LockerMenuScreen, OnActivate() calls Show(false) during prefab
	/// init, so a Harmony patch on KleiItemDropScreen.Show pushes/pops this handler.
	///
	/// Timing: PresentItem and PresentNoItemAvailablePrompt are called inside Show()
	/// (via OnShow → PresentNextUnopenedItem) before the Show postfix pushes this
	/// handler. Static pending fields capture that data for OnActivate to consume.
	/// Subsequent calls (item 2+, exhaustion) happen while the handler is active
	/// and go through the normal patch → handler path.
	/// </summary>
	public class KleiItemDropHandler: BaseWidgetHandler {
		public override string DisplayName => (string)STRINGS.ONIACCESS.HANDLERS.ITEM_DROP;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		/// Coroutine animations take ~2 seconds before buttons appear.
		protected override int MaxDiscoveryRetries => 300;

		enum Stage { Initial, WaitingForAccept, WaitingForServer, ItemRevealed, NoItemAvailable }

		private Stage _stage;
		private KleiItems.ItemData _currentItem;

		// Live Unity component refs resolved once in OnActivate via Traverse.
		private KButton _acceptButton;
		private KButton _acknowledgeButton;
		private KButton _closeButton;
		private LocText _unopenedItemCountLabel;
		private LocText _userMessageLabel;
		private LocText _errorMessage;

		// Static pending fields: captures data from patches that fire during Show()
		// before the handler is pushed. Consumed in OnActivate.
		internal static KleiItems.ItemData PendingItem;
		internal static bool HasPendingItem;
		internal static bool HasPendingNoItem;

		public KleiItemDropHandler(KScreen screen) : base(screen) {
			HelpEntries = BuildHelpEntries();
		}

		public override void OnActivate() {
			_stage = Stage.Initial;

			var t = Traverse.Create(_screen);
			_acceptButton = t.Field<KButton>("acceptButton").Value;
			_acknowledgeButton = t.Field<KButton>("acknowledgeButton").Value;
			_closeButton = t.Field<KButton>("closeButton").Value;
			_unopenedItemCountLabel = t.Field<LocText>("unopenedItemCountLabel").Value;
			_userMessageLabel = t.Field<LocText>("userMessageLabel").Value;
			_errorMessage = t.Field<LocText>("errorMessage").Value;

			// Consume data from patches that fired during Show() before this handler
			// was pushed. Set stage before base.OnActivate() so DiscoverWidgets uses it.
			if (HasPendingItem) {
				HasPendingItem = false;
				_currentItem = PendingItem;
				_stage = Stage.WaitingForAccept;

				// Speak the item count; the accept button itself will be announced by
				// deferred rediscovery when the coroutine animation makes it visible.
				// Read text directly — the label may not be activeInHierarchy yet if a
				// parent is still hidden by the coroutine, but SetText has already run.
				string countText = _unopenedItemCountLabel != null
					? _unopenedItemCountLabel.text : null;
				if (!string.IsNullOrEmpty(countText)) {
					Speech.SpeechPipeline.SpeakInterrupt(countText);
				}
			} else if (HasPendingNoItem) {
				HasPendingNoItem = false;
				_stage = Stage.NoItemAvailable;

				if (_userMessageLabel != null) {
					string text = _userMessageLabel.text;
					if (!string.IsNullOrEmpty(text)) {
						Speech.SpeechPipeline.SpeakInterrupt(text);
					}
				}
			}

			base.OnActivate();
		}

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			if (_stage == Stage.WaitingForAccept || _stage == Stage.NoItemAvailable) {
				if (_acceptButton != null && _acceptButton.gameObject.activeInHierarchy) {
					string label = GetButtonLabel(_acceptButton, (string)STRINGS.ONIACCESS.BUTTONS.ACCEPT);
					_widgets.Add(new ButtonWidget {
						Label = label,
						Component = _acceptButton,
						GameObject = _acceptButton.gameObject
					});
				}
			}

			if (_stage == Stage.ItemRevealed) {
				if (_acknowledgeButton != null && _acknowledgeButton.gameObject.activeInHierarchy) {
					string label = GetButtonLabel(_acknowledgeButton, (string)STRINGS.UI.CONFIRMDIALOG.OK);
					_widgets.Add(new ButtonWidget {
						Label = label,
						Component = _acknowledgeButton,
						GameObject = _acknowledgeButton.gameObject
					});
				}
			}

			if (_closeButton != null && _closeButton.gameObject.activeInHierarchy) {
				var locText = _closeButton.GetComponentInChildren<LocText>();
				string label = locText != null && !string.IsNullOrEmpty(locText.text)
					? locText.text : null;
				// Skip if no label — the button is an unlabeled X; Escape closes the screen.
				if (label != null) {
					_widgets.Add(new ButtonWidget {
						Label = label,
						Component = _closeButton,
						GameObject = _closeButton.gameObject
					});
				}
			}

			return true;
		}

		public override bool Tick() {
			DiscoverWidgets(_screen);

			if (_widgets.Count > 0 && CurrentIndex >= _widgets.Count) {
				CurrentIndex = _widgets.Count - 1;
			}

			return base.Tick();
		}

		/// <summary>
		/// Called from Harmony postfix on KleiItemDropScreen.PresentItem.
		/// Only fires for items 2+ (the first fires during Show() before the handler
		/// is pushed, so it goes through the pending field path instead).
		/// </summary>
		internal void OnItemPresented(KleiItems.ItemData item, bool firstItemPresentation) {
			_currentItem = item;
			_stage = Stage.WaitingForAccept;
			// Non-first items: game calls RequestReveal immediately, so OnRevealResponse
			// will fire shortly. No accept button is shown.
		}

		/// <summary>
		/// Called from Harmony postfix on KleiItemDropScreen.OnOpenItemRequestResponse.
		/// The handler is always active by the time server responses arrive.
		/// </summary>
		internal void OnRevealResponse(bool success) {
			if (success) {
				_stage = Stage.ItemRevealed;
				string announcement = BuildItemAnnouncement(_currentItem);
				if (announcement != null) {
					Speech.SpeechPipeline.SpeakInterrupt(announcement);
				}
			} else {
				_stage = Stage.WaitingForAccept;
				if (_errorMessage != null && _errorMessage.gameObject.activeSelf) {
					string errorText = _errorMessage.text;
					if (!string.IsNullOrEmpty(errorText)) {
						Speech.SpeechPipeline.SpeakInterrupt(errorText);
					}
				}
			}
		}

		/// <summary>
		/// Called from Harmony postfix on KleiItemDropScreen.PresentNoItemAvailablePrompt.
		/// Only fires after items are exhausted (the initial no-items case fires during
		/// Show() and goes through the pending field path).
		/// </summary>
		internal void OnNoItemAvailable() {
			_stage = Stage.NoItemAvailable;
			if (_userMessageLabel != null) {
				string text = _userMessageLabel.text;
				if (!string.IsNullOrEmpty(text)) {
					Speech.SpeechPipeline.SpeakInterrupt(text);
				}
			}
		}

		/// <summary>
		/// Resolve item info from the permit database, matching the game's own
		/// resolution logic in PresentItemRoutine.
		/// </summary>
		private static string BuildItemAnnouncement(KleiItems.ItemData item) {
			try {
				var parts = new List<string>();

				if (PermitItems.TryGetBoxInfo(item, out string name, out string desc, out _)) {
					// Mystery box: rarity is always Loyalty
					parts.Add(PermitRarity.Loyalty.GetLocStringName());
					parts.Add(name);
					if (!string.IsNullOrEmpty(desc)) parts.Add(desc);
				} else {
					PermitResource permit = Db.Get().Permits.Get(item.Id);
					parts.Add(permit.Rarity.GetLocStringName());

					string category;
					switch (permit.Category) {
						case PermitCategory.Building:
							category = Assets.GetPrefab(new Tag((permit as BuildingFacadeResource).PrefabID)).GetProperName();
							break;
						case PermitCategory.JoyResponse:
							category = PermitCategories.GetDisplayName(permit.Category);
							if (permit is BalloonArtistFacadeResource) {
								category = category + ": " + (string)STRINGS.UI.KLEI_INVENTORY_SCREEN.CATEGORIES.JOY_RESPONSES.BALLOON_ARTIST;
							}
							break;
						case PermitCategory.Artwork:
							category = PermitCategories.GetDisplayName(permit.Category);
							if (permit is ArtableStage artStage) {
								category = Assets.GetPrefab(new Tag(artStage.prefabId)).GetProperName();
							}
							break;
						default:
							category = PermitCategories.GetDisplayName(permit.Category);
							break;
					}
					if (!string.IsNullOrEmpty(category)) parts.Add(category);

					parts.Add(permit.Name);
					if (!string.IsNullOrEmpty(permit.Description)) parts.Add(permit.Description);
				}

				return string.Join(", ", parts.ToArray());
			} catch (System.Exception ex) {
				Log.Error($"KleiItemDropHandler.BuildItemAnnouncement failed for item {item.Id}: {ex}");
				return null;
			}
		}
	}
}
