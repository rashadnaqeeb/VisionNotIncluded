using System.Collections.Generic;
using Database;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OniAccess.Widgets {
	static class SideScreenOverrides {
		public static void RegisterAll() {
			SideScreenWalker.RegisterOverride<PixelPackSideScreen>(WalkPixelPack);
			SideScreenWalker.RegisterOverride<CommandModuleSideScreen>(WalkCommandModule);
			SideScreenWalker.RegisterOverride<ConditionListSideScreen>(WalkConditionList);
			SideScreenWalker.RegisterOverride<AlarmSideScreen>(WalkAlarm);
			SideScreenWalker.RegisterOverride<FewOptionSideScreen>(WalkFewOption);
			SideScreenWalker.RegisterOverride<TreeFilterableSideScreen>(WalkTreeFilter);
			SideScreenWalker.RegisterOverride<ComplexFabricatorSideScreen>(WalkComplexFabricator);
			SideScreenWalker.RegisterOverride<AccessControlSideScreen>(WalkAccessControl);
			SideScreenWalker.RegisterOverride<OwnablesSidescreen>(WalkOwnables);
			SideScreenWalker.RegisterOverride<AssignableSideScreen>(WalkAssignable);
			SideScreenWalker.RegisterOverride<BionicSideScreen>(WalkBionic);
			SideScreenWalker.RegisterOverride<ArtableSelectionSideScreen>(WalkArtableSelection);
			SideScreenWalker.RegisterOverride<MonumentSideScreen>(WalkMonument);
			SideScreenWalker.RegisterOverride<CritterSensorSideScreen>(WalkCritterSensor);
			SideScreenWalker.RegisterOverride<HighEnergyParticleDirectionSideScreen>(WalkHEPDirection);
			SideScreenWalker.RegisterOverride<TelepadSideScreen>(WalkTelepad);
			SideScreenWalker.RegisterOverride<ClusterDestinationSideScreen>(WalkClusterDestination);
			SideScreenWalker.RegisterOverride<CheckboxListGroupSideScreen>(WalkCheckboxListGroup);
			SideScreenWalker.RegisterOverride<ModuleFlightUtilitySideScreen>(WalkModuleFlightUtility);
			SideScreenWalker.RegisterOverride<RocketModuleSideScreen>(WalkRocketModule);
			SideScreenWalker.RegisterOverride<DispenserSideScreen>(WalkDispenser);
			SideScreenWalker.RegisterOverride<IncubatorSideScreen>(WalkIncubator);
			SideScreenWalker.RegisterOverride<CounterSideScreen>(WalkCounter);
			SideScreenWalker.RegisterOverride<LogicBitSelectorSideScreen>(WalkLogicBitSelector);
			SideScreenWalker.RegisterOverride<RemoteWorkTerminalSidescreen>(WalkRemoteWorkTerminal);
			SideScreenWalker.RegisterOverride<LureSideScreen>(WalkLure);
			SideScreenWalker.RegisterOverride<CometDetectorSideScreen>(WalkCometDetector);
			SideScreenWalker.RegisterOverride<NToggleSideScreen>(WalkNToggle);
			SideScreenWalker.RegisterOverride<GeneticAnalysisStationSideScreen>(WalkGeneticAnalysis);
			SideScreenWalker.RegisterOverride<RelatedEntitiesSideScreen>(WalkRelatedEntities);
			SideScreenWalker.RegisterOverride<GeoTunerSideScreen>(WalkGeoTuner);
			SideScreenWalker.RegisterOverride<BaseGameImpactorImperativeSideScreen>(WalkBaseGameImpactorImperative);
			SideScreenWalker.RegisterOverride<FilterSideScreen>(WalkFilterSideScreen);
			SideScreenWalker.RegisterOverride<SingleItemSelectionSideScreen>(WalkSingleItemSelection);
			SideScreenWalker.RegisterOverride<AssignPilotAndCrewSideScreen>(WalkAssignPilotAndCrew);
			SideScreenWalker.RegisterOverride<RocketRestrictionSideScreen>(WalkRocketRestriction);
			SideScreenWalker.RegisterOverride<PlanterSideScreen>(WalkPlanter);
			SideScreenWalker.RegisterOverride<ProgressBarSideScreen>(WalkProgressBar);
			SideScreenWalker.RegisterOverride<SingleSliderSideScreen>(WalkSingleSlider);
			SideScreenWalker.RegisterOverride<ConfigureConsumerSideScreen>(WalkConfigureConsumer);
		}

		static void WalkPixelPack(PixelPackSideScreen pixelPack, List<Widget> items) {
			// Palette group (drillable)
			var swatchContainer = pixelPack.colorSwatchContainer.transform;
			var paletteChildren = new List<Widget>();
			for (int i = 0; i < swatchContainer.childCount; i++) {
				var child = swatchContainer.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var swatchGO = child.gameObject;
				var capturedGO = swatchGO;
				var img = swatchGO.GetComponent<Image>();
				if (img == null) continue;
				string label = ColorNameUtil.GetColorName(img.color) ?? capturedGO.name;
				paletteChildren.Add(new ButtonWidget {
					Label = label,
					Component = swatchGO.GetComponent<KButton>(),
					GameObject = capturedGO,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = ColorNameUtil.GetColorName(capturedGO.GetComponent<Image>().color)
							?? capturedGO.name;
						var href = capturedGO.GetComponent<HierarchyReferences>();
						var selectedRef = href.GetReference("selected");
						bool isSelected = selectedRef != null && selectedRef.gameObject.activeSelf;
						var usedImage = href.GetReference("used").GetComponentInChildren<Image>();
						bool inUse = usedImage != null && usedImage.gameObject.activeSelf;
						string speech = name;
						if (isSelected)
							speech += $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}";
						if (inUse)
							speech += $", {(string)STRINGS.ONIACCESS.PIXEL_PACK.IN_USE}";
						return speech;
					}
				});
			}
			var capturedContainer = swatchContainer;
			items.Add(new LabelWidget {
				Label = (string)STRINGS.ONIACCESS.PIXEL_PACK.PALETTE,
				GameObject = pixelPack.colorSwatchContainer,
				SuppressTooltip = true,
				Children = paletteChildren,
				SpeechFunc = () => {
					int count = 0;
					for (int i = 0; i < capturedContainer.childCount; i++) {
						if (capturedContainer.GetChild(i).gameObject.activeSelf) count++;
					}
					string countText = string.Format(
						(string)STRINGS.ONIACCESS.PIXEL_PACK.PALETTE_COUNT, count);
					return $"{(string)STRINGS.ONIACCESS.PIXEL_PACK.PALETTE}, {countText}";
				}
			});

			AddColorSlotGroup(pixelPack.activeColors, pixelPack.activeColorsContainer,
				(string)STRINGS.ONIACCESS.PIXEL_PACK.ACTIVE_COLORS, items);
			AddColorSlotGroup(pixelPack.standbyColors, pixelPack.standbyColorsContainer,
				(string)STRINGS.ONIACCESS.PIXEL_PACK.STANDBY_COLORS, items);

			// Action buttons (these have LocText labels)
			var buttons = new[] {
				pixelPack.copyActiveToStandbyButton,
				pixelPack.copyStandbyToActiveButton,
				pixelPack.swapColorsButton
			};
			foreach (var btn in buttons) {
				if (btn == null || !btn.gameObject.activeSelf) continue;
				var captured = btn;
				string label = SideScreenWalker.GetButtonLabel(captured, captured.transform.name);
				if (!SideScreenWalker.HasVisibleContent(label)) continue;
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured,
					GameObject = captured.gameObject,
					SpeechFunc = () => SideScreenWalker.GetButtonLabel(captured, captured.transform.name)
				});
			}
		}

		private static void AddColorSlotGroup(
				List<GameObject> slots, GameObject container,
				string groupLabel, List<Widget> items) {
			var children = new List<Widget>();
			for (int i = 0; i < slots.Count; i++) {
				var slotGO = slots[i];
				var capturedSlot = slotGO;
				int slotIndex = i + 1;
				string slotLabel = string.Format(
					(string)STRINGS.ONIACCESS.PIXEL_PACK.PIXEL_SLOT, slotIndex);
				children.Add(new ButtonWidget {
					Label = slotLabel,
					Component = slotGO.GetComponent<KButton>(),
					GameObject = slotGO,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string colorName = ColorNameUtil.GetColorName(
							capturedSlot.GetComponent<Image>().color) ?? capturedSlot.name;
						return string.Format(
							(string)STRINGS.ONIACCESS.PIXEL_PACK.PIXEL_SLOT, slotIndex)
							+ ", " + colorName;
					}
				});
			}
			items.Add(new LabelWidget {
				Label = groupLabel,
				GameObject = container,
				SuppressTooltip = true,
				Children = children,
				SpeechFunc = () => groupLabel
			});
		}

		static void WalkCommandModule(CommandModuleSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkConditionContainer(screen.conditionListContainer, items);

			var dest = screen.destinationButton;
			if (dest == null || !dest.gameObject.activeSelf) return;
			var captured = dest;
			var childLt = SideScreenWalker.FindChildLocText(dest.transform, null);
			string label = childLt != null
				? childLt.GetParsedText() : dest.transform.name;
			if (!SideScreenWalker.HasVisibleContent(label)) return;
			items.Add(new ToggleWidget {
				Label = label,
				Component = captured,
				GameObject = captured.gameObject,
				SpeechFunc = () => {
					string lbl = childLt != null
						? childLt.GetParsedText()
						: captured.transform.name;
					return $"{lbl}, {WidgetOps.GetMultiToggleState(captured)}";
				}
			});
		}

		static void WalkConditionList(ConditionListSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkConditionContainer(screen.rowContainer, items);
		}

		static void WalkAlarm(AlarmSideScreen alarm, List<Widget> items) {
			SideScreenWalker.WalkDefault(alarm, items);
			CollapseAlarmTypeButtons(alarm, items);
		}

		private static void CollapseAlarmTypeButtons(
				AlarmSideScreen alarm, List<Widget> items) {
			Dictionary<NotificationType, MultiToggle> togglesByType;
			try {
				togglesByType = Traverse.Create(alarm)
					.Field<Dictionary<NotificationType, MultiToggle>>("toggles_by_type").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"CollapseAlarmTypeButtons: toggles_by_type read failed: {ex.Message}");
				return;
			}
			if (togglesByType == null || togglesByType.Count == 0) return;

			// Find the existing MultiToggle item from the type buttons
			var typeToggles = new HashSet<MultiToggle>();
			foreach (var kv in togglesByType)
				typeToggles.Add(kv.Value);

			int insertIndex = -1;
			for (int i = items.Count - 1; i >= 0; i--) {
				var mt = items[i].Component as MultiToggle;
				if (mt != null && typeToggles.Contains(mt)) {
					if (insertIndex < 0) insertIndex = i;
					items.RemoveAt(i);
				}
			}
			if (insertIndex < 0) insertIndex = items.Count;

			// Build RadioMember list in the game's validTypes order
			var validTypes = new[] {
				NotificationType.Bad,
				NotificationType.Neutral,
				NotificationType.DuplicantThreatening
			};
			var members = new List<SideScreenWalker.RadioMember>();
			foreach (var type in validTypes) {
				if (!togglesByType.TryGetValue(type, out var mt)) continue;
				var tooltip = mt.GetComponent<ToolTip>();
				string label = tooltip != null
					? WidgetOps.ReadAllTooltipText(tooltip) : type.ToString();
				if (!SideScreenWalker.HasVisibleContent(label)) label = type.ToString();
				members.Add(new SideScreenWalker.RadioMember {
					Label = label,
					MultiToggleRef = mt,
					Tag = type
				});
			}
			if (members.Count == 0) return;

			string groupLabel = (string)STRINGS.UI.UISIDESCREENS.LOGICALARMSIDESCREEN.TOOLTIPS.TYPE;
			var radioMembers = members;
			var capturedAlarm = alarm;
			var buttonsParent = members[0].MultiToggleRef.transform.parent;
			items.Insert(insertIndex, new DropdownWidget {
				Label = groupLabel,
				Component = members[0].MultiToggleRef,
				SuppressTooltip = true,
				GameObject = buttonsParent != null ? buttonsParent.gameObject
					: members[0].MultiToggleRef.gameObject,
				Tag = radioMembers,
				SpeechFunc = () => {
					string selected = radioMembers[0].Label;
					var activeType = capturedAlarm.targetAlarm.notificationType;
					for (int k = 0; k < radioMembers.Count; k++) {
						if (radioMembers[k].Tag is NotificationType nt && nt == activeType) {
							selected = radioMembers[k].Label;
							break;
						}
					}
					return $"{groupLabel}, {selected}";
				}
			});
		}

		static void WalkFewOption(FewOptionSideScreen fewOption, List<Widget> items) {
			SideScreenWalker.WalkDefault(fewOption, items);
			CollapseFewOptionRows(fewOption, items);
		}

		// TagFilterScreen also targets TreeFilterable but uses KTreeControl instead of rows.
		// It exists as a prefab in ScreenPrefabs but no vanilla building activates it.
		static void WalkTreeFilter(TreeFilterableSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkTreeFilter: Traverse create failed: {ex.Message}");
				return;
			}

			// All toggle
			try {
				var allCheckBox = tv.Field<MultiToggle>("allCheckBox").Value;
				var allCheckBoxLabel = tv.Field<LocText>("allCheckBoxLabel").Value;
				if (allCheckBox != null) {
					var capturedBox = allCheckBox;
					var capturedLabel = allCheckBoxLabel;
					items.Add(new ToggleWidget {
						Label = capturedLabel != null
							? capturedLabel.GetParsedText()
							: (string)STRINGS.UI.UISIDESCREENS.TREEFILTERABLESIDESCREEN.ALLBUTTON,
						Component = capturedBox,
						GameObject = capturedBox.gameObject,
						SpeechFunc = () => {
							string lbl = capturedLabel != null
								? capturedLabel.GetParsedText()
								: (string)STRINGS.UI.UISIDESCREENS.TREEFILTERABLESIDESCREEN.ALLBUTTON;
							return $"{lbl}, {WidgetOps.GetMultiToggleState(capturedBox)}";
						}
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkTreeFilter: allCheckBox read failed: {ex.Message}");
			}

			// Sweep Only toggle
			try {
				var transportRow = tv.Field<GameObject>("onlyallowTransportItemsRow").Value;
				if (transportRow != null && transportRow.activeSelf) {
					var transportCheckBox = tv.Field<MultiToggle>(
						"onlyAllowTransportItemsCheckBox").Value;
					if (transportCheckBox != null) {
						var captured = transportCheckBox;
						string label = (string)STRINGS.UI.UISIDESCREENS
							.TREEFILTERABLESIDESCREEN.ONLYALLOWTRANSPORTITEMSBUTTON;
						items.Add(new ToggleWidget {
							Label = label,
							Component = captured,
							GameObject = captured.gameObject,
							SpeechFunc = () =>
								$"{label}, {WidgetOps.GetMultiToggleState(captured)}"
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkTreeFilter: transport row read failed: {ex.Message}");
			}

			// Seasoned Food Only toggle
			try {
				var spicedRow = tv.Field<GameObject>("onlyallowSpicedItemsRow").Value;
				if (spicedRow != null && spicedRow.activeSelf) {
					var spicedCheckBox = tv.Field<MultiToggle>(
						"onlyAllowSpicedItemsCheckBox").Value;
					if (spicedCheckBox != null) {
						var captured = spicedCheckBox;
						string label = (string)STRINGS.UI.UISIDESCREENS
							.TREEFILTERABLESIDESCREEN.ONLYALLOWSPICEDITEMSBUTTON;
						items.Add(new ToggleWidget {
							Label = label,
							Component = captured,
							GameObject = captured.gameObject,
							SpeechFunc = () =>
								$"{label}, {WidgetOps.GetMultiToggleState(captured)}"
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkTreeFilter: spiced row read failed: {ex.Message}");
			}

			// Category rows from rowGroup
			GameObject rowGroup;
			try { rowGroup = tv.Field<GameObject>("rowGroup").Value; } catch (System.Exception ex) {
				Util.Log.Warn($"WalkTreeFilter: rowGroup read failed: {ex.Message}");
				return;
			}
			if (rowGroup == null) return;

			var rowGroupT = rowGroup.transform;
			var capturedScreen = screen;
			for (int i = 0; i < rowGroupT.childCount; i++) {
				var child = rowGroupT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var row = child.GetComponent<TreeFilterableSideScreenRow>();
				if (row == null) continue;

				try {
					AddTreeFilterRow(row, capturedScreen, items);
				} catch (System.Exception ex) {
					Util.Log.Warn(
						$"WalkTreeFilter: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		private static void AddTreeFilterRow(
				TreeFilterableSideScreenRow row,
				TreeFilterableSideScreen screen,
				List<Widget> items) {
			var rowTv = Traverse.Create(row);
			var checkBoxToggle = rowTv.Field<MultiToggle>("checkBoxToggle").Value;
			var elementNameLt = rowTv.Field<LocText>("elementName").Value;
			var rowElements = rowTv
				.Field<List<TreeFilterableSideScreenElement>>("rowElements").Value;

			var capturedRow = row;
			var capturedToggle = checkBoxToggle;
			var capturedNameLt = elementNameLt;

			string label = capturedNameLt != null
				? capturedNameLt.GetParsedText() : row.transform.name;

			// Build children for individual elements
			List<Widget> children = null;
			if (rowElements != null && rowElements.Count > 0) {
				children = new List<Widget>();
				var capturedScreen = screen;
				foreach (var elem in rowElements) {
					if (elem == null || !elem.gameObject.activeSelf) continue;
					var capturedElem = elem;
					var elemTag = capturedElem.GetElementTag();
					string elemLabel = elemTag.ProperName();
					children.Add(new ToggleWidget {
						Label = elemLabel,
						Component = capturedElem.GetCheckboxToggle(),
						GameObject = capturedElem.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => {
							string name = capturedElem.GetElementTag().ProperName();
							string state = capturedElem.IsSelected
								? (string)STRINGS.ONIACCESS.STATES.ON
								: (string)STRINGS.ONIACCESS.STATES.OFF;
							if (capturedScreen.IsStorage) {
								float mass = capturedScreen.GetAmountInStorage(
									capturedElem.GetElementTag());
								string massText = GameUtil.GetFormattedMass(mass);
								return $"{name}, {massText}, {state}";
							}
							return $"{name}, {state}";
						}
					});
				}
			}

			items.Add(new ToggleWidget {
				Label = label,
				Component = capturedToggle,
				GameObject = row.gameObject,
				SuppressTooltip = true,
				Children = children,
				SpeechFunc = () => {
					string name = capturedNameLt != null
						? capturedNameLt.GetParsedText() : capturedRow.transform.name;
					return $"{name}, {RowStateToString(capturedRow.GetState())}";
				}
			});
		}

		private static string RowStateToString(TreeFilterableSideScreenRow.State state) {
			switch (state) {
				case TreeFilterableSideScreenRow.State.On:
					return (string)STRINGS.ONIACCESS.STATES.ON;
				case TreeFilterableSideScreenRow.State.Off:
					return (string)STRINGS.ONIACCESS.STATES.OFF;
				default:
					return (string)STRINGS.ONIACCESS.STATES.MIXED;
			}
		}

		static void WalkComplexFabricator(ComplexFabricatorSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: Traverse create failed: {ex.Message}");
				return;
			}

			// Order status labels
			try {
				var currentOrderLabel = tv.Field<LocText>("currentOrderLabel").Value;
				if (currentOrderLabel != null && currentOrderLabel.gameObject.activeSelf) {
					var captured = currentOrderLabel;
					string text = captured.GetParsedText();
					if (SideScreenWalker.HasVisibleContent(text)) {
						items.Add(new LabelWidget {
							Label = text,
							GameObject = captured.gameObject,
							SpeechFunc = () => captured.GetParsedText()
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: currentOrderLabel read failed: {ex.Message}");
			}

			try {
				var nextOrderLabel = tv.Field<LocText>("nextOrderLabel").Value;
				if (nextOrderLabel != null && nextOrderLabel.gameObject.activeSelf) {
					var captured = nextOrderLabel;
					string text = captured.GetParsedText();
					if (SideScreenWalker.HasVisibleContent(text)) {
						items.Add(new LabelWidget {
							Label = text,
							GameObject = captured.gameObject,
							SpeechFunc = () => captured.GetParsedText()
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: nextOrderLabel read failed: {ex.Message}");
			}

			// No recipes fallback
			try {
				var noRecipesLabel = tv.Field<LocText>("noRecipesDiscoveredLabel").Value;
				if (noRecipesLabel != null && noRecipesLabel.gameObject.activeSelf) {
					var captured = noRecipesLabel;
					string text = captured.GetParsedText();
					if (SideScreenWalker.HasVisibleContent(text)) {
						items.Add(new LabelWidget {
							Label = text,
							GameObject = captured.gameObject,
							SpeechFunc = () => captured.GetParsedText()
						});
						return;
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: noRecipesDiscoveredLabel read failed: {ex.Message}");
			}

			// Subtitle (e.g. discovery count)
			try {
				var subtitleLabel = tv.Field<LocText>("subtitleLabel").Value;
				if (subtitleLabel != null && subtitleLabel.gameObject.activeSelf) {
					var capturedSubtitle = subtitleLabel;
					string text = capturedSubtitle.GetParsedText();
					if (SideScreenWalker.HasVisibleContent(text)) {
						items.Add(new LabelWidget {
							Label = text,
							GameObject = capturedSubtitle.gameObject,
							SpeechFunc = () => capturedSubtitle.GetParsedText()
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: subtitleLabel read failed: {ex.Message}");
			}

			// Recipe toggles
			List<GameObject> recipeToggles;
			Dictionary<GameObject, List<ComplexRecipe>> recipeCategoryToggleMap;
			try {
				recipeToggles = tv.Field<List<GameObject>>("recipeToggles").Value;
				recipeCategoryToggleMap = tv
					.Field<Dictionary<GameObject, List<ComplexRecipe>>>("recipeCategoryToggleMap").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkComplexFabricator: recipe fields read failed: {ex.Message}");
				return;
			}
			if (recipeToggles == null || recipeCategoryToggleMap == null) return;

			foreach (var toggleGO in recipeToggles) {
				if (toggleGO == null || !toggleGO.activeSelf) continue;
				var href = toggleGO.GetComponent<HierarchyReferences>();
				if (href == null) continue;

				var toggle = toggleGO.GetComponent<KToggle>();
				if (toggle == null) continue;

				var labelLt = href.GetReference<LocText>("Label");
				string label = labelLt != null ? labelLt.GetParsedText() : toggleGO.name;
				if (!SideScreenWalker.HasVisibleContent(label)) continue;

				var capturedGO = toggleGO;
				var capturedHref = href;
				var capturedLabelLt = labelLt;
				items.Add(new ButtonWidget {
					Label = label,
					Component = toggle,
					GameObject = capturedGO,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = capturedLabelLt != null
							? capturedLabelLt.GetParsedText() : capturedGO.name;

						// Queue status
						string queueStatus;
						var infiniteIcon = capturedHref
							.GetReference<RectTransform>("InfiniteIcon");
						if (infiniteIcon != null && infiniteIcon.gameObject.activeSelf) {
							queueStatus = (string)STRINGS.ONIACCESS.FABRICATOR.CONTINUOUS;
						} else {
							var countLabel = capturedHref.GetReference<LocText>("CountLabel");
							string countText = countLabel != null
								? countLabel.GetParsedText() : "";
							if (string.IsNullOrEmpty(countText) || countText == "0") {
								queueStatus = (string)STRINGS.ONIACCESS.FABRICATOR.NOT_QUEUED;
							} else {
								queueStatus = string.Format(
									(string)STRINGS.ONIACCESS.FABRICATOR.QUEUED, countText);
							}
						}

						string speech = $"{name}, {queueStatus}";

						// Tech required (checked first — implies unavailable)
						var techRequired = capturedHref
							.GetReference<RectTransform>("TechRequired");
						if (techRequired != null && techRequired.gameObject.activeSelf) {
							speech += $", {(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE}";
						} else if (capturedLabelLt != null && capturedLabelLt.color.r > 0.0f) {
							speech += $", {(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE}";
						}

						return speech;
					}
				});
			}
		}

		private static void CollapseFewOptionRows(
				FewOptionSideScreen fewOption, List<Widget> items) {
			var rows = fewOption.rows;
			if (rows == null || rows.Count == 0) return;

			FewOptionSideScreen.IFewOptionSideScreen target;
			try {
				target = Traverse.Create(fewOption)
					.Field<FewOptionSideScreen.IFewOptionSideScreen>("targetFewOptions").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"CollapseFewOptionRows: targetFewOptions read failed: {ex.Message}");
				return;
			}
			if (target == null) return;

			// Remove any items the walker already emitted for the row GameObjects
			var rowObjects = new HashSet<GameObject>();
			foreach (var kv in rows)
				rowObjects.Add(kv.Value);
			int insertIndex = -1;
			for (int i = items.Count - 1; i >= 0; i--) {
				if (items[i].GameObject != null && rowObjects.Contains(items[i].GameObject)) {
					insertIndex = i;
					items.RemoveAt(i);
				}
			}
			if (insertIndex < 0) insertIndex = items.Count;

			// Build RadioMember list from the rows
			var members = new List<SideScreenWalker.RadioMember>();
			foreach (var kv in rows) {
				var go = kv.Value;
				var href = go.GetComponent<HierarchyReferences>();
				LocText labelLt = href != null ? href.GetReference<LocText>("label") : null;
				string label = labelLt != null ? labelLt.GetParsedText() : kv.Key.ToString();
				members.Add(new SideScreenWalker.RadioMember {
					Label = label,
					MultiToggleRef = go.GetComponent<MultiToggle>(),
					Tag = kv.Key
				});
			}
			if (members.Count == 0) return;

			string groupLabel = fewOption.GetTitle();
			if (string.IsNullOrEmpty(groupLabel))
				groupLabel = members[0].Label;
			var radioMembers = members;
			var capturedTarget = target;
			var capturedRows = rows;
			items.Insert(insertIndex, new DropdownWidget {
				Label = groupLabel,
				Component = members[0].MultiToggleRef,
				SuppressTooltip = true,
				GameObject = fewOption.rowContainer != null
					? fewOption.rowContainer.gameObject
					: members[0].MultiToggleRef.gameObject,
				Tag = radioMembers,
				SpeechFunc = () => {
					var selectedTag = capturedTarget.GetSelectedOption();
					string selected = null;
					for (int k = 0; k < radioMembers.Count; k++) {
						if (radioMembers[k].Tag is Tag t && t == selectedTag) {
							selected = radioMembers[k].Label;
							break;
						}
					}
					if (selected == null) selected = radioMembers[0].Label;
					string speech = $"{groupLabel}, {selected}";
					// Read tooltip from the selected row for description
					if (capturedRows.TryGetValue(selectedTag, out var rowGO)) {
						var tooltip = rowGO.GetComponent<ToolTip>();
						if (tooltip != null) {
							string desc = WidgetOps.ReadAllTooltipText(tooltip);
							if (!string.IsNullOrEmpty(desc))
								speech += ", " + desc;
						}
					}
					return speech;
				}
			});
		}

		static void WalkAccessControl(AccessControlSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkAccessControl: Traverse create failed: {ex.Message}");
				return;
			}

			AccessControl target;
			try { target = tv.Field<AccessControl>("target").Value; } catch (System.Exception ex) {
				Util.Log.Warn($"WalkAccessControl: target read failed: {ex.Message}");
				return;
			}
			if (target == null) return;

			if (target.overrideAccess == Door.ControlState.Locked) {
				items.Add(new LabelWidget {
					Label = (string)STRINGS.ONIACCESS.ACCESS_CONTROL.LOCKED,
					GameObject = screen.gameObject,
					SpeechFunc = () => (string)STRINGS.ONIACCESS.ACCESS_CONTROL.LOCKED
				});
				return;
			}

			var building = target.GetComponent<Building>();
			bool isRotated = building != null
				&& building.Orientation != Orientation.Neutral;

			var sections = new[] {
				("standardMinionSectionHeader", "standardMinionSectionContent", false),
				("bionicMinionSectionHeader", "bionicMinionSectionContent", false),
				("robotSectionHeader", "robotSectionContent", true)
			};

			foreach (var (headerField, contentField, isRobot) in sections) {
				try {
					AddAccessSection(tv, target, isRotated, headerField, contentField,
						isRobot, items);
				} catch (System.Exception ex) {
					Util.Log.Warn(
						$"WalkAccessControl: {headerField} failed: {ex.Message}");
				}
			}
		}

		private static void AddAccessSection(
				Traverse tv, AccessControl target, bool isRotated,
				string headerField, string contentField, bool isRobot,
				List<Widget> items) {
			GameObject header;
			GameObject content;
			try {
				header = tv.Field<GameObject>(headerField).Value;
				content = tv.Field<GameObject>(contentField).Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkAccessControl: {headerField} read failed: {ex.Message}");
				return;
			}
			if (header == null || !header.activeSelf) return;

			var href = header.GetComponent<HierarchyReferences>();
			var categoryLabel = href.GetReference<LocText>("CategoryLabel");
			var headerToggleLeft = href.GetReference<MultiToggle>("ToggleLeft");
			var headerToggleRight = href.GetReference<MultiToggle>("ToggleRight");
			var collapseToggle = href.GetReference<MultiToggle>("CollapseToggle");

			string leftDir = isRotated
				? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.PASS_UP
				: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.PASS_LEFT;
			string rightDir = isRotated
				? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.PASS_DOWN
				: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.PASS_RIGHT;
			string defaultLeftLabel = isRotated
				? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.DEFAULT_PASS_UP
				: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.DEFAULT_PASS_LEFT;
			string defaultRightLabel = isRotated
				? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.DEFAULT_PASS_DOWN
				: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.DEFAULT_PASS_RIGHT;

			var children = new List<Widget>();

			var capturedHeaderLeft = headerToggleLeft;
			children.Add(new ToggleWidget {
				Label = defaultLeftLabel,
				Component = capturedHeaderLeft,
				GameObject = capturedHeaderLeft.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string state = capturedHeaderLeft.CurrentState == 0
						? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
						: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
					return $"{defaultLeftLabel}, {state}";
				}
			});

			var capturedHeaderRight = headerToggleRight;
			children.Add(new ToggleWidget {
				Label = defaultRightLabel,
				Component = capturedHeaderRight,
				GameObject = capturedHeaderRight.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string state = capturedHeaderRight.CurrentState == 0
						? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
						: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
					return $"{defaultRightLabel}, {state}";
				}
			});

			if (content != null) {
				var contentT = content.transform;
				for (int i = 0; i < contentT.childCount; i++) {
					var rowGO = contentT.GetChild(i).gameObject;
					if (!rowGO.activeSelf) continue;
					try {
						AddAccessRow(rowGO, isRotated, isRobot, leftDir, rightDir,
							children);
					} catch (System.Exception ex) {
						Util.Log.Warn(
							$"WalkAccessControl: row {i} failed: {ex.Message}");
					}
				}
			}

			var capturedCatLabel = categoryLabel;
			items.Add(new ToggleWidget {
				Label = capturedCatLabel.GetParsedText(),
				Component = collapseToggle,
				GameObject = header,
				SuppressTooltip = true,
				Children = children,
				SpeechFunc = () => {
					string catName = capturedCatLabel.GetParsedText();
					string leftState = capturedHeaderLeft.CurrentState == 0
						? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
						: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
					string rightState = capturedHeaderRight.CurrentState == 0
						? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
						: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
					return $"{catName}, {leftDir} {leftState}, {rightDir} {rightState}";
				}
			});
		}

		private static void AddAccessRow(
				GameObject rowGO, bool isRotated, bool isRobot,
				string leftDir, string rightDir,
				List<Widget> children) {
			var href = rowGO.GetComponent<HierarchyReferences>();
			if (href == null) return;

			var useDefaultBtn = href.GetReference<MultiToggle>("UseDefaultButton");
			var toggleLeft = href.GetReference<MultiToggle>("ToggleLeft");
			var toggleRight = href.GetReference<MultiToggle>("ToggleRight");
			var directionToggles = href.GetReference<RectTransform>("DirectionToggles");

			var capturedHref = href;
			var capturedRowGO = rowGO;
			children.Add(new ToggleWidget {
				Label = GetRowEntityName(href, isRobot, rowGO),
				Component = useDefaultBtn,
				GameObject = rowGO,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string name = GetRowEntityName(capturedHref, isRobot, capturedRowGO);
					string defaultState = useDefaultBtn.CurrentState == 1
						? (string)STRINGS.UI.UISIDESCREENS.ACCESS_CONTROL_SIDE_SCREEN.USING_DEFAULT
						: (string)STRINGS.UI.UISIDESCREENS.ACCESS_CONTROL_SIDE_SCREEN.USING_CUSTOM;
					return $"{name}, {defaultState}";
				}
			});

			if (directionToggles != null && directionToggles.gameObject.activeSelf) {
				children.Add(new ToggleWidget {
					Label = leftDir,
					Component = toggleLeft,
					GameObject = toggleLeft.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string state = toggleLeft.CurrentState == 0
							? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
							: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
						return $"{leftDir}, {state}";
					}
				});

				children.Add(new ToggleWidget {
					Label = rightDir,
					Component = toggleRight,
					GameObject = toggleRight.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string state = toggleRight.CurrentState == 0
							? (string)STRINGS.ONIACCESS.ACCESS_CONTROL.ALLOWED
							: (string)STRINGS.ONIACCESS.ACCESS_CONTROL.BLOCKED;
						return $"{rightDir}, {state}";
					}
				});
			}
		}

		private static string GetRowEntityName(
				HierarchyReferences href, bool isRobot, GameObject fallback) {
			if (isRobot) {
				var nameLabel = href.GetReference<LocText>("NameLabel");
				return nameLabel.GetParsedText();
			}
			var portrait = href.GetReference<CrewPortrait>("Portrait");
			var identity = portrait.identityObject;
			return identity != null ? identity.GetProperName() : fallback.name;
		}

		static void WalkOwnables(OwnablesSidescreen screen, List<Widget> items) {
			OwnablesSidescreenCategoryRow[] categoryRows;
			try {
				categoryRows = Traverse.Create(screen)
					.Field<OwnablesSidescreenCategoryRow[]>("categoryRows").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkOwnables: categoryRows read failed: {ex.Message}");
				return;
			}
			if (categoryRows == null) return;

			foreach (var categoryRow in categoryRows) {
				if (categoryRow == null || !categoryRow.gameObject.activeSelf) continue;

				OwnablesSidescreenItemRow[] itemRows;
				try {
					itemRows = Traverse.Create(categoryRow)
						.Field<OwnablesSidescreenItemRow[]>("itemRows").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkOwnables: itemRows read failed: {ex.Message}");
					continue;
				}

				var children = new List<Widget>();
				if (itemRows != null) {
					foreach (var row in itemRows) {
						if (row == null || !row.gameObject.activeSelf) continue;
						AddOwnableItemRow(row, children);
					}
				}

				var capturedCategory = categoryRow;
				items.Add(new LabelWidget {
					Label = capturedCategory.titleLabel.GetParsedText(),
					GameObject = capturedCategory.gameObject,
					SuppressTooltip = true,
					Children = children,
					SpeechFunc = () => capturedCategory.titleLabel.GetParsedText()
				});
			}
		}

		static void WalkAssignable(AssignableSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkAssignable: Traverse create failed: {ex.Message}");
				return;
			}

			// Dupe rows (currentOwnerText is always "-", skip it)
			GameObject rowGroup;
			try { rowGroup = tv.Field<GameObject>("rowGroup").Value; } catch (System.Exception ex) {
				Util.Log.Warn($"WalkAssignable: rowGroup read failed: {ex.Message}");
				return;
			}
			if (rowGroup == null) return;

			var rowGroupT = rowGroup.transform;
			for (int i = 0; i < rowGroupT.childCount; i++) {
				var child = rowGroupT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var row = child.GetComponent<AssignableSideScreenRow>();
				if (row == null) continue;
				try {
					AddAssignableRow(row, items);
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkAssignable: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		private static void AddAssignableRow(
				AssignableSideScreenRow row, List<Widget> items) {
			var capturedRow = row;
			var toggle = row.GetComponent<MultiToggle>();
			LocText assignmentText;
			try {
				assignmentText = Traverse.Create(row)
					.Field<LocText>("assignmentText").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"AddAssignableRow: assignmentText read failed: {ex.Message}");
				return;
			}
			var capturedAssignmentText = assignmentText;

			string label = row.targetIdentity != null
				? row.targetIdentity.GetProperName() : row.transform.name;

			items.Add(new ButtonWidget {
				Label = label,
				Component = toggle,
				GameObject = row.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string name = capturedRow.targetIdentity != null
						? capturedRow.targetIdentity.GetProperName()
						: capturedRow.transform.name;
					string state;
					switch (capturedRow.currentState) {
						case AssignableSideScreenRow.AssignableState.Selected:
							state = (string)STRINGS.ONIACCESS.STATES.ASSIGNED;
							break;
						case AssignableSideScreenRow.AssignableState.Unassigned:
							state = (string)STRINGS.ONIACCESS.STATES.UNASSIGNED;
							break;
						case AssignableSideScreenRow.AssignableState.Disabled:
							state = (string)STRINGS.UI.UISIDESCREENS
								.ASSIGNABLESIDESCREEN.DISABLED;
							break;
						case AssignableSideScreenRow.AssignableState.AssignedToOther:
							state = capturedAssignmentText != null
								? capturedAssignmentText.GetParsedText()
								: (string)STRINGS.ONIACCESS.STATES.UNASSIGNED;
							break;
						default:
							state = (string)STRINGS.ONIACCESS.STATES.UNASSIGNED;
							break;
					}
					return $"{name}, {state}";
				}
			});
		}

		static void WalkBionic(BionicSideScreen screen, List<Widget> items) {
			List<BionicSideScreenUpgradeSlot> slots;
			try {
				slots = Traverse.Create(screen)
					.Field<List<BionicSideScreenUpgradeSlot>>("bionicSlots").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkBionic: bionicSlots read failed: {ex.Message}");
				return;
			}
			if (slots == null) return;

			foreach (var slot in slots) {
				if (slot == null || !slot.gameObject.activeSelf) continue;
				var capturedSlot = slot;
				items.Add(new ButtonWidget {
					Label = slot.label.GetParsedText(),
					Component = slot.toggle,
					GameObject = slot.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						var upgrade = capturedSlot.upgradeSlot;
						if (upgrade.IsLocked)
							return (string)STRINGS.ONIACCESS.STATES.LOCKED;
						if (upgrade.HasUpgradeInstalled)
							return $"{upgrade.installedUpgradeComponent.GetProperName()}, {capturedSlot.label.GetParsedText()}";
						if (upgrade.HasUpgradeComponentAssigned)
							return $"{upgrade.assignedUpgradeComponent.GetProperName()}, {capturedSlot.label.GetParsedText()}";
						return capturedSlot.label.GetParsedText();
					}
				});
			}
		}

		private static void AddOwnableItemRow(
				OwnablesSidescreenItemRow row, List<Widget> children) {
			var capturedRow = row;
			children.Add(new ButtonWidget {
				Label = capturedRow.textLabel.GetParsedText(),
				Component = capturedRow.toggle,
				GameObject = capturedRow.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string text = capturedRow.textLabel.GetParsedText();
					if (capturedRow.IsLocked)
						text += $", {(string)STRINGS.ONIACCESS.STATES.LOCKED}";
					else if (capturedRow.SlotIsAssigned)
						text += $", {(string)STRINGS.ONIACCESS.STATES.ASSIGNED}";
					return text;
				}
			});
		}

		static void WalkArtableSelection(ArtableSelectionSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkArtableSelection: Traverse create failed: {ex.Message}");
				return;
			}

			Artable target;
			Dictionary<string, MultiToggle> buttons;
			try {
				target = tv.Field<Artable>("target").Value;
				buttons = tv.Field<Dictionary<string, MultiToggle>>("buttons").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkArtableSelection: field read failed: {ex.Message}");
				return;
			}
			if (target == null || buttons == null) return;

			var prefabId = target.GetComponent<KPrefabID>().PrefabID();
			var stages = Db.GetArtableStages().GetPrefabStages(prefabId);
			var stageById = new Dictionary<string, ArtableStage>();
			foreach (var stage in stages)
				stageById[stage.id] = stage;

			var capturedScreen = screen;
			foreach (var kv in buttons) {
				var mt = kv.Value;
				if (!mt.gameObject.activeSelf) continue;
				if (!stageById.TryGetValue(kv.Key, out var stage)) continue;

				var capturedMt = mt;
				var capturedStageId = kv.Key;
				var capturedStage = stage;
				items.Add(new ToggleWidget {
					Label = capturedStage.Name,
					Component = capturedMt,
					GameObject = capturedMt.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string selectedId;
						try {
							selectedId = Traverse.Create(capturedScreen)
								.Field<string>("selectedStage").Value;
						} catch (System.Exception ex) {
							Util.Log.Warn($"WalkArtableSelection: selectedStage read failed: {ex.Message}");
							selectedId = "";
						}
						bool isSelected = capturedStageId == selectedId;
						string decor = capturedStage.decor >= 0
							? $"+{capturedStage.decor}" : capturedStage.decor.ToString();
						string speech = capturedStage.Name;
						if (isSelected)
							speech += $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}";
						speech += $", {decor} {(string)STRINGS.DUPLICANTS.STATS.DECOR.NAME}";
						return speech;
					}
				});
			}

			string applyLabel = (string)STRINGS.UI.FRONTEND.GRAPHICS_OPTIONS_SCREEN.APPLYBUTTON;
			var applyBtn = screen.applyButton;
			if (applyBtn != null && applyBtn.gameObject.activeSelf) {
				items.Add(new ButtonWidget {
					Label = applyLabel,
					Component = applyBtn,
					GameObject = applyBtn.gameObject,
					SpeechFunc = () => applyLabel
				});
			}

			var clearBtn = screen.clearButton;
			if (clearBtn != null && clearBtn.gameObject.activeSelf) {
				string clearLabel = SideScreenWalker.GetButtonLabel(clearBtn, clearBtn.transform.name);
				items.Add(new ButtonWidget {
					Label = clearLabel,
					Component = clearBtn,
					GameObject = clearBtn.gameObject,
					SpeechFunc = () => SideScreenWalker.GetButtonLabel(clearBtn, clearBtn.transform.name)
				});
			}
		}

		static void WalkMonument(MonumentSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkMonument: Traverse create failed: {ex.Message}");
				return;
			}

			MonumentPart target;
			List<GameObject> buttons;
			try {
				target = tv.Field<MonumentPart>("target").Value;
				buttons = tv.Field<List<GameObject>>("buttons").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkMonument: field read failed: {ex.Message}");
				return;
			}
			if (target == null || buttons == null) return;

			var parts = Db.GetMonumentParts().GetParts(target.part);
			var capturedTarget = target;
			for (int i = 0; i < buttons.Count && i < parts.Count; i++) {
				var btnGO = buttons[i];
				if (!btnGO.activeSelf) continue;
				var kButton = btnGO.GetComponent<KButton>();
				if (kButton == null) continue;

				var capturedPart = parts[i];
				items.Add(new ButtonWidget {
					Label = capturedPart.Name,
					Component = kButton,
					GameObject = btnGO,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string chosenState;
						try {
							chosenState = Traverse.Create(capturedTarget)
								.Field<string>("chosenState").Value;
						} catch (System.Exception ex) {
							Util.Log.Warn($"WalkMonument: chosenState read failed: {ex.Message}");
							chosenState = "";
						}
						string speech = capturedPart.Name;
						if (capturedPart.Id == chosenState)
							speech += $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}";
						return speech;
					}
				});
			}

			var flipBtn = screen.flipButton;
			if (flipBtn != null && flipBtn.gameObject.activeSelf) {
				var captured = flipBtn;
				string label = SideScreenWalker.GetButtonLabel(captured, captured.transform.name);
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured,
					GameObject = captured.gameObject,
					SpeechFunc = () => SideScreenWalker.GetButtonLabel(captured, captured.transform.name)
				});
			}
		}
		static void WalkCritterSensor(CritterSensorSideScreen screen, List<Widget> items) {
			var capturedScreen = screen;
			string crittersLabel = (string)STRINGS.BUILDINGS.PREFABS
				.LOGICCRITTERCOUNTSENSOR.COUNT_CRITTER_LABEL;
			items.Add(new ToggleWidget {
				Label = crittersLabel,
				Component = screen.countCrittersToggle,
				GameObject = screen.countCrittersToggle.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string state = capturedScreen.targetSensor.countCritters
						? (string)STRINGS.ONIACCESS.STATES.ON
						: (string)STRINGS.ONIACCESS.STATES.OFF;
					return $"{crittersLabel}, {state}";
				}
			});

			string eggsLabel = (string)STRINGS.BUILDINGS.PREFABS
				.LOGICCRITTERCOUNTSENSOR.COUNT_EGG_LABEL;
			items.Add(new ToggleWidget {
				Label = eggsLabel,
				Component = screen.countEggsToggle,
				GameObject = screen.countEggsToggle.gameObject,
				SuppressTooltip = true,
				SpeechFunc = () => {
					string state = capturedScreen.targetSensor.countEggs
						? (string)STRINGS.ONIACCESS.STATES.ON
						: (string)STRINGS.ONIACCESS.STATES.OFF;
					return $"{eggsLabel}, {state}";
				}
			});
		}

		// Game direction order: N(0), NW(1), W(2), SW(3), S(4), SE(5), E(6), NE(7)
		static readonly System.Func<string>[] HEPDirLabels = new System.Func<string>[] {
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_UP,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_UP_LEFT,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN_LEFT,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN_RIGHT,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT,
			() => STRINGS.ONIACCESS.SCANNER.DIRECTION_UP_RIGHT,
		};

		static void WalkHEPDirection(
				HighEnergyParticleDirectionSideScreen screen, List<Widget> items) {
			for (int i = 0; i < screen.Buttons.Count && i < HEPDirLabels.Length; i++) {
				var btn = screen.Buttons[i];
				var capturedBtn = btn;
				var capturedLabel = HEPDirLabels[i];
				items.Add(new ToggleWidget {
					Label = capturedLabel(),
					Component = capturedBtn,
					GameObject = capturedBtn.gameObject,
					SuppressTooltip = true,
					IsInteractable = true,
					SpeechFunc = () => {
						string dirLabel = capturedLabel();
						if (!capturedBtn.isInteractable)
							return $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {dirLabel}";
						return dirLabel;
					}
				});
			}
		}

		static void WalkTelepad(TelepadSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkTelepad: Traverse create failed: {ex.Message}");
				return;
			}

			LocText timeLabel;
			KButton viewImmigrantsBtn;
			KButton viewColonySummaryBtn;
			Image newAchievementsEarned;
			KButton openRolesScreenButton;
			Image skillPointsAvailable;
			GameObject victoryConditionsContainer;
			try {
				timeLabel = tv.Field<LocText>("timeLabel").Value;
				viewImmigrantsBtn = tv.Field<KButton>("viewImmigrantsBtn").Value;
				viewColonySummaryBtn = tv.Field<KButton>("viewColonySummaryBtn").Value;
				newAchievementsEarned = tv.Field<Image>("newAchievementsEarned").Value;
				openRolesScreenButton = tv.Field<KButton>("openRolesScreenButton").Value;
				skillPointsAvailable = tv.Field<Image>("skillPointsAvailable").Value;
				victoryConditionsContainer = tv.Field<GameObject>("victoryConditionsContainer").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkTelepad: field read failed: {ex.Message}");
				return;
			}

			// timeLabel and viewImmigrantsBtn are mutually exclusive
			if (timeLabel != null && timeLabel.gameObject.activeSelf) {
				var capturedTime = timeLabel;
				items.Add(new LabelWidget {
					Label = capturedTime.GetParsedText(),
					GameObject = capturedTime.gameObject,
					SpeechFunc = () => capturedTime.GetParsedText()
				});
			}

			if (viewImmigrantsBtn != null && viewImmigrantsBtn.gameObject.activeSelf) {
				var captured = viewImmigrantsBtn;
				string label = (string)STRINGS.UI.IMMIGRANTSCREEN.IMMIGRANTSCREENTITLE;
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured,
					GameObject = captured.gameObject,
					SpeechFunc = () => label
				});
			}

			if (viewColonySummaryBtn != null && viewColonySummaryBtn.gameObject.activeSelf) {
				var captured = viewColonySummaryBtn;
				var capturedBadge = newAchievementsEarned;
				string label = (string)STRINGS.UI.UISIDESCREENS
					.TELEPADSIDESCREEN.SUMMARY_TITLE;
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured,
					GameObject = captured.gameObject,
					SpeechFunc = () => {
						string speech = label;
						if (capturedBadge != null
							&& capturedBadge.gameObject.activeSelf)
							speech += $", {(string)STRINGS.ONIACCESS.TELEPAD.NEW_ACHIEVEMENTS}";
						return speech;
					}
				});
			}

			if (openRolesScreenButton != null && openRolesScreenButton.gameObject.activeSelf) {
				var captured = openRolesScreenButton;
				var capturedBadge = skillPointsAvailable;
				string label = (string)STRINGS.UI.UISIDESCREENS
					.TELEPADSIDESCREEN.SKILLS_BUTTON;
				items.Add(new ButtonWidget {
					Label = label,
					Component = captured,
					GameObject = captured.gameObject,
					SpeechFunc = () => {
						string speech = label;
						if (capturedBadge != null
							&& capturedBadge.gameObject.activeSelf)
							speech += $", {(string)STRINGS.ONIACCESS.TELEPAD.SKILL_POINTS}";
						return speech;
					}
				});
			}

			// Victory conditions
			if (victoryConditionsContainer == null) return;
			var containerT = victoryConditionsContainer.transform;
			for (int g = 0; g < containerT.childCount; g++) {
				var groupT = containerT.GetChild(g);
				if (!groupT.gameObject.activeSelf) continue;
				var groupHref = groupT.GetComponent<HierarchyReferences>();
				if (groupHref == null) continue;

				LocText groupLabel;
				try {
					groupLabel = groupHref.GetReference<LocText>("Label");
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkTelepad: victory group reference failed: {ex.Message}");
					continue;
				}
				if (groupLabel == null) continue;

				var children = new List<Widget>();
				for (int r = 0; r < groupT.childCount; r++) {
					var rowT = groupT.GetChild(r);
					if (!rowT.gameObject.activeSelf) continue;
					var rowHref = rowT.GetComponent<HierarchyReferences>();
					if (rowHref == null) continue;

					LocText rowLabel;
					Image rowCheck;
					try {
						rowLabel = rowHref.GetReference<LocText>("Label");
						rowCheck = rowHref.GetReference<Image>("Check");
					} catch (System.Exception ex) {
						Util.Log.Warn($"WalkTelepad: victory row reference failed: {ex.Message}");
						continue;
					}
					if (rowLabel == null || rowCheck == null) continue;
					if (rowLabel == groupLabel) continue;

					var capturedLabel = rowLabel;
					var capturedCheck = rowCheck;
					children.Add(new LabelWidget {
						Label = capturedLabel.GetParsedText(),
						GameObject = rowT.gameObject,
						SpeechFunc = () => {
							string status = capturedCheck.enabled
								? (string)STRINGS.ONIACCESS.STATES.CONDITION_MET
								: (string)STRINGS.ONIACCESS.STATES.CONDITION_NOT_MET;
							return $"{status}, {capturedLabel.GetParsedText()}";
						}
					});
				}

				var capturedGroupLabel = groupLabel;
				items.Add(new LabelWidget {
					Label = capturedGroupLabel.GetParsedText(),
					GameObject = groupT.gameObject,
					SuppressTooltip = true,
					Children = children.Count > 0 ? children : null,
					SpeechFunc = () => capturedGroupLabel.GetParsedText()
				});
			}
		}

		static void WalkClusterDestination(
				ClusterDestinationSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);

			items.RemoveAll(item => item is LabelWidget
				&& item.GameObject?.GetComponent<LocText>() == screen.landingPlatformInfoLabel);

			RocketClusterDestinationSelector rocketSelector;
			try {
				rocketSelector = Traverse.Create(screen)
					.Property<RocketClusterDestinationSelector>("targetRocketSelector").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkClusterDestination: targetRocketSelector read failed: {ex.Message}");
				return;
			}
			if (rocketSelector == null) return;

			var craft = rocketSelector.GetComponent<Clustercraft>();
			var pads = LaunchPad.GetLaunchPadsForDestination(
				rocketSelector.GetDestination());

			var members = new List<SideScreenWalker.RadioMember>();
			members.Add(new SideScreenWalker.RadioMember {
				Label = (string)STRINGS.UI.UISIDESCREENS
					.CLUSTERDESTINATIONSIDESCREEN.FIRSTAVAILABLE,
				OnSelect = () => rocketSelector.SetDestinationPad(null),
				IsActive = () => rocketSelector.GetDestinationPad() == null
			});
			var excluded = new List<string>();
			foreach (var pad in pads) {
				var capturedPad = pad;
				var status = craft.CanLandAtPad(capturedPad, out string failReason);
				if (status == Clustercraft.PadLandingStatus.CanNeverLand) {
					excluded.Add($"{capturedPad.GetProperName()}: {failReason}");
					continue;
				}
				members.Add(new SideScreenWalker.RadioMember {
					Label = capturedPad.GetProperName(),
					OnSelect = () => rocketSelector.SetDestinationPad(capturedPad),
					IsActive = () => rocketSelector.GetDestinationPad() == capturedPad
				});
			}

			var openButton = screen.launchPadDropDown.openButton;
			int openButtonIndex = -1;
			for (int i = 0; i < items.Count; i++) {
				if (items[i].Component == openButton) {
					openButtonIndex = i;
					break;
				}
			}

			var platformLabel = screen.landingPlatformInfoLabel;
			string excludedSummary = excluded.Count > 0
				? ". Unavailable: " + string.Join(", ", excluded)
				: null;
			var dropdown = new DropdownWidget {
				Label = (string)STRINGS.UI.UISIDESCREENS
					.CLUSTERDESTINATIONSIDESCREEN.LANDING_PLATFORM_LABEL,
				Component = openButton,
				GameObject = screen.launchPadDropDown.gameObject,
				Tag = members,
				SpeechFunc = () => {
					string label = platformLabel.GetParsedText();
					if (excludedSummary != null)
						return label + excludedSummary;
					return label;
				}
			};

			if (openButtonIndex >= 0)
				items[openButtonIndex] = dropdown;
			else
				items.Add(dropdown);
		}

		static void WalkModuleFlightUtility(
				ModuleFlightUtilitySideScreen screen, List<Widget> items) {
			Dictionary<IEmptyableCargo, HierarchyReferences> modulePanels;
			try {
				modulePanels = Traverse.Create(screen)
					.Field<Dictionary<IEmptyableCargo, HierarchyReferences>>("modulePanels").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"ModuleFlightUtility: failed to read modulePanels: {ex.Message}");
				SideScreenWalker.WalkDefault(screen, items);
				return;
			}
			if (modulePanels == null || modulePanels.Count == 0) return;

			var nameCounts = new Dictionary<string, int>();
			foreach (var kv in modulePanels) {
				string n = kv.Key.master.gameObject.GetProperName();
				nameCounts[n] = nameCounts.ContainsKey(n) ? nameCounts[n] + 1 : 1;
			}
			var nameCounters = new Dictionary<string, int>();

			foreach (var kv in modulePanels) {
				var module = kv.Key;
				var href = kv.Value;
				if (href == null || !href.gameObject.activeSelf) continue;

				var children = new List<Widget>();

				// Action button (deploy/drill/etc.)
				var actionButton = href.GetReference<KButton>("button");
				var capturedModule = module;
				children.Add(new ButtonWidget {
					Label = module.GetButtonText,
					Component = actionButton,
					GameObject = actionButton.gameObject,
					SpeechFunc = () => capturedModule.GetButtonText
				});

				// Repeat button (auto-deploy toggle, only if supported)
				if (module.CanAutoDeploy) {
					var repeatButton = href.GetReference<KButton>("repeatButton");
					var capturedModuleRepeat = module;
					children.Add(new ToggleWidget {
						Label = (string)STRINGS.UI.UISIDESCREENS
							.MODULEFLIGHTUTILITYSIDESCREEN.REPEAT_BUTTON_TOOLTIP,
						Component = repeatButton,
						GameObject = repeatButton.gameObject,
						SpeechFunc = () => {
							string state = capturedModuleRepeat.AutoDeploy
								? (string)STRINGS.ONIACCESS.STATES.ON
								: (string)STRINGS.ONIACCESS.STATES.OFF;
							return $"{(string)STRINGS.UI.UISIDESCREENS.MODULEFLIGHTUTILITYSIDESCREEN.REPEAT_BUTTON_TOOLTIP}, {state}";
						}
					});
				}

				// Select target button (only if module supports targeting)
				if (module.CanTargetClusterGridEntities) {
					var selectTargetButton = href.GetReference<KButton>("selectTargetButton");
					var capturedSelectBtn = selectTargetButton;
					children.Add(new ButtonWidget {
						Label = (string)STRINGS.UI.UISIDESCREENS
							.MODULEFLIGHTUTILITYSIDESCREEN.SELECT_TARGET_BUTTON,
						Component = selectTargetButton,
						GameObject = selectTargetButton.gameObject,
						SpeechFunc = () => {
							var lt = capturedSelectBtn.GetComponentInChildren<LocText>();
							return lt != null ? lt.text : (string)STRINGS.UI.UISIDESCREENS
								.MODULEFLIGHTUTILITYSIDESCREEN.SELECT_TARGET_BUTTON;
						}
					});

					// Clear target button (only if a target is assigned)
					var clearTargetButton = href.GetReference<KButton>("clearTargetButton");
					var selector = module.master.GetComponent<EntityClusterDestinationSelector>();
					if (clearTargetButton != null && selector != null
							&& selector.GetClusterEntityTarget() != null) {
						children.Add(new ButtonWidget {
							Label = (string)STRINGS.UI.UISIDESCREENS
								.MODULEFLIGHTUTILITYSIDESCREEN.CLEAR_TARGET_BUTTON_TOOLTIP,
							Component = clearTargetButton,
							GameObject = clearTargetButton.gameObject,
							SpeechFunc = () => (string)STRINGS.UI.UISIDESCREENS
								.MODULEFLIGHTUTILITYSIDESCREEN.CLEAR_TARGET_BUTTON_TOOLTIP
						});
					}
				}

				// Duplicant dropdown (only if module uses crew assignment)
				if (module.ChooseDuplicant) {
					try {
						var dropDown = href.GetReference<DropDown>("dropDown");
						if (dropDown != null && dropDown.gameObject.activeSelf) {
							var capturedDropDown = dropDown;
							var capturedModuleDD = module;
							var onEntrySelected = Traverse.Create(dropDown)
								.Field<System.Action<IListableOption, object>>(
									"onEntrySelectedAction").Value;
							var radioMembers = new List<SideScreenWalker.RadioMember>();
							var capturedOnSelect = onEntrySelected;
							radioMembers.Add(new SideScreenWalker.RadioMember {
								Label = (string)STRINGS.UI.DROPDOWN.NONE,
								OnSelect = () => capturedOnSelect(
									null, capturedModuleDD),
								IsActive = () =>
									capturedModuleDD.ChosenDuplicant == null
							});
							foreach (var entry in dropDown.Entries) {
								var capturedEntry = entry;
								radioMembers.Add(new SideScreenWalker.RadioMember {
									Label = entry.GetProperName(),
									OnSelect = () => capturedOnSelect(
										capturedEntry, capturedModuleDD),
									IsActive = () =>
										capturedModuleDD.ChosenDuplicant
											== (MinionIdentity)capturedEntry
								});
							}
							children.Add(new DropdownWidget {
								Label = (string)STRINGS.UI.UISIDESCREENS
									.MODULEFLIGHTUTILITYSIDESCREEN.SELECT_DUPLICANT,
								Component = dropDown.openButton,
								GameObject = dropDown.gameObject,
								Tag = radioMembers,
								SpeechFunc = () => capturedDropDown.selectedLabel.text
							});
						}
					} catch (System.Exception ex) {
						Util.Log.Warn($"ModuleFlightUtility: failed to read dropDown: {ex.Message}");
					}
				}

				string moduleName = module.master.gameObject.GetProperName();
				if (nameCounts[moduleName] > 1) {
					nameCounters[moduleName] = nameCounters.ContainsKey(moduleName)
						? nameCounters[moduleName] + 1 : 1;
					moduleName = $"{moduleName} {nameCounters[moduleName]}";
				}
				var capturedHref = href;
				var capturedLabel = moduleName;
				items.Add(new LabelWidget {
					Label = capturedLabel,
					GameObject = capturedHref.gameObject,
					SuppressTooltip = true,
					Children = children,
					SpeechFunc = () => capturedLabel
				});
			}
		}

		static void WalkCheckboxListGroup(
				CheckboxListGroupSideScreen screen, List<Widget> items) {
			Traverse t;
			try { t = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkCheckboxListGroup: Traverse create failed: {ex.Message}");
				return;
			}

			try {
				var descriptionLabel = t.Field<LocText>("descriptionLabel").Value;
				if (descriptionLabel != null && descriptionLabel.enabled) {
					string desc = descriptionLabel.GetParsedText();
					if (!string.IsNullOrEmpty(desc)) {
						var capturedDesc = descriptionLabel;
						items.Add(new LabelWidget {
							Label = desc,
							GameObject = descriptionLabel.gameObject,
							SpeechFunc = () => capturedDesc.GetParsedText()
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkCheckboxListGroup: descriptionLabel read failed: {ex.Message}");
			}

			List<CheckboxListGroupSideScreen.CheckboxContainer> groups;
			try {
				groups = t.Field<List<CheckboxListGroupSideScreen.CheckboxContainer>>(
					"activeChecklistGroups").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkCheckboxListGroup: activeChecklistGroups read failed: {ex.Message}");
				return;
			}
			if (groups == null) return;

			foreach (var group in groups) {
				if (!group.container.gameObject.activeSelf) continue;

				LocText groupLabel;
				try {
					groupLabel = group.container.GetReference<LocText>("Text");
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkCheckboxListGroup: group label reference failed: {ex.Message}");
					continue;
				}
				if (groupLabel == null) continue;

				var children = new List<Widget>();
				foreach (var checkboxRef in group.checkboxUIItems) {
					if (!checkboxRef.gameObject.activeSelf) continue;

					LocText itemLabel;
					Image itemCheck;
					ToolTip itemTooltip;
					try {
						itemLabel = checkboxRef.GetReference<LocText>("Text");
						itemCheck = checkboxRef.GetReference<Image>("Check");
						itemTooltip = checkboxRef.GetReference<ToolTip>("Tooltip");
					} catch (System.Exception ex) {
						Util.Log.Warn($"WalkCheckboxListGroup: checkbox reference failed: {ex.Message}");
						continue;
					}
					if (itemLabel == null || itemCheck == null) continue;

					var capturedLabel = itemLabel;
					var capturedCheck = itemCheck;
					var capturedTooltip = itemTooltip;
					children.Add(new LabelWidget {
						Label = capturedLabel.GetParsedText(),
						GameObject = checkboxRef.gameObject,
						SpeechFunc = () => {
							string status = capturedCheck.enabled
								? (string)STRINGS.ONIACCESS.STATES.CONDITION_MET
								: (string)STRINGS.ONIACCESS.STATES.CONDITION_NOT_MET;
							string speech = $"{status}, {capturedLabel.GetParsedText()}";
							if (capturedTooltip != null) {
								string ttText = WidgetOps.ReadAllTooltipText(capturedTooltip);
								if (!string.IsNullOrEmpty(ttText))
									speech += $", {ttText}";
							}
							return speech;
						}
					});
				}

				var capturedGroupLabel = groupLabel;
				items.Add(new LabelWidget {
					Label = capturedGroupLabel.GetParsedText(),
					GameObject = group.container.gameObject,
					SuppressTooltip = true,
					Children = children.Count > 0 ? children : null,
					SpeechFunc = () => capturedGroupLabel.GetParsedText()
				});
			}
		}
		static void WalkRocketModule(RocketModuleSideScreen screen, List<Widget> items) {
			var reorderable = Traverse.Create(screen).Field<ReorderableBuilding>("reorderable").Value;

			// Module name
			var nameLabel = screen.moduleNameLabel;
			items.Add(new LabelWidget {
				Label = reorderable.GetProperName(),
				GameObject = nameLabel.gameObject,
				SpeechFunc = () => reorderable.GetProperName()
			});

			// Module description
			var descLabel = screen.moduleDescriptionLabel;
			string desc = reorderable.GetComponent<Building>().Desc;
			items.Add(new LabelWidget {
				Label = desc,
				GameObject = descLabel.gameObject,
				SpeechFunc = () => reorderable.GetComponent<Building>().Desc
			});

			// Add Module
			var addBtn = screen.addNewModuleButton;
			items.Add(new ButtonWidget {
				Label = SideScreenWalker.GetButtonLabel(addBtn, addBtn.transform.name),
				Component = addBtn,
				GameObject = addBtn.gameObject,
				SpeechFunc = () => SideScreenWalker.GetButtonLabel(addBtn, addBtn.transform.name)
			});

			// Change Module
			var changeBtn = screen.changeModuleButton;
			items.Add(new ButtonWidget {
				Label = SideScreenWalker.GetButtonLabel(changeBtn, changeBtn.transform.name),
				Component = changeBtn,
				GameObject = changeBtn.gameObject,
				SpeechFunc = () => SideScreenWalker.GetButtonLabel(changeBtn, changeBtn.transform.name)
			});

			// Deconstruct / Cancel Deconstruct (dynamic label via removeButtonLabel)
			var removeBtn = screen.removeModuleButton;
			var removeLt = screen.removeButtonLabel;
			items.Add(new ButtonWidget {
				Label = removeLt.GetParsedText(),
				Component = removeBtn,
				GameObject = removeBtn.gameObject,
				SpeechFunc = () => removeLt.GetParsedText()
			});

			// Move up (icon-only button, no child LocText)
			var upBtn = screen.moveModuleUpButton;
			string upLabel = (string)STRINGS.ONIACCESS.BUTTONS.MOVE_UP;
			items.Add(new ButtonWidget {
				Label = upLabel,
				Component = upBtn,
				GameObject = upBtn.gameObject,
				SpeechFunc = () => upLabel
			});

			// Move down (icon-only button, no child LocText)
			var downBtn = screen.moveModuleDownButton;
			string downLabel = (string)STRINGS.ONIACCESS.BUTTONS.MOVE_DOWN;
			items.Add(new ButtonWidget {
				Label = downLabel,
				Component = downBtn,
				GameObject = downBtn.gameObject,
				SpeechFunc = () => downLabel
			});
		}

		static void WalkDispenser(DispenserSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkDispenser: Traverse create failed: {ex.Message}");
				return;
			}

			// Dispense / Cancel button
			try {
				var dispenseButton = tv.Field<KButton>("dispenseButton").Value;
				if (dispenseButton != null && dispenseButton.gameObject.activeSelf) {
					var captured = dispenseButton;
					items.Add(new ButtonWidget {
						Label = SideScreenWalker.GetButtonLabel(captured, captured.transform.name),
						Component = captured,
						GameObject = captured.gameObject,
						SpeechFunc = () => {
							var lt = captured.GetComponentInChildren<LocText>();
							return lt != null ? lt.GetParsedText()
								: SideScreenWalker.GetButtonLabel(captured, captured.transform.name);
						}
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkDispenser: dispenseButton read failed: {ex.Message}");
			}

			// Item rows (display-only selection indicators)
			try {
				var rows = tv.Field<Dictionary<Tag, GameObject>>("rows").Value;
				if (rows == null) return;
				foreach (var kv in rows) {
					var rowGO = kv.Value;
					if (rowGO == null || !rowGO.activeSelf) continue;
					var href = rowGO.GetComponent<HierarchyReferences>();
					if (href == null) continue;
					var labelLt = href.GetReference<LocText>("Label");
					if (labelLt == null) continue;
					var capturedLt = labelLt;
					var capturedGO = rowGO;
					items.Add(new LabelWidget {
						Label = capturedLt.GetParsedText(),
						GameObject = capturedGO,
						SuppressTooltip = true,
						SpeechFunc = () => {
							var mt = capturedGO.GetComponent<MultiToggle>();
							string state = mt != null && mt.CurrentState == 0
								? (string)STRINGS.ONIACCESS.STATES.SELECTED : "";
							string name = capturedLt.GetParsedText();
							return string.IsNullOrEmpty(state) ? name : $"{state}, {name}";
						}
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkDispenser: rows read failed: {ex.Message}");
			}
		}

		static void WalkIncubator(IncubatorSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);
			SingleEntityReceptacle receptacle;
			try {
				receptacle = Traverse.Create(screen)
					.Field<SingleEntityReceptacle>("targetReceptacle").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkIncubator: targetReceptacle read failed: {ex.Message}");
				return;
			}
			if (receptacle == null) return;
			var incubator = receptacle.GetComponent<EggIncubator>();
			if (incubator == null) return;

			var toggleGO = screen.continuousToggle.gameObject;
			for (int i = 0; i < items.Count; i++) {
				if (items[i].GameObject != toggleGO) continue;
				var capturedIncubator = incubator;
				string label = (string)STRINGS.UI.CRAFT_CONTINUOUS;
				items[i] = new ToggleWidget {
					Label = label,
					Component = screen.continuousToggle,
					GameObject = toggleGO,
					SpeechFunc = () => {
						string state = capturedIncubator.autoReplaceEntity
							? (string)STRINGS.ONIACCESS.STATES.ON
							: (string)STRINGS.ONIACCESS.STATES.OFF;
						return $"{label}, {state}";
					}
				};
				break;
			}
		}

		static void WalkCounter(CounterSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);
			var toggleGO = screen.advancedModeToggle.gameObject;
			for (int i = 0; i < items.Count; i++) {
				if (items[i].GameObject != toggleGO) continue;
				string existingLabel = items[i].Label;
				var capturedScreen = screen;
				items[i] = new ToggleWidget {
					Label = existingLabel,
					Component = screen.advancedModeToggle,
					GameObject = toggleGO,
					SpeechFunc = () => {
						var labelLt = SideScreenWalker.FindChildLocText(
							capturedScreen.advancedModeToggle.transform, null);
						string lbl = SideScreenWalker.ReadLocText(
							labelLt, capturedScreen.advancedModeToggle.transform.name);
						string state = capturedScreen.targetLogicCounter.advancedMode
							? (string)STRINGS.ONIACCESS.STATES.ON
							: (string)STRINGS.ONIACCESS.STATES.OFF;
						return $"{lbl}, {state}";
					}
				};
				break;
			}
		}

		static void WalkRemoteWorkTerminal(
				RemoteWorkTerminalSidescreen screen, List<Widget> items) {
			var containerT = screen.rowContainer;
			if (containerT == null) return;

			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var href = child.GetComponent<HierarchyReferences>();
				if (href == null) continue;
				try {
					var labelLt = href.GetReference<LocText>("label");
					var mt = child.GetComponent<MultiToggle>();
					if (labelLt == null || mt == null) continue;
					var capturedLt = labelLt;
					var capturedMt = mt;
					items.Add(new ButtonWidget {
						Label = capturedLt.GetParsedText(),
						Component = capturedMt,
						GameObject = child.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => {
							string state = capturedMt.CurrentState == 1
								? $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}" : "";
							return $"{capturedLt.GetParsedText()}{state}";
						}
					});
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkRemoteWorkTerminal: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		static void WalkGeneticAnalysis(
				GeneticAnalysisStationSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkGeneticAnalysis: Traverse create failed: {ex.Message}");
				return;
			}

			try {
				var message = tv.Field<LocText>("message").Value;
				if (message != null && message.gameObject.activeSelf) {
					string text = message.GetParsedText();
					if (!string.IsNullOrEmpty(text)) {
						var capturedMsg = message;
						items.Add(new LabelWidget {
							Label = text,
							GameObject = message.gameObject,
							SpeechFunc = () => capturedMsg.GetParsedText()
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkGeneticAnalysis: message read failed: {ex.Message}");
			}

			GameObject rowContainer;
			try { rowContainer = tv.Field<GameObject>("rowContainer").Value; } catch (System.Exception ex) {
				Util.Log.Warn($"WalkGeneticAnalysis: rowContainer read failed: {ex.Message}");
				return;
			}
			if (rowContainer == null) return;

			var rowContainerT = rowContainer.transform;
			for (int i = 0; i < rowContainerT.childCount; i++) {
				var child = rowContainerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var href = child.GetComponent<HierarchyReferences>();
				if (href == null) continue;
				try {
					var labelLt = href.GetReference<LocText>("Label");
					var progressLt = href.GetReference<LocText>("ProgressLabel");
					var toggle = child.GetComponent<KToggle>();
					if (labelLt == null || toggle == null) continue;
					var capturedLabelLt = labelLt;
					var capturedProgressLt = progressLt;
					var capturedToggle = toggle;
					items.Add(new ToggleWidget {
						Label = capturedLabelLt.GetParsedText(),
						Component = capturedToggle,
						GameObject = child.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => {
							string name = capturedLabelLt.GetParsedText();
							string progress = capturedProgressLt != null
								? capturedProgressLt.GetParsedText() : "";
							return string.IsNullOrEmpty(progress)
								? name : $"{name}, {progress}";
						}
					});
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkGeneticAnalysis: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		static void WalkRelatedEntities(
				RelatedEntitiesSideScreen screen, List<Widget> items) {
			var containerT = screen.rowContainer;
			if (containerT == null) return;

			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var href = child.GetComponent<HierarchyReferences>();
				if (href == null) continue;
				try {
					var labelLt = href.GetReference<LocText>("label");
					var statusLt = href.GetReference<LocText>("status");
					var button = child.GetComponent<KButton>();
					if (labelLt == null || button == null) continue;
					var capturedLabelLt = labelLt;
					var capturedStatusLt = statusLt;
					var capturedButton = button;
					items.Add(new ButtonWidget {
						Label = STRINGS.UI.StripLinkFormatting(capturedLabelLt.text),
						Component = capturedButton,
						GameObject = child.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => {
							string name = STRINGS.UI.StripLinkFormatting(capturedLabelLt.text);
							if (capturedStatusLt != null
									&& capturedStatusLt.gameObject.activeSelf) {
								string status = capturedStatusLt.GetParsedText();
								if (!string.IsNullOrEmpty(status))
									return $"{name}, {status}";
							}
							return name;
						}
					});
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkRelatedEntities: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		static void WalkGeoTuner(GeoTunerSideScreen screen, List<Widget> items) {
			var containerT = screen.rowContainer;
			if (containerT == null) return;

			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var href = child.GetComponent<HierarchyReferences>();
				if (href == null) continue;
				try {
					var labelLt = href.GetReference<LocText>("label");
					var amountLt = href.GetReference<LocText>("amount");
					var mt = child.GetComponent<MultiToggle>();
					if (labelLt == null || mt == null) continue;
					var capturedLabelLt = labelLt;
					var capturedAmountLt = amountLt;
					var capturedMt = mt;
					items.Add(new ButtonWidget {
						Label = capturedLabelLt.GetParsedText(),
						Component = capturedMt,
						GameObject = child.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => {
							string name = capturedLabelLt.GetParsedText();
							string selected = capturedMt.CurrentState == 1
								? $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}" : "";
							string amount = "";
							if (capturedAmountLt != null
									&& capturedAmountLt.transform.parent.gameObject.activeSelf) {
								string raw = capturedAmountLt.GetParsedText();
								if (!string.IsNullOrEmpty(raw))
									amount = $", {string.Format((string)STRINGS.ONIACCESS.GEOTUNER.TUNER_COUNT, raw)}";
							}
							return $"{name}{selected}{amount}";
						}
					});
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkGeoTuner: row '{child.name}' failed: {ex.Message}");
				}
			}
		}

		static void WalkLure(LureSideScreen screen, List<Widget> items) {
			CreatureLure lure;
			try {
				lure = Traverse.Create(screen).Field<CreatureLure>("target_lure").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkLure: target_lure read failed: {ex.Message}");
				return;
			}
			if (lure == null) return;

			var container = screen.toggle_container;
			if (container == null) return;
			var containerT = container.transform;

			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var mt = child.GetComponent<MultiToggle>();
				if (mt == null) continue;

				var href = child.GetComponent<HierarchyReferences>();
				var labelLt = href != null ? href.GetReference<LocText>("Label") : null;
				var capturedLure = lure;
				var capturedMt = mt;
				var capturedGO = child.gameObject;
				var capturedLt = labelLt;

				Tag rowTag = Tag.Invalid;
				foreach (var kv in screen.toggles_by_tag) {
					if (kv.Value == mt) { rowTag = kv.Key; break; }
				}
				var capturedTag = rowTag;

				string label = capturedLt != null ? capturedLt.GetParsedText() : child.name;
				items.Add(new ToggleWidget {
					Label = label,
					Component = capturedMt,
					GameObject = capturedGO,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = capturedLt != null ? capturedLt.GetParsedText() : capturedGO.name;
						bool selected = capturedLure.activeBaitSetting == capturedTag;
						return selected
							? $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {name}"
							: name;
					}
				});
			}
		}

		static void WalkCometDetector(CometDetectorSideScreen screen, List<Widget> items) {
			var containerT = screen.rowContainer;
			if (containerT == null || containerT.childCount == 0) return;

			var members = new List<SideScreenWalker.RadioMember>();
			for (int i = 0; i < containerT.childCount; i++) {
				var child = containerT.GetChild(i);
				if (!child.gameObject.activeSelf) continue;
				var mt = child.GetComponent<MultiToggle>();
				if (mt == null) continue;
				var href = child.GetComponent<HierarchyReferences>();
				var labelLt = href != null ? href.GetReference<LocText>("label") : null;
				string label = labelLt != null ? labelLt.GetParsedText() : child.name;
				members.Add(new SideScreenWalker.RadioMember {
					Label = label,
					MultiToggleRef = mt
				});
			}
			if (members.Count == 0) return;

			string groupLabel = screen.GetTitle();
			if (string.IsNullOrEmpty(groupLabel)) groupLabel = members[0].Label;
			var radioMembers = members;
			var capturedGroupLabel = groupLabel;

			items.Add(new DropdownWidget {
				Label = groupLabel,
				Component = members[0].MultiToggleRef,
				SuppressTooltip = true,
				GameObject = containerT.gameObject,
				Tag = radioMembers,
				SpeechFunc = () => {
					string selected = null;
					for (int k = 0; k < radioMembers.Count; k++) {
						if (radioMembers[k].MultiToggleRef.CurrentState == 1) {
							selected = radioMembers[k].Label;
							break;
						}
					}
					if (selected == null) selected = radioMembers[0].Label;
					return $"{capturedGroupLabel}, {selected}";
				}
			});
		}

		static void WalkNToggle(NToggleSideScreen screen, List<Widget> items) {
			INToggleSideScreenControl target;
			List<KToggle> buttons;
			try {
				var tv = Traverse.Create(screen);
				target = tv.Field<INToggleSideScreenControl>("target").Value;
				buttons = tv.Field<List<KToggle>>("buttonList").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkNToggle: field read failed: {ex.Message}");
				return;
			}
			if (target == null || buttons == null) return;

			var capturedTarget = target;
			for (int i = 0; i < buttons.Count; i++) {
				var btn = buttons[i];
				if (btn == null || !btn.gameObject.activeSelf) continue;

				var labelLt = btn.GetComponentInChildren<LocText>();
				int capturedIdx = i;
				var capturedBtn = btn;
				var capturedLt = labelLt;

				if (capturedLt == null)
					Util.Log.Warn($"WalkNToggle: no LocText on button {btn.transform.name}, using transform name");
				string label = capturedLt != null ? capturedLt.GetParsedText() : btn.transform.name;
				items.Add(new ButtonWidget {
					Label = label,
					Component = capturedBtn,
					GameObject = capturedBtn.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string name = capturedLt != null ? capturedLt.GetParsedText() : capturedBtn.transform.name;
						bool isSelected = capturedTarget.SelectedOption == capturedIdx
							&& capturedTarget.QueuedOption == capturedIdx;
						bool isQueued = capturedTarget.QueuedOption == capturedIdx;

						if (isSelected)
							return $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {name}";
						if (isQueued)
							return $"{(string)STRINGS.ONIACCESS.STATES.QUEUED}, {name}";
						return name;
					}
				});
			}

			string desc = capturedTarget.Description;
			if (!string.IsNullOrEmpty(desc)) {
				items.Add(new LabelWidget {
					Label = desc,
					SpeechFunc = () => capturedTarget.Description
				});
			}
		}

		static void WalkLogicBitSelector(LogicBitSelectorSideScreen screen, List<Widget> items) {
			ILogicRibbonBitSelector target;
			try {
				target = Traverse.Create(screen)
					.Field<ILogicRibbonBitSelector>("target").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkLogicBitSelector: target read failed: {ex.Message}");
				return;
			}
			if (target == null) return;

			var toggles = screen.toggles_by_int;
			if (toggles == null || toggles.Count == 0) return;

			var capturedTarget = target;
			foreach (var kv in toggles) {
				int bit = kv.Key;
				var toggle = kv.Value;
				if (toggle == null || !toggle.gameObject.activeSelf) continue;

				int capturedBit = bit;
				string label = string.Format(
					(string)STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.BIT,
					bit + 1);
				items.Add(new ButtonWidget {
					Label = label,
					Component = toggle,
					GameObject = toggle.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						bool selected = capturedTarget.GetBitSelection() == capturedBit;
						bool active = capturedTarget.IsBitActive(capturedBit);
						string signalState = active
							? (string)STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_ACTIVE
							: (string)STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_INACTIVE;
						string speech = string.Format(
							(string)STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.BIT,
							capturedBit + 1);
						if (selected)
							speech += $", {(string)STRINGS.ONIACCESS.STATES.SELECTED}";
						speech += $", {signalState}";
						return speech;
					}
				});
			}
		}

		static void WalkBaseGameImpactorImperative(
				BaseGameImpactorImperativeSideScreen screen, List<Widget> items) {
			var t = Traverse.Create(screen);

			LocText healthBarLabel;
			ToolTip healthBarTooltip;
			try {
				healthBarLabel = t.Field<LocText>("healthBarLabel").Value;
				healthBarTooltip = t.Field<ToolTip>("healthBarTooltip").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkBaseGameImpactorImperative: health bar fields failed: {ex.Message}");
				healthBarLabel = null;
				healthBarTooltip = null;
			}
			if (healthBarLabel != null && healthBarTooltip != null) {
				var capturedLabel = healthBarLabel;
				var capturedTooltip = healthBarTooltip;
				items.Add(new LabelWidget {
					Label = capturedLabel.GetParsedText(),
					GameObject = capturedTooltip.gameObject,
					SpeechFunc = () => WidgetOps.ReadAllTooltipText(capturedTooltip)
				});
			}

			LocText timeBarLabel;
			ToolTip timeBarTooltip;
			try {
				timeBarLabel = t.Field<LocText>("timeBarLabel").Value;
				timeBarTooltip = t.Field<ToolTip>("timeBarTooltip").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkBaseGameImpactorImperative: time bar fields failed: {ex.Message}");
				timeBarLabel = null;
				timeBarTooltip = null;
			}
			if (timeBarLabel != null && timeBarTooltip != null) {
				var capturedLabel = timeBarLabel;
				var capturedTooltip = timeBarTooltip;
				items.Add(new LabelWidget {
					Label = capturedLabel.GetParsedText(),
					GameObject = capturedTooltip.gameObject,
					SpeechFunc = () => WidgetOps.ReadAllTooltipText(capturedTooltip)
				});
			}
		}
		static void WalkFilterSideScreen(FilterSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkFilterSideScreen: Traverse create failed: {ex.Message}");
				return;
			}

			LocText selectionLabel;
			SingleItemSelectionRow voidRow;
			SortedDictionary<Tag, SingleItemSelectionSideScreenBase.Category> cats;
			try {
				selectionLabel = screen.currentSelectionLabel;
				voidRow = tv.Field<SingleItemSelectionRow>("voidRow").Value;
				cats = tv.Field<SortedDictionary<Tag, SingleItemSelectionSideScreenBase.Category>>("categories").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkFilterSideScreen: field read failed: {ex.Message}");
				return;
			}

			// Current selection label
			if (selectionLabel != null) {
				var capturedLabel = selectionLabel;
				items.Add(new LabelWidget {
					Label = capturedLabel.GetParsedText(),
					GameObject = capturedLabel.gameObject,
					SpeechFunc = () => capturedLabel.GetParsedText()
				});
			}

			// None row
			if (voidRow != null) {
				var capturedVoidRow = voidRow;
				string noneLabel = (string)STRINGS.UI.UISIDESCREENS.FILTERSIDESCREEN.NO_SELECTION;
				items.Add(new ButtonWidget {
					Label = noneLabel,
					Component = capturedVoidRow.button,
					GameObject = capturedVoidRow.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => capturedVoidRow.IsSelected
						? $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {noneLabel}"
						: noneLabel
				});
			}

			// Unfold every category so items are activeInHierarchy. Folded
			// categories hide their entries container, which prevents clicking
			// (OnSpawn never fires so button.onClick has no handler).
			if (cats == null) return;
			foreach (var kv in cats) {
				if (kv.Key == GameTags.Void) continue;
				if (!kv.Value.IsVisible) continue;
				kv.Value.SetUnfoldedState(
					SingleItemSelectionSideScreenBase.Category.UnfoldedStates.Unfolded);
			}
			var allRows = new List<System.Tuple<Tag, SingleItemSelectionRow>>();
			foreach (var kv in cats) {
				if (kv.Key == GameTags.Void) continue;
				var cat = kv.Value;
				if (!cat.IsVisible) continue;
				List<SingleItemSelectionRow> catItems;
				try {
					catItems = Traverse.Create(cat)
						.Field<List<SingleItemSelectionRow>>("items").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkFilterSideScreen: category items read failed: {ex.Message}");
					continue;
				}
				if (catItems == null) continue;
				foreach (var row in catItems) {
					if (!row.gameObject.activeInHierarchy) continue;
					allRows.Add(new System.Tuple<Tag, SingleItemSelectionRow>(row.tag, row));
				}
			}

			allRows.Sort((a, b) => a.Item1.ProperName().CompareTo(b.Item1.ProperName()));

			foreach (var pair in allRows) {
				var capturedRow = pair.Item2;
				var capturedTag = pair.Item1;
				string label = capturedTag.ProperName();
				items.Add(new ButtonWidget {
					Label = label,
					Component = capturedRow.button,
					GameObject = capturedRow.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => capturedRow.IsSelected
						? $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {capturedTag.ProperName()}"
						: capturedTag.ProperName()
				});
			}
		}

		static void WalkSingleItemSelection(
				SingleItemSelectionSideScreen screen, List<Widget> items) {
			Traverse tv;
			try { tv = Traverse.Create(screen); } catch (System.Exception ex) {
				Util.Log.Warn($"WalkSingleItemSelection: Traverse create failed: {ex.Message}");
				return;
			}

			SingleItemSelectionSideScreen_SelectedItemSection selectedSection;
			SingleItemSelectionRow noneRow;
			SortedDictionary<Tag, SingleItemSelectionSideScreenBase.Category> cats;
			try {
				selectedSection = tv.Field<SingleItemSelectionSideScreen_SelectedItemSection>(
					"selectedItemLabel").Value;
				noneRow = tv.Field<SingleItemSelectionRow>("noneOptionRow").Value;
				cats = tv.Field<SortedDictionary<Tag, SingleItemSelectionSideScreenBase.Category>>(
					"categories").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkSingleItemSelection: field read failed: {ex.Message}");
				return;
			}

			// Current selection
			if (selectedSection != null) {
				var capturedSection = selectedSection;
				LocText contentText;
				try {
					contentText = Traverse.Create(capturedSection)
						.Field<LocText>("contentText").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkSingleItemSelection: contentText read failed: {ex.Message}");
					contentText = null;
				}
				if (contentText != null) {
					var capturedText = contentText;
					items.Add(new LabelWidget {
						Label = capturedText.GetParsedText(),
						GameObject = capturedSection.gameObject,
						SpeechFunc = () => string.Format(
							(string)STRINGS.ONIACCESS.SIDESCREENS.STORING,
							capturedText.GetParsedText())
					});
				}
			}

			// None row
			if (noneRow != null) {
				var capturedNoneRow = noneRow;
				string noneLabel = (string)STRINGS.UI.UISIDESCREENS.SINGLEITEMSELECTIONSIDESCREEN.NO_SELECTION;
				items.Add(new ButtonWidget {
					Label = noneLabel,
					Component = capturedNoneRow.button,
					GameObject = capturedNoneRow.gameObject,
					SuppressTooltip = true,
					SpeechFunc = () => capturedNoneRow.IsSelected
						? $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {noneLabel}"
						: noneLabel
				});
			}

			// Unfold every category so items are activeInHierarchy
			if (cats == null) return;
			foreach (var kv in cats) {
				if (kv.Key == GameTags.Void) continue;
				if (!kv.Value.IsVisible) continue;
				kv.Value.SetUnfoldedState(
					SingleItemSelectionSideScreenBase.Category.UnfoldedStates.Unfolded);
			}

			foreach (var kv in cats) {
				if (kv.Key == GameTags.Void) continue;
				var cat = kv.Value;
				if (!cat.IsVisible) continue;
				List<SingleItemSelectionRow> catItems;
				try {
					catItems = Traverse.Create(cat)
						.Field<List<SingleItemSelectionRow>>("items").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"WalkSingleItemSelection: category items read failed: {ex.Message}");
					continue;
				}
				if (catItems == null) continue;

				var children = new List<Widget>();
				foreach (var row in catItems) {
					if (!row.gameObject.activeInHierarchy) continue;
					var capturedRow = row;
					var capturedTag = row.tag;
					string rowLabel = capturedTag.ProperName();
					children.Add(new ButtonWidget {
						Label = rowLabel,
						Component = capturedRow.button,
						GameObject = capturedRow.gameObject,
						SuppressTooltip = true,
						SpeechFunc = () => capturedRow.IsSelected
							? $"{(string)STRINGS.ONIACCESS.STATES.SELECTED}, {capturedTag.ProperName()}"
							: capturedTag.ProperName()
					});
				}
				if (children.Count == 0) continue;

				var capturedCat = cat;
				var capturedCatTag = kv.Key;
				var capturedHref = Traverse.Create(cat)
					.Field<HierarchyReferences>("hierarchyReferences").Value;
				items.Add(new LabelWidget {
					Label = capturedCatTag.ProperName(),
					GameObject = capturedHref != null ? capturedHref.gameObject : null,
					SuppressTooltip = true,
					Children = children,
					SpeechFunc = () => {
						string name = capturedCatTag.ProperName();
						string countText = string.Format(
							(string)STRINGS.ONIACCESS.RECEPTACLE.ITEM_COUNT, children.Count);
						return $"{name}, {countText}";
					}
				});
			}
		}

		static void WalkAssignPilotAndCrew(
				AssignPilotAndCrewSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);

			var infoLabel = screen.infoLabel;
			var copilotImage = screen.copilotImage;

			for (int i = 0; i < items.Count; i++) {
				if (items[i] is LabelWidget label
					&& label.GameObject?.GetComponent<LocText>() == infoLabel) {
					label.SpeechFunc = () => {
						string text = infoLabel.GetParsedText();
						if (copilotImage.gameObject.activeSelf)
							text += ", " + (string)STRINGS.ONIACCESS.SIDESCREENS.COPILOT_ROBO;
						return text;
					};
					break;
				}
			}
		}
		static void WalkRocketRestriction(
				RocketRestrictionSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);
			if (!screen.automationControlled.activeSelf) return;

			var autoGO = screen.automationControlled;
			for (int i = 0; i < items.Count; i++) {
				if (items[i].GameObject != autoGO
					&& !items[i].GameObject.transform.IsChildOf(autoGO.transform))
					continue;
				items[i] = new LabelWidget {
					Label = (string)STRINGS.UI.UISIDESCREENS.ROCKETRESTRICTIONSIDESCREEN.AUTOMATION,
					GameObject = items[i].GameObject,
					SuppressTooltip = true,
					SpeechFunc = () => {
						string label = (string)STRINGS.UI.UISIDESCREENS.ROCKETRESTRICTIONSIDESCREEN.AUTOMATION;
						string tooltip = (string)STRINGS.UI.UISIDESCREENS.ROCKETRESTRICTIONSIDESCREEN.AUTOMATION_TOOLTIP;
						return $"{label}. {tooltip}";
					}
				};
				break;
			}
		}

		static void WalkPlanter(PlanterSideScreen screen, List<Widget> items) {
			SideScreenWalker.WalkDefault(screen, items);

			var targetReceptacle = Traverse.Create(screen)
				.Field<SingleEntityReceptacle>("targetReceptacle").Value;
			if (targetReceptacle == null) return;

			var plot = targetReceptacle as PlantablePlot;
			if (plot == null) return;

			if (plot.Occupant != null) return;
			if (plot.GetActiveRequest != null) return;

			var selectedTag = Traverse.Create(screen)
				.Field<Tag>("selectedDepositObjectTag").Value;
			if (!selectedTag.IsValid) return;

			var seedPrefab = Assets.GetPrefab(selectedTag);
			if (seedPrefab == null) return;
			var seed = seedPrefab.GetComponent<PlantableSeed>();
			if (seed == null) return;
			var previewPrefab = Assets.GetPrefab(seed.PreviewID);
			if (previewPrefab == null) return;
			var occupyArea = previewPrefab.GetComponent<OccupyArea>();
			if (occupyArea == null) return;

			var offsets = occupyArea.OccupiedCellsOffsets;
			if (offsets == null || offsets.Length <= 1) return;

			// Offsets are unrotated on the prefab; compute max extent in each axis
			int maxUp = 0, maxLeft = 0, maxRight = 0;
			foreach (var o in offsets) {
				if (o.y > maxUp) maxUp = o.y;
				if (o.x > maxRight) maxRight = o.x;
				if (o.x < 0 && -o.x > maxLeft) maxLeft = -o.x;
			}

			// Preview origin is 1 cell out from the farm tile in the growth direction,
			// so the total extent along that axis is maxUp + 1.
			// For Bottom direction, 180° rotation flips up→down and left↔right.
			int growth = maxUp + 1;
			bool bottom = plot.Direction == SingleEntityReceptacle.ReceptacleDirection.Bottom;

			var parts = new List<string>();
			if (bottom) {
				if (growth > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_DOWN, growth));
				if (maxRight > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_LEFT, maxRight));
				if (maxLeft > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_RIGHT, maxLeft));
			} else {
				if (growth > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_UP, growth));
				if (maxLeft > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_LEFT, maxLeft));
				if (maxRight > 0) parts.Add(string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EXTENT_RIGHT, maxRight));
			}
			if (parts.Count == 0) return;

			string extentDirs = string.Join(", ", parts);
			var capturedPlot = plot;

			var requestBtn = Traverse.Create(screen)
				.Field<KButton>("requestSelectedEntityBtn").Value;
			int insertIndex = -1;
			for (int i = 0; i < items.Count; i++) {
				if (items[i].Component == requestBtn) { insertIndex = i; break; }
			}
			if (insertIndex == -1 && requestBtn != null) {
				for (int i = 0; i < items.Count; i++) {
					if (items[i].GameObject == requestBtn.gameObject) { insertIndex = i; break; }
				}
			}
			if (insertIndex == -1) insertIndex = items.Count;

			items.Insert(insertIndex, new LabelWidget {
				Label = string.Format(
					(string)STRINGS.ONIACCESS.RECEPTACLE.EXTENT_CLEAR, extentDirs),
				GameObject = requestBtn?.gameObject,
				SpeechFunc = () => {
					bool blocked = !capturedPlot.ValidPlant;
					return string.Format(
						blocked
							? (string)STRINGS.ONIACCESS.RECEPTACLE.EXTENT_BLOCKED
							: (string)STRINGS.ONIACCESS.RECEPTACLE.EXTENT_CLEAR,
						extentDirs);
				}
			});
		}
		static void WalkSingleSlider(SingleSliderSideScreen screen, List<Widget> items) {
			foreach (var set in screen.sliderSets) {
				if (set.valueSlider == null) continue;
				var captured = set;
				var labelLt = set.targetLabel;
				string label = SideScreenWalker.ReadLocText(labelLt, set.valueSlider.transform.name);
				items.Add(new SliderWidget {
					Label = label,
					Component = set.valueSlider,
					GameObject = set.valueSlider.gameObject,
					SpeechFunc = () => {
						if (labelLt != null) labelLt.ForceMeshUpdate();
						string lbl = SideScreenWalker.ReadLocText(labelLt, captured.valueSlider.transform.name);
						return $"{lbl}, {WidgetOps.FormatSliderValue(captured.valueSlider)}, {(string)STRINGS.ONIACCESS.STATES.SLIDER}";
					}
				});
				if (set.numberInput == null) continue;
				var capturedInput = set.numberInput;
				var unitsLt = set.unitsLabel;
				string inputLabel = SideScreenWalker.ReadLocText(unitsLt, "value");
				items.Add(new TextInputWidget {
					Label = inputLabel,
					Component = capturedInput,
					GameObject = capturedInput.gameObject,
					SpeechFunc = () => {
						string units = SideScreenWalker.ReadLocText(unitsLt, "value");
						string val = capturedInput.field != null ? capturedInput.field.text : "";
						return $"{units}, {val}, {(string)STRINGS.ONIACCESS.STATES.INPUT_FIELD}";
					}
				});
			}
		}

		static void WalkProgressBar(ProgressBarSideScreen screen, List<Widget> items) {
			if (screen.targetObject == null) return;
			var captured = screen.targetObject;
			items.Add(new LabelWidget {
				Label = captured.GetProgressBarLabel(),
				GameObject = screen.gameObject,
				SpeechFunc = () => captured.GetProgressBarLabel()
			});
		}
		static void WalkConfigureConsumer(ConfigureConsumerSideScreen screen, List<Widget> items) {
			List<HierarchyReferences> settingToggles;
			IConfigurableConsumerOption[] settings;
			IConfigurableConsumer targetProducer;
			try {
				var tv = Traverse.Create(screen);
				settingToggles = tv.Field<List<HierarchyReferences>>("settingToggles").Value;
				settings = tv.Field<IConfigurableConsumerOption[]>("settings").Value;
				targetProducer = tv.Field<IConfigurableConsumer>("targetProducer").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"WalkConfigureConsumer: field read failed: {ex.Message}");
				return;
			}
			if (settingToggles == null || settings == null || targetProducer == null) return;

			items.Clear();

			var members = new List<SideScreenWalker.RadioMember>();
			for (int i = 0; i < settingToggles.Count && i < settings.Length; i++) {
				var href = settingToggles[i];
				var capturedSetting = settings[i];
				var capturedProducer = targetProducer;
				string label = href.GetReference<LocText>("Label").GetParsedText();
				members.Add(new SideScreenWalker.RadioMember {
					Label = label,
					MultiToggleRef = href.GetReference<MultiToggle>("Toggle"),
					IsActive = () => capturedProducer.GetSelectedOption() == capturedSetting
				});
			}
			if (members.Count == 0) return;

			string groupLabel = screen.GetTitle();
			if (string.IsNullOrEmpty(groupLabel)) groupLabel = members[0].Label;
			var radioMembers = members;
			var capturedTarget = targetProducer;

			items.Add(new DropdownWidget {
				Label = groupLabel,
				Component = members[0].MultiToggleRef,
				SuppressTooltip = true,
				GameObject = screen.gameObject,
				Tag = radioMembers,
				SpeechFunc = () => {
					var selected = capturedTarget.GetSelectedOption();
					string name = selected != null
						? selected.GetName()
						: (string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.NORECIPESELECTED;
					return $"{groupLabel}, {name}";
				}
			});

			items.Add(new LabelWidget {
				Label = "description",
				GameObject = screen.gameObject,
				SpeechFunc = () => {
					var selected = capturedTarget.GetSelectedOption();
					if (selected == null) return null;
					return WidgetOps.CleanTooltipEntry(selected.GetDetailedDescription());
				}
			});
		}
	}
}
