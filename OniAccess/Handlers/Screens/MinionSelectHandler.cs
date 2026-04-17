using System.Collections.Generic;
using Database;
using HarmonyLib;
using Klei.AI;

using OniAccess.Input;
using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for MinionSelectScreen (initial colony start — full game start screen).
	///
	/// Two-level navigation:
	/// TOP LEVEL (Up/Down): Colony name, Shuffle name, Select duplicants, Embark
	/// DUPE MODE (Up/Down within slot, Tab/Shift+Tab between slots):
	///   name, interests, traits, expectations (stress/joy), attributes, description, interest filter, reroll
	///
	/// Colony name is editable (Enter to edit, Enter to confirm, Escape to cancel).
	/// Shuffle name button clicks and speaks the new name.
	/// Select duplicants enters dupe mode.
	/// Tab/Shift+Tab in dupe mode preserves widget position across slots.
	///
	/// Per Pitfall 4: CharacterContainer inherits KScreen but is NOT pushed to
	/// KScreenManager -- ContextDetector ignores it. Navigation is handled entirely
	/// within this handler.
	///
	/// Per locked decisions:
	/// - Traits: full info upfront (name, effect, description all spoken together)
	/// - Attributes: one per arrow press ("Athletics 3")
	/// - After reroll: speak new name and interests automatically
	/// </summary>
	public class MinionSelectHandler: BaseWidgetHandler {
		private int _currentSlot;
		private UnityEngine.Component[] _containers;
		private System.Action _pendingAnnounce;
		private bool _pendingColonyNameAnnounce;
		private bool _inDupeMode;
		private static readonly System.Type MinionSelectScreenType =
			HarmonyLib.AccessTools.TypeByName("MinionSelectScreen");

		/// <summary>
		/// Whether the screen is MinionSelectScreen (has colony naming).
		/// Printing Pod (ImmigrantScreen) does not have BaseNaming.
		/// </summary>
		private bool IsMinionSelectScreen =>
			_screen != null && _screen.GetType() == MinionSelectScreenType;

		/// <summary>
		/// CharacterContainer uses coroutines (DelayedGeneration, SetAttributes)
		/// that take multiple frames to complete.
		/// </summary>
		protected override int MaxDiscoveryRetries => 10;

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.MINION_SELECT;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public MinionSelectHandler(KScreen screen) : base(screen) {
			_currentSlot = 0;
			_inDupeMode = false;
			HelpEntries = BuildHelpEntries(new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_DUPE_SLOT));
		}

		public override void OnActivate() {
			_inDupeMode = false;
			_pendingColonyNameAnnounce = false;
			_pendingAnnounce = null;
			base.OnActivate();
		}

		// ========================================
		// TAB NAVIGATION (switch between dupe slots)
		// ========================================

		protected override void NavigateTabForward() {
			if (!_inDupeMode) return;
			if (_containers == null || _containers.Length == 0) return;
			_currentSlot = (_currentSlot + 1) % _containers.Length;
			if (_currentSlot == 0) PlaySound("HUD_Click");
			RediscoverAndSpeakSlot();
		}

		protected override void NavigateTabBackward() {
			if (!_inDupeMode) return;
			if (_containers == null || _containers.Length == 0) return;
			int prev = _currentSlot;
			_currentSlot = (_currentSlot - 1 + _containers.Length) % _containers.Length;
			if (_currentSlot == _containers.Length - 1 && prev == 0) PlaySound("HUD_Click");
			RediscoverAndSpeakSlot();
		}

		private void RediscoverAndSpeakSlot() {
			DiscoverWidgets(_screen);
			CurrentIndex = 0;
			if (_widgets.Count > 0) {
				Speech.SpeechPipeline.SpeakInterrupt(
					$"{string.Format(STRINGS.ONIACCESS.INFO.SLOT, _currentSlot + 1)}, {GetWidgetSpeechText(_widgets[0])}");
			}
		}

		// ========================================
		// WIDGET DISCOVERY
		// ========================================

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			if (_inDupeMode) {
				return DiscoverDupeModeWidgets(screen);
			}

			return DiscoverTopLevelWidgets(screen);
		}

		/// <summary>
		/// Discover top-level widgets: colony name, shuffle, select dupes, embark.
		/// </summary>
		private bool DiscoverTopLevelWidgets(KScreen screen) {
			// Colony name (MinionSelectScreen only, via BaseNaming component)
			if (IsMinionSelectScreen) {
				try {
					// BaseNaming is on the same GameObject as MinionSelectScreen
					var baseNamingObj = screen.gameObject.GetComponent(
						HarmonyLib.AccessTools.TypeByName("BaseNaming"));
					if (baseNamingObj != null) {
						var bnt = Traverse.Create(baseNamingObj);
						var inputField = bnt.Field("inputField").GetValue<KInputTextField>();
						if (inputField != null) {
							var colonyField = inputField;
							_widgets.Add(new TextInputWidget {
								Label = $"{STRINGS.ONIACCESS.PANELS.COLONY_NAME}, {inputField.text}",
								Component = inputField,
								GameObject = inputField.gameObject,
								Tag = "colony_name",
								SpeechFunc = () => {
									if (string.IsNullOrEmpty(colonyField.text)) {
										_pendingColonyNameAnnounce = true;
										return STRINGS.ONIACCESS.PANELS.COLONY_NAME;
									}
									return $"{STRINGS.ONIACCESS.PANELS.COLONY_NAME}, {colonyField.text}";
								}
							});

							// Shuffle colony name button
							var shuffleBtn = bnt.Field("shuffleBaseNameButton").GetValue<KButton>();
							if (shuffleBtn != null && shuffleBtn.gameObject.activeInHierarchy) {
								_widgets.Add(new ButtonWidget {
									Label = GetButtonLabel(shuffleBtn, (string)STRINGS.ONIACCESS.PANELS.SHUFFLE_NAME),
									Component = shuffleBtn,
									GameObject = shuffleBtn.gameObject,
									Tag = "colony_shuffle"
								});
							}
						}
					}
				} catch (System.Exception ex) {
					Util.Log.Error($"MinionSelectHandler.DiscoverTopLevelWidgets(BaseNaming): {ex.Message}");
				}
			}

			// "Select duplicants" virtual button (enters dupe mode)
			// Verify containers exist before offering this option
			_containers = screen.GetComponentsInChildren<CharacterContainer>(true);
			if (_containers != null && _containers.Length > 0) {
				_widgets.Add(new ButtonWidget {
					Label = STRINGS.ONIACCESS.HANDLERS.MINION_SELECT,
					Component = null,
					GameObject = screen.gameObject,
					Tag = "enter_dupe_mode"
				});
			}

			// Embark / Proceed button (from CharacterSelectionController)
			try {
				var proceedButton = Traverse.Create(screen).Field("proceedButton")
					.GetValue<KButton>();
				if (proceedButton != null && proceedButton.gameObject.activeInHierarchy) {
					string label = GetButtonLabel(proceedButton, (string)STRINGS.UI.IMMIGRANTSCREEN.EMBARK);
					_widgets.Add(new ButtonWidget {
						Label = label,
						Component = proceedButton,
						GameObject = proceedButton.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.DiscoverTopLevelWidgets(proceedButton): {ex.Message}");
			}

			// Back button (MinionSelectScreen only)
			if (IsMinionSelectScreen) {
				try {
					var backButton = Traverse.Create(screen).Field("backButton")
						.GetValue<KButton>();
					if (backButton != null && backButton.gameObject.activeInHierarchy
						&& backButton.isInteractable) {
						string label = GetButtonLabel(backButton, (string)STRINGS.UI.SANDBOXTOOLS.FILTERS.BACK);
						_widgets.Add(new ButtonWidget {
							Label = label,
							Component = backButton,
							GameObject = backButton.gameObject
						});
					}
				} catch (System.Exception ex) {
					Util.Log.Error($"MinionSelectHandler.DiscoverTopLevelWidgets(backButton): {ex.Message}");
				}
			}

			if (_widgets.Count == 0) {
				Util.Log.Debug("MinionSelectHandler.DiscoverTopLevelWidgets: 0 widgets");
				return false;
			}

			Util.Log.Debug($"MinionSelectHandler.DiscoverTopLevelWidgets: {_widgets.Count} widgets");
			return true;
		}

		/// <summary>
		/// Discover dupe mode widgets for the current slot.
		/// </summary>
		private bool DiscoverDupeModeWidgets(KScreen screen) {
			_containers = screen.GetComponentsInChildren<CharacterContainer>(true);
			if (_containers == null || _containers.Length == 0) {
				Util.Log.Debug("MinionSelectHandler.DiscoverDupeModeWidgets: no containers");
				return false;
			}

			if (_currentSlot >= _containers.Length) _currentSlot = 0;
			var container = _containers[_currentSlot] as CharacterContainer;
			if (container == null || !container.gameObject.activeInHierarchy) {
				Util.Log.Debug("MinionSelectHandler.DiscoverDupeModeWidgets: container null or inactive");
				return false;
			}

			// Check if character data is ready (stats populated by coroutine)
			var ct = Traverse.Create(container);
			var stats = ct.Field("stats").GetValue<object>();
			if (stats == null) {
				Util.Log.Debug("MinionSelectHandler.DiscoverDupeModeWidgets: stats null (coroutine pending)");
				return false;
			}

			DiscoverSlotWidgets(container);

			if (_widgets.Count == 0) {
				Util.Log.Debug("MinionSelectHandler.DiscoverDupeModeWidgets: 0 widgets after discovery");
				return false;
			}

			Util.Log.Debug($"MinionSelectHandler.DiscoverDupeModeWidgets: {_widgets.Count} widgets in slot {_currentSlot}");
			return true;
		}

		/// <summary>
		/// Build the widget list for a single CharacterContainer slot.
		/// Order: name, interests, traits (one per), attributes (one per),
		/// interest filter dropdown, reroll button.
		/// </summary>
		private void DiscoverSlotWidgets(CharacterContainer container) {
			var traverse = Traverse.Create(container);

			// (a) Name
			DiscoverNameWidget(container, traverse);

			// (b) Interests: one per interest with attribute bonus
			DiscoverInterestsWidget(container, traverse);

			// (c) Traits: one per trait, with full info
			DiscoverTraitWidgets(container, traverse);

			// (d) Expectations: stress reaction & overjoyed response
			DiscoverExpectationWidgets(container, traverse);

			// (e) Attributes: one per attribute
			DiscoverAttributeWidgets(container, traverse);

			// (f) Description / bio
			DiscoverDescriptionWidget(container, traverse);

			// (g) Interest filter dropdown (archetypeDropDown)
			DiscoverFilterDropdown(container, traverse);

			// (g.5) Model filter dropdown (modelDropDown, DLC3 only)
			DiscoverModelDropdown(container, traverse);

			// (h) Reroll button (reshuffleButton)
			DiscoverRerollButton(container, traverse);
		}

		/// <summary>
		/// Discover the duplicant name widget from the CharacterContainer.
		/// Tries characterNameTitle (EditableTitleBar) first, then falls back
		/// to searching for LocText children with a name-like pattern.
		/// </summary>
		private void DiscoverNameWidget(CharacterContainer container, Traverse traverse) {
			CharacterWidgetBuilder.AddNameWidgets(_widgets, traverse, "MinionSelectHandler");

			try {
				var titleBar = traverse.Field("characterNameTitle").GetValue<object>();
				if (titleBar == null) return;
				var titleBarTraverse = Traverse.Create(titleBar);

				// Rename button (editNameButton)
				var editBtn = titleBarTraverse.Field("editNameButton").GetValue<KButton>();
				if (editBtn != null && editBtn.gameObject.activeInHierarchy) {
					_widgets.Add(new ButtonWidget {
						Label = STRINGS.ONIACCESS.PANELS.RENAME,
						Component = editBtn,
						GameObject = editBtn.gameObject,
						Tag = "dupe_rename"
					});
				}

				// Shuffle name button (randomNameButton)
				var randomBtn = titleBarTraverse.Field("randomNameButton").GetValue<KButton>();
				if (randomBtn != null) {
					_widgets.Add(new ButtonWidget {
						Label = STRINGS.ONIACCESS.PANELS.SHUFFLE_NAME,
						Component = randomBtn,
						GameObject = randomBtn.gameObject,
						Tag = "dupe_shuffle_name"
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.DiscoverNameWidget: {ex.Message}");
			}
		}

		/// <summary>
		/// Discover the interests/aptitudes label. Combines all interests into
		/// one label: "Interests: Research, Cooking".
		/// </summary>
		private void DiscoverInterestsWidget(CharacterContainer container, Traverse traverse) {
			CharacterWidgetBuilder.AddInterestWidgets(_widgets, traverse, "MinionSelectHandler");
		}

		/// <summary>
		/// Discover trait widgets. Each trait gets its own widget with full info:
		/// name + effect + description combined into one label.
		/// Per locked decision: "Traits: full info upfront."
		/// Distinguishes positive/negative traits, and bionic upgrade/bug for bionic dupes.
		/// </summary>
		private void DiscoverTraitWidgets(CharacterContainer container, Traverse traverse) {
			var stats = traverse.Field("stats").GetValue<MinionStartingStats>();
			if (stats == null) return;
			CharacterWidgetBuilder.AddTraitWidgets(_widgets, stats, container.gameObject, "MinionSelectHandler");
		}

		private void DiscoverExpectationWidgets(CharacterContainer container, Traverse traverse) {
			CharacterWidgetBuilder.AddExpectationWidgets(_widgets, traverse, "MinionSelectHandler");
		}

		private void DiscoverDescriptionWidget(CharacterContainer container, Traverse traverse) {
			var stats = traverse.Field("stats").GetValue<MinionStartingStats>();
			if (stats == null) return;
			CharacterWidgetBuilder.AddDescriptionWidgets(_widgets, traverse, stats, container.gameObject, "MinionSelectHandler");
		}

		private void DiscoverAttributeWidgets(CharacterContainer container, Traverse traverse) {
			CharacterWidgetBuilder.AddAttributeWidgets(_widgets, traverse, "MinionSelectHandler");
		}

		private void DiscoverFilterDropdown(CharacterContainer container, Traverse traverse) {
			try {
				var dropdown = traverse.Field("archetypeDropDown")
					.GetValue<DropDown>();
				if (dropdown != null && dropdown.gameObject.activeInHierarchy) {
					_widgets.Add(new DropdownWidget {
						Label = GetInterestFilterLabel(container),
						Component = dropdown,
						GameObject = dropdown.gameObject,
						Tag = "interest_filter",
						SpeechFunc = () => GetInterestFilterLabel(_containers[_currentSlot] as CharacterContainer)
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.DiscoverFilterDropdown: {ex.Message}");
			}
		}

		private string GetInterestFilterLabel(CharacterContainer container) {
			try {
				var ct = Traverse.Create(container);
				var aptId = ct.Field("guaranteedAptitudeID").GetValue<string>();
				if (string.IsNullOrEmpty(aptId)) {
					return $"{STRINGS.ONIACCESS.INFO.INTEREST_FILTER}, {STRINGS.ONIACCESS.STATES.ANY}";
				}
				var skillGroup = Db.Get().SkillGroups.TryGet(aptId);
				return skillGroup != null
					? $"{STRINGS.ONIACCESS.INFO.INTEREST_FILTER}, {skillGroup.Name}"
					: $"{STRINGS.ONIACCESS.INFO.INTEREST_FILTER}, {aptId}";
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.GetInterestFilterLabel: {ex.Message}");
				return (string)STRINGS.ONIACCESS.INFO.INTEREST_FILTER;
			}
		}

		protected override void CycleDropdown(Widget widget, int direction) {
			if (!(widget.Tag is string tag)) return;
			if (tag == "model_filter") {
				CycleModelDropdown(direction);
				return;
			}
			if (tag != "interest_filter") return;
			try {
				var container = _containers[_currentSlot] as CharacterContainer;
				var ct = Traverse.Create(container);
				var dropdown = ct.Field("archetypeDropDown").GetValue<DropDown>();
				if (dropdown == null) return;

				var entries = dropdown.Entries;
				if (entries == null || entries.Count == 0) return;

				var currentId = ct.Field("guaranteedAptitudeID").GetValue<string>();

				// Find current index (-1 = "Any" / no filter)
				int currentIdx = -1;
				if (!string.IsNullOrEmpty(currentId)) {
					for (int i = 0; i < entries.Count; i++) {
						if (entries[i] is SkillGroup sg && sg.Id == currentId) {
							currentIdx = i;
							break;
						}
					}
				}

				// Cycle: -1 (Any) -> 0 -> 1 -> ... -> Count-1 -> -1 (Any)
				int newIdx = currentIdx + direction;
				if (newIdx < -1) newIdx = entries.Count - 1;
				if (newIdx >= entries.Count) newIdx = -1;

				// Invoke the callback through the dropdown's onEntrySelectedAction
				var onSelect = Traverse.Create(dropdown)
					.Field("onEntrySelectedAction")
					.GetValue<System.Action<IListableOption, object>>();
				if (onSelect != null) {
					var selected = newIdx >= 0 ? entries[newIdx] : null;
					onSelect(selected, dropdown.targetData);
				}

				// Reshuffle triggers a coroutine — delay one frame then announce
				_pendingAnnounce = AnnounceAfterFilterChange;
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.CycleDropdown: {ex.Message}");
			}
		}

		private void DiscoverRerollButton(CharacterContainer container, Traverse traverse) {
			try {
				var reshuffleButton = traverse.Field("reshuffleButton")
					.GetValue<KButton>();
				if (reshuffleButton != null && reshuffleButton.gameObject.activeInHierarchy
					&& reshuffleButton.isInteractable) {
					_widgets.Add(new ButtonWidget {
						Label = (string)STRINGS.UI.IMMIGRANTSCREEN.SHUFFLE,
						Component = reshuffleButton,
						GameObject = reshuffleButton.gameObject,
						Tag = "reroll"
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.DiscoverRerollButton: {ex.Message}");
			}
		}

		// ========================================
		// WIDGET SPEECH
		// ========================================

		/// <summary>
		/// Allow the shuffle name button to be navigated even when its
		/// GameObject is inactive — the game hides randomNameButton by default
		/// but we can still click it programmatically.
		/// </summary>
		protected override bool IsWidgetValid(Widget widget) {
			if (widget.Tag is string tag && tag == "dupe_shuffle_name")
				return widget.Component != null;
			return base.IsWidgetValid(widget);
		}

		/// <summary>
		/// Suppress auto-tooltip for widgets that already bake tooltip into
		/// their label, or where the auto-discovered tooltip is wrong
		/// (e.g., enter_dupe_mode picks up editNameButton's tooltip).
		/// </summary>
		protected override string GetTooltipText(Widget widget) {
			if (widget.Tag is string tag) {
				switch (tag) {
					// These already have tooltip content in their Label
					case "interest":
					case "dupe_rename":
					case "dupe_shuffle_name":
					// This picks up an unrelated child tooltip
					case "enter_dupe_mode":
					// Label-only widgets with no useful tooltip
					case "model_type":
					case "model_filter":
						return null;
				}
			}
			// In dupe mode, traits/expectations/attributes/description are all
			// label-only widgets with no tag — suppress tooltip for plain labels
			if (_inDupeMode && widget is LabelWidget) return null;
			return base.GetTooltipText(widget);
		}

		// ========================================
		// WIDGET ACTIVATION (Enter key)
		// ========================================

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];

			// Enter dupe mode
			if (widget.Tag is string tag && tag == "enter_dupe_mode") {
				_inDupeMode = true;
				_currentSlot = 0;
				bool ready = DiscoverWidgets(_screen);
				CurrentIndex = 0;
				if (ready && _widgets.Count > 0) {
					Speech.SpeechPipeline.SpeakInterrupt(
						$"{string.Format(STRINGS.ONIACCESS.INFO.SLOT, _currentSlot + 1)}, {GetWidgetSpeechText(_widgets[0])}");
				} else {
					_pendingRediscovery = true;
				}
				return;
			}

			// Colony name text editing — base handles KInputTextField via TextEdit
			if (widget.Tag is string nameTag && nameTag == "colony_name"
				&& widget.Component is KInputTextField) {
				base.ActivateCurrentItem();
				return;
			}

			// Colony shuffle button: click, defer read by one frame
			if (widget.Tag is string shuffleTag && shuffleTag == "colony_shuffle") {
				base.ActivateCurrentItem();
				_pendingAnnounce = AnnounceAfterColonyShuffle;
				return;
			}

			// Dupe rename button: enter text edit mode on EditableTitleBar.inputField
			if (widget.Tag is string renameTag && renameTag == "dupe_rename") {
				try {
					int slot = _currentSlot;
					var container = _containers[slot] as CharacterContainer;
					var titleBar = Traverse.Create(container)
						.Field("characterNameTitle").GetValue<object>();
					var titleText = Traverse.Create(titleBar)
						.Field("titleText").GetValue<LocText>();
					string currentName = titleText != null ? titleText.text : null;
					TextEdit.Begin(
						() => Traverse.Create(titleBar)
							.Field("inputField").GetValue<KInputTextField>(),
						initialText: currentName);
				} catch (System.Exception ex) {
					Util.Log.Error($"MinionSelectHandler.ActivateCurrentItem(dupe_rename): {ex.Message}");
				}
				return;
			}

			// Dupe shuffle name button: click, defer read by one frame
			if (widget.Tag is string dupeShuffleTag && dupeShuffleTag == "dupe_shuffle_name") {
				ClickButton((KButton)widget.Component);
				_pendingAnnounce = AnnounceAfterDupeShuffle;
				return;
			}

			// Reroll button in dupe mode
			if (widget.Tag is string rerollTag && rerollTag == "reroll") {
				ClickButton((KButton)widget.Component);
				// Delay announcement by one frame for SetAttributes coroutine
				_pendingAnnounce = AnnounceAfterReroll;
				return;
			}

			base.ActivateCurrentItem();
		}

		/// <summary>
		/// After reroll, wait one frame then rediscover and announce.
		/// </summary>
		private void AnnounceAfterReroll() {
			DiscoverWidgets(_screen);
			CurrentIndex = FindWidgetByTag("reroll");
			AnnounceNameAndInterests();
		}

		private void AnnounceAfterColonyShuffle() {
			try {
				var baseNamingObj = _screen.gameObject.GetComponent(
					HarmonyLib.AccessTools.TypeByName("BaseNaming"));
				if (baseNamingObj != null) {
					var inputField = Traverse.Create(baseNamingObj)
						.Field("inputField").GetValue<KInputTextField>();
					if (inputField != null) {
						Speech.SpeechPipeline.SpeakInterrupt(
							$"{STRINGS.ONIACCESS.PANELS.COLONY_NAME}, {inputField.text}");
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.AnnounceAfterColonyShuffle: {ex.Message}");
			}
		}

		private void AnnounceAfterDupeShuffle() {
			DiscoverWidgets(_screen);
			CurrentIndex = FindWidgetByTag("dupe_shuffle_name");
			if (_widgets.Count > 0) {
				Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
			}
		}

		/// <summary>
		/// Find a widget by tag, returning its index or clamped fallback.
		/// </summary>
		private int FindWidgetByTag(string targetTag) {
			for (int i = 0; i < _widgets.Count; i++) {
				if (_widgets[i].Tag is string t && t == targetTag)
					return i;
			}
			return _widgets.Count > 0 ? _widgets.Count - 1 : 0;
		}

		/// <summary>
		/// Interrupt-speak name (first widget) then queue interest-tagged widgets.
		/// Does not change CurrentIndex.
		/// </summary>
		private void AnnounceNameAndInterests() {
			if (_widgets.Count > 0)
				Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
			QueueNameAndInterests(includeName: false);
		}

		/// <summary>
		/// Queue-speak name (first widget) and all interest-tagged widgets.
		/// Does not change CurrentIndex. Does not interrupt.
		/// </summary>
		private void QueueNameAndInterests(bool includeName = true) {
			bool seenInterest = false;
			for (int i = 0; i < _widgets.Count; i++) {
				var w = _widgets[i];
				bool isInterest = w.Tag is string tag && tag == "interest";

				if (i == 0 && includeName || isInterest) {
					Speech.SpeechPipeline.SpeakQueued(GetWidgetSpeechText(w));
					if (isInterest) seenInterest = true;
				} else if (seenInterest) {
					break;
				}
			}
		}

		private void AnnounceAfterFilterChange() {
			DiscoverWidgets(_screen);
			// Find the filter widget by tag — index shifts when trait/interest count changes
			CurrentIndex = FindWidgetByTag("interest_filter");
			Speech.SpeechPipeline.SpeakInterrupt(
				GetInterestFilterLabel(_containers[_currentSlot] as CharacterContainer));
			// Queue name + interests after the filter label (don't interrupt)
			QueueNameAndInterests();
		}

		// ========================================
		// MODEL FILTER DROPDOWN (DLC3)
		// ========================================

		private void DiscoverModelDropdown(CharacterContainer container, Traverse traverse) {
			try {
				var dropdown = traverse.Field("modelDropDown").GetValue<DropDown>();
				if (dropdown != null && dropdown.transform.parent.gameObject.activeInHierarchy) {
					_widgets.Add(new DropdownWidget {
						Label = GetModelFilterLabel(),
						Component = dropdown,
						GameObject = dropdown.gameObject,
						Tag = "model_filter",
						SpeechFunc = () => GetModelFilterLabel()
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.DiscoverModelDropdown: {ex.Message}");
			}
		}

		private string GetModelFilterLabel() {
			try {
				var container = _containers[_currentSlot] as CharacterContainer;
				var ct = Traverse.Create(container);
				var models = ct.Field("permittedModels").GetValue<List<Tag>>();
				string title = (string)STRINGS.DUPLICANTS.MODELTITLE;
				if (models == null || models.Count == 0) return title;
				if (models.Count > 1) {
					return $"{title}{STRINGS.UI.CHARACTERCONTAINER_ALL_MODELS}";
				}
				if (models[0] == GameTags.Minions.Models.Bionic) {
					return $"{title}{STRINGS.DUPLICANTS.MODEL.BIONIC.NAME}";
				}
				return $"{title}{STRINGS.DUPLICANTS.MODEL.STANDARD.NAME}";
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.GetModelFilterLabel: {ex.Message}");
				return (string)STRINGS.DUPLICANTS.MODELTITLE;
			}
		}

		private void CycleModelDropdown(int direction) {
			try {
				var container = _containers[_currentSlot] as CharacterContainer;
				var ct = Traverse.Create(container);
				var dropdown = ct.Field("modelDropDown").GetValue<DropDown>();
				if (dropdown == null) return;

				var entries = dropdown.Entries;
				if (entries == null || entries.Count == 0) return;

				var models = ct.Field("permittedModels").GetValue<List<Tag>>();
				if (models == null || models.Count == 0) return;

				// Current index: -1 = Any, 0 = Standard, 1 = Bionic
				int currentIdx;
				if (models.Count > 1) {
					currentIdx = -1;
				} else if (models[0] == GameTags.Minions.Models.Bionic) {
					currentIdx = 1;
				} else {
					currentIdx = 0;
				}

				// Cycle: -1 (Any) -> 0 -> 1 -> ... -> Count-1 -> -1 (Any)
				int newIdx = currentIdx + direction;
				if (newIdx < -1) newIdx = entries.Count - 1;
				if (newIdx >= entries.Count) newIdx = -1;

				var onSelect = Traverse.Create(dropdown)
					.Field("onEntrySelectedAction")
					.GetValue<System.Action<IListableOption, object>>();
				if (onSelect != null) {
					var selected = newIdx >= 0 ? entries[newIdx] : null;
					onSelect(selected, dropdown.targetData);
				}

				_pendingAnnounce = AnnounceAfterModelChange;
			} catch (System.Exception ex) {
				Util.Log.Error($"MinionSelectHandler.CycleModelDropdown: {ex.Message}");
			}
		}

		private void AnnounceAfterModelChange() {
			DiscoverWidgets(_screen);
			CurrentIndex = FindWidgetByTag("model_filter");
			Speech.SpeechPipeline.SpeakInterrupt(GetModelFilterLabel());
			QueueNameAndInterests();
		}

		// ========================================
		// KEY HANDLING
		// ========================================

		/// <summary>
		/// Intercept Escape for dupe mode exit.
		/// Text edit Escape handling is in base.HandleKeyDown.
		/// </summary>
		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			// Dupe mode: Escape exits back to top level
			if (_inDupeMode) {
				if (e.TryConsume(Action.Escape)) {
					_inDupeMode = false;
					_search.Clear();
					DiscoverWidgets(_screen);
					CurrentIndex = 0;
					if (_widgets.Count > 0)
						Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
					return true;
				}
			}

			return false;
		}

		// ========================================
		// TICK: DEFERRED ANNOUNCE
		// ========================================

		public override bool Tick() {
			// Colony name not yet populated by BaseNaming.OnSpawn — re-announce
			if (_pendingColonyNameAnnounce && CurrentIndex == 0 && _widgets.Count > 0) {
				var w = _widgets[0];
				if (w.Tag is string t && t == "colony_name" && w.Component is KInputTextField tf
					&& !string.IsNullOrEmpty(tf.text)) {
					_pendingColonyNameAnnounce = false;
					Speech.SpeechPipeline.SpeakInterrupt(
						$"{STRINGS.ONIACCESS.PANELS.COLONY_NAME}, {tf.text}");
				}
				// Don't return — allow other tick logic to run
			}

			// Deferred one-frame announce (reroll, filter change)
			if (_pendingAnnounce != null) {
				var action = _pendingAnnounce;
				_pendingAnnounce = null;
				action();
				return false;
			}

			return base.Tick();
		}
	}
}
