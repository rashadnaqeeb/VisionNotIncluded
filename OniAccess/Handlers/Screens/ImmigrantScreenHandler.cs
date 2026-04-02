using System.Collections.Generic;
using Database;
using HarmonyLib;
using Klei.AI;

using OniAccess.Input;
using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for ImmigrantScreen (Printing Pod selection, every 3 cycles).
	///
	/// Flat Tab navigation across 3-4 options (mix of duplicants and care packages).
	/// Up/Down drills into detail widgets within the selected option.
	/// Choose selects the deliverable and prints. Reject All opens a confirmation dialog.
	/// Escape closes the screen without rejecting.
	/// </summary>
	public class ImmigrantScreenHandler: BaseWidgetHandler {
		private int _currentSlot;
		private bool _rejectDialogOpen;
		private bool _pendingProceed;
		private List<ITelepadDeliverableContainer> _containers;

		protected override int MaxDiscoveryRetries => 10;

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.PRINTING_POD;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public ImmigrantScreenHandler(KScreen screen) : base(screen) {
			_currentSlot = 0;
			_rejectDialogOpen = false;
			_pendingProceed = false;
			HelpEntries = BuildHelpEntries(new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_OPTION));
		}

		public override void OnActivate() {
			_rejectDialogOpen = false;
			_pendingProceed = false;
			base.OnActivate();
		}

		// ========================================
		// TAB NAVIGATION (switch between options)
		// ========================================

		protected override void NavigateTabForward() {
			if (_rejectDialogOpen) return;
			if (_containers == null || _containers.Count == 0) return;
			_currentSlot = (_currentSlot + 1) % _containers.Count;
			if (_currentSlot == 0) PlaySound("HUD_Click");
			RediscoverAndSpeakSlot();
		}

		protected override void NavigateTabBackward() {
			if (_rejectDialogOpen) return;
			if (_containers == null || _containers.Count == 0) return;
			int prev = _currentSlot;
			_currentSlot = (_currentSlot - 1 + _containers.Count) % _containers.Count;
			if (_currentSlot == _containers.Count - 1 && prev == 0) PlaySound("HUD_Click");
			RediscoverAndSpeakSlot();
		}

		private void RediscoverAndSpeakSlot() {
			DiscoverWidgets(_screen);
			CurrentIndex = 0;
			if (_widgets.Count > 0) {
				Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
			}
		}

		// ========================================
		// WIDGET DISCOVERY
		// ========================================

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			if (_rejectDialogOpen) {
				return DiscoverRejectionDialogWidgets(screen);
			}

			return DiscoverOptionWidgets(screen);
		}

		private bool DiscoverRejectionDialogWidgets(KScreen screen) {
			try {
				var st = Traverse.Create(screen);
				var confirmBtn = st.Field("confirmRejectionBtn").GetValue<KButton>();
				var cancelBtn = st.Field("cancelRejectionBtn").GetValue<KButton>();

				if (confirmBtn != null) {
					_widgets.Add(new ButtonWidget {
						Label = (string)STRINGS.UI.IMMIGRANTSCREEN.REJECTALL,
						Component = confirmBtn,
						GameObject = confirmBtn.gameObject,
						Tag = "confirm_reject"
					});
				}
				if (cancelBtn != null) {
					_widgets.Add(new ButtonWidget {
						Label = (string)STRINGS.UI.CONFIRMDIALOG.CANCEL,
						Component = cancelBtn,
						GameObject = cancelBtn.gameObject,
						Tag = "cancel_reject"
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverRejectionDialogWidgets: {ex.Message}");
			}

			return _widgets.Count > 0;
		}

		private bool DiscoverOptionWidgets(KScreen screen) {
			try {
				_containers = Traverse.Create(screen)
					.Field("containers")
					.GetValue<List<ITelepadDeliverableContainer>>();
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverOptionWidgets(containers): {ex.Message}");
				return false;
			}

			if (_containers == null || _containers.Count == 0) {
				Util.Log.Debug("ImmigrantScreenHandler: no containers");
				return false;
			}

			if (_currentSlot >= _containers.Count) _currentSlot = 0;
			var container = _containers[_currentSlot];

			if (container is CharacterContainer cc) {
				return DiscoverCharacterWidgets(cc);
			}
			if (container is CarePackageContainer cpc) {
				return DiscoverCarePackageWidgets(cpc);
			}

			Util.Log.Warn($"ImmigrantScreenHandler: unknown container type {container.GetType().Name}");
			return false;
		}

		// ========================================
		// CHARACTER CONTAINER DISCOVERY
		// ========================================

		private bool DiscoverCharacterWidgets(CharacterContainer container) {
			var traverse = Traverse.Create(container);

			var stats = traverse.Field("stats").GetValue<MinionStartingStats>();
			if (stats == null) {
				Util.Log.Debug("ImmigrantScreenHandler: stats null (coroutine pending)");
				return false;
			}

			// Name
			try {
				var titleBar = traverse.Field("characterNameTitle").GetValue<object>();
				if (titleBar != null) {
					var locText = Traverse.Create(titleBar).Field("titleText").GetValue<LocText>();
					if (locText != null && !string.IsNullOrEmpty(locText.text)) {
						_widgets.Add(new LabelWidget {
							Label = locText.text,
							GameObject = locText.gameObject
						});

						if (stats.personality.model == GameTags.Minions.Models.Bionic) {
							_widgets.Add(new LabelWidget {
								Label = (string)STRINGS.DUPLICANTS.MODEL.BIONIC.NAME,
								GameObject = locText.gameObject,
								Tag = "model_type"
							});
						}
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(name): {ex.Message}");
			}

			// Interests
			try {
				var aptitudeEntries = traverse.Field("aptitudeEntries")
					.GetValue<List<UnityEngine.GameObject>>();
				if (aptitudeEntries != null) {
					foreach (var entryGo in aptitudeEntries) {
						if (entryGo == null || !entryGo.activeInHierarchy) continue;
						var locTexts = entryGo.GetComponentsInChildren<LocText>(false);
						if (locTexts == null || locTexts.Length == 0) continue;

						var parts = new List<string>();
						foreach (var lt in locTexts) {
							if (lt == null || string.IsNullOrEmpty(lt.text)
								|| !lt.gameObject.activeInHierarchy) continue;
							parts.Add(lt.text.Trim());
						}

						if (parts.Count > 0) {
							_widgets.Add(new LabelWidget {
								Label = $"{STRINGS.ONIACCESS.INFO.INTEREST}: {string.Join(", ", parts)}",
								GameObject = entryGo,
								Tag = "interest"
							});
						}
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(interests): {ex.Message}");
			}

			// Traits (skip index 0)
			try {
				var traits = stats.Traits;
				if (traits != null) {
					bool isBionic = stats.personality.model == GameTags.Minions.Models.Bionic;
					for (int i = 1; i < traits.Count; i++) {
						var trait = traits[i];
						string name = trait.GetName();
						if (string.IsNullOrEmpty(name)) continue;

						string prefix;
						if (isBionic) {
							prefix = trait.PositiveTrait
								? (string)STRINGS.ONIACCESS.INFO.BIONIC_UPGRADE
								: (string)STRINGS.ONIACCESS.INFO.BIONIC_BUG;
						} else {
							prefix = trait.PositiveTrait
								? (string)STRINGS.ONIACCESS.INFO.POSITIVE_TRAIT
								: (string)STRINGS.ONIACCESS.INFO.NEGATIVE_TRAIT;
						}

						string tooltip = trait.GetTooltip();
						string label;
						if (string.IsNullOrEmpty(tooltip)) {
							label = $"{prefix}: {name}";
						} else {
							string flat = tooltip.Replace("\n• ", ", ").Replace("\n", ", ");
							label = $"{prefix}: {name}, {flat}";
						}

						_widgets.Add(new LabelWidget {
							Label = label,
							GameObject = container.gameObject
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(traits): {ex.Message}");
			}

			// Expectations
			try {
				var labels = traverse.Field("expectationLabels")
					.GetValue<List<LocText>>();
				if (labels != null) {
					foreach (var lt in labels) {
						if (lt == null || string.IsNullOrEmpty(lt.text)
							|| !lt.gameObject.activeInHierarchy) continue;

						string label = lt.text.Trim();
						var tooltip = lt.GetComponent<ToolTip>();
						if (tooltip != null) {
							try {
								string ttText = ReadAllTooltipText(tooltip);
								if (!string.IsNullOrEmpty(ttText)) {
									label = $"{label}, {ttText}";
								}
							} catch (System.Exception ex) {
								Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(expectation tooltip): {ex.Message}");
							}
						}

						_widgets.Add(new LabelWidget {
							Label = label,
							GameObject = lt.gameObject
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(expectations): {ex.Message}");
			}

			// Attributes
			try {
				var iconGroups = traverse.Field("iconGroups")
					.GetValue<List<UnityEngine.GameObject>>();
				if (iconGroups != null) {
					foreach (var go in iconGroups) {
						if (go == null || !go.activeInHierarchy) continue;
						var locText = go.GetComponentInChildren<LocText>();
						if (locText == null || string.IsNullOrEmpty(locText.text)) continue;

						string label = locText.text.Trim();
						var tooltip = go.GetComponent<ToolTip>();
						if (tooltip != null) {
							try {
								string ttText = ReadAllTooltipText(tooltip);
								if (!string.IsNullOrEmpty(ttText)) {
									string flat = ttText.Replace("\n", ", ").Replace("\r", "");
									label = $"{label}, {flat}";
								}
							} catch (System.Exception ex) {
								Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(attribute tooltip): {ex.Message}");
							}
						}

						_widgets.Add(new LabelWidget {
							Label = label,
							GameObject = go
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(attributes): {ex.Message}");
			}

			// Description
			try {
				string descKey = "STRINGS.ONIACCESS.DUPE_DESCRIPTIONS."
					+ stats.personality.Name.Replace("-", "_").ToUpper();
				if (Strings.TryGet(descKey, out var descEntry)) {
					_widgets.Add(new LabelWidget {
						Label = string.Format((string)STRINGS.ONIACCESS.INFO.DUPE_DESCRIPTION, descEntry.String),
						GameObject = container.gameObject
					});
				}

				var descLocText = traverse.Field("description").GetValue<LocText>();
				if (descLocText != null && !string.IsNullOrEmpty(descLocText.text)) {
					_widgets.Add(new LabelWidget {
						Label = descLocText.text.Trim(),
						GameObject = descLocText.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCharacterWidgets(description): {ex.Message}");
			}

			AppendActionButtons();

			if (_widgets.Count == 0) {
				Util.Log.Debug("ImmigrantScreenHandler: 0 character widgets");
				return false;
			}

			Util.Log.Debug($"ImmigrantScreenHandler: {_widgets.Count} character widgets in slot {_currentSlot}");
			return true;
		}

		// ========================================
		// CARE PACKAGE CONTAINER DISCOVERY
		// ========================================

		private bool DiscoverCarePackageWidgets(CarePackageContainer container) {
			var traverse = Traverse.Create(container);

			// carePackageInstanceData is created in GenerateCharacter right before
			// SetInfoText — if it exists, all data methods are safe to call.
			if (container.carePackageInstanceData == null) {
				Util.Log.Debug("ImmigrantScreenHandler: carePackageInstanceData null (coroutine pending)");
				return false;
			}

			// Invoke the same private methods SetInfoText uses, bypassing
			// serialized LocText field references which may not match the
			// decompiled field names in the actual prefab.
			try {
				string name = traverse.Method("GetSpawnableName").GetValue<string>();
				if (!string.IsNullOrEmpty(name)) {
					_widgets.Add(new LabelWidget {
						Label = name.Trim(),
						GameObject = container.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCarePackageWidgets(name): {ex.Message}");
			}

			try {
				string qty = traverse.Method("GetSpawnableQuantityOnly").GetValue<string>();
				if (!string.IsNullOrEmpty(qty)) {
					_widgets.Add(new LabelWidget {
						Label = qty.Trim(),
						GameObject = container.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCarePackageWidgets(quantity): {ex.Message}");
			}

			try {
				var inventory = ClusterManager.Instance.activeWorld.worldInventory;
				string current = traverse.Method("GetCurrentQuantity",
					new System.Type[] { typeof(WorldInventory) })
					.GetValue<string>(inventory);
				if (!string.IsNullOrEmpty(current)) {
					// Game returns "Available: {amount}" — extract the amount
					// and reformat with the mod's translatable string.
					string gameFormat = (string)STRINGS.UI.IMMIGRANTSCREEN.CARE_PACKAGE_CURRENT_AMOUNT;
					int placeholder = gameFormat.IndexOf("{0}");
					if (placeholder >= 0) {
						string gamePrefix = gameFormat.Substring(0, placeholder);
						string gameSuffix = gameFormat.Substring(placeholder + 3);
						string amount = current;
						if (gamePrefix.Length > 0 && amount.StartsWith(gamePrefix))
							amount = amount.Substring(gamePrefix.Length);
						if (gameSuffix.Length > 0 && amount.EndsWith(gameSuffix))
							amount = amount.Substring(0, amount.Length - gameSuffix.Length);
						current = string.Format(
							(string)STRINGS.ONIACCESS.INFO.COLONY_HAS, amount);
					}
					_widgets.Add(new LabelWidget {
						Label = current.Trim(),
						GameObject = container.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCarePackageWidgets(currentQuantity): {ex.Message}");
			}

			try {
				string desc = traverse.Method("GetSpawnableDescription").GetValue<string>();
				if (!string.IsNullOrEmpty(desc)) {
					_widgets.Add(new LabelWidget {
						Label = desc.Trim(),
						GameObject = container.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCarePackageWidgets(description): {ex.Message}");
			}

			try {
				string effects = traverse.Method("GetSpawnableEffects").GetValue<string>();
				if (!string.IsNullOrEmpty(effects) && effects.Trim().Length > 0) {
					_widgets.Add(new LabelWidget {
						Label = effects.Trim(),
						GameObject = container.gameObject
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.DiscoverCarePackageWidgets(effects): {ex.Message}");
			}

			AppendActionButtons();

			if (_widgets.Count == 0) {
				Util.Log.Debug("ImmigrantScreenHandler: 0 care package widgets");
				return false;
			}

			Util.Log.Debug($"ImmigrantScreenHandler: {_widgets.Count} care package widgets in slot {_currentSlot}");
			return true;
		}

		// ========================================
		// ACTION BUTTONS (appended to every option)
		// ========================================

		private void AppendActionButtons() {
			_widgets.Add(new ButtonWidget {
				Label = (string)STRINGS.UI.IMMIGRANTSCREEN.PROCEEDBUTTON,
				Component = null,
				GameObject = _screen.gameObject,
				Tag = "choose"
			});

			_widgets.Add(new ButtonWidget {
				Label = (string)STRINGS.UI.IMMIGRANTSCREEN.REJECTALL,
				Component = null,
				GameObject = _screen.gameObject,
				Tag = "reject_all"
			});
		}

		// ========================================
		// WIDGET SPEECH
		// ========================================

		protected override string GetTooltipText(Widget widget) {
			if (widget.Tag is string tag) {
				switch (tag) {
					case "interest":
					case "model_type":
					case "choose":
					case "reject_all":
					case "confirm_reject":
					case "cancel_reject":
						return null;
				}
			}
			if (widget is LabelWidget) return null;
			return base.GetTooltipText(widget);
		}

		// ========================================
		// WIDGET ACTIVATION (Enter key)
		// ========================================

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];

			if (widget.Tag is string tag) {
				switch (tag) {
					case "choose":
						ActivateChoose();
						return;
					case "reject_all":
						ActivateRejectAll();
						return;
					case "confirm_reject":
						ActivateConfirmReject();
						return;
					case "cancel_reject":
						ActivateCancelReject();
						return;
				}
			}

			base.ActivateCurrentItem();
		}

		private void ActivateChoose() {
			if (_containers == null || _currentSlot >= _containers.Count) return;
			var container = _containers[_currentSlot];

			try {
				if (container is CharacterContainer cc) {
					cc.SelectDeliverable();
				} else if (container is CarePackageContainer cpc) {
					cpc.SelectDeliverable();
				}
				_pendingProceed = true;
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.ActivateChoose: {ex.Message}");
			}
		}

		private void ActivateRejectAll() {
			try {
				var rejectButton = Traverse.Create(_screen)
					.Field("rejectButton").GetValue<KButton>();
				if (rejectButton != null) {
					ClickButton(rejectButton);
					_rejectDialogOpen = true;
					DiscoverWidgets(_screen);
					CurrentIndex = 0;
					Speech.SpeechPipeline.SpeakInterrupt(
						(string)STRINGS.UI.IMMIGRANTSCREEN.CONFIRMATIONTITLE);
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.ActivateRejectAll: {ex.Message}");
			}
		}

		private void ActivateConfirmReject() {
			try {
				var confirmBtn = Traverse.Create(_screen)
					.Field("confirmRejectionBtn").GetValue<KButton>();
				if (confirmBtn != null) {
					ClickButton(confirmBtn);
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.ActivateConfirmReject: {ex.Message}");
			}
		}

		private void ActivateCancelReject() {
			try {
				var cancelBtn = Traverse.Create(_screen)
					.Field("cancelRejectionBtn").GetValue<KButton>();
				if (cancelBtn != null) {
					ClickButton(cancelBtn);
					_rejectDialogOpen = false;
					DiscoverWidgets(_screen);
					CurrentIndex = 0;
					if (_widgets.Count > 0) {
						Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.ActivateCancelReject: {ex.Message}");
			}
		}

		// ========================================
		// KEY HANDLING
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			if (_rejectDialogOpen) {
				if (e.TryConsume(Action.Escape)) {
					ActivateCancelReject();
					return true;
				}
			} else {
				if (e.TryConsume(Action.Escape)) {
					try {
						var closeButton = Traverse.Create(_screen)
							.Field("closeButton").GetValue<KButton>();
						if (closeButton != null) {
							ClickButton(closeButton);
						}
					} catch (System.Exception ex) {
						Util.Log.Error($"ImmigrantScreenHandler.HandleKeyDown(Escape): {ex.Message}");
					}
					return true;
				}
			}

			return false;
		}

		// ========================================
		// TICK
		// ========================================

		public override bool Tick() {
			if (_pendingProceed) {
				_pendingProceed = false;
				try {
					var proceedButton = Traverse.Create(_screen)
						.Field("proceedButton").GetValue<KButton>();
					if (proceedButton != null) {
						ClickButton(proceedButton);
					}
				} catch (System.Exception ex) {
					Util.Log.Error($"ImmigrantScreenHandler.Tick(proceed): {ex.Message}");
				}
				return false;
			}

			// Sync rejection dialog state
			try {
				var rejectScreen = Traverse.Create(_screen)
					.Field("rejectConfirmationScreen").GetValue<UnityEngine.GameObject>();
				if (rejectScreen != null) {
					bool dialogActive = rejectScreen.activeSelf;
					if (dialogActive != _rejectDialogOpen) {
						_rejectDialogOpen = dialogActive;
						DiscoverWidgets(_screen);
						CurrentIndex = 0;
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"ImmigrantScreenHandler.Tick(rejectSync): {ex.Message}");
			}

			return base.Tick();
		}
	}
}
