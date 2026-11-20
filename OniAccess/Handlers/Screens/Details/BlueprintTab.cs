using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Details {
	class BlueprintTab: IDetailTab {
		public string DisplayName => (string)STRINGS.UI.DETAILTABS.COSMETICS.NAME;
		public int StartLevel => 1;
		public string GameTabId => null;

		public bool IsAvailable(GameObject target) {
			var tab = DetailsScreen.Instance?.GetTabOfType(
				DetailsScreen.SidescreenTabTypes.Blueprints);
			return tab != null && tab.IsVisible;
		}

		public void OnTabSelected() {
			var tab = DetailsScreen.Instance?.GetTabOfType(
				DetailsScreen.SidescreenTabTypes.Blueprints);
			if (tab?.tabInstance != null)
				WidgetOps.ClickMultiToggle(tab.tabInstance);
		}

		public void Populate(GameObject target, List<DetailSection> sections) {
			var panel = FindPanel();
			if (panel == null) return;

			bool isDupe = target.GetComponent<MinionIdentity>() != null;
			if (isDupe)
				PopulateDupeMode(panel, sections);
			else
				PopulateBuildingMode(panel, target, sections);
		}

		private static CosmeticsPanel FindPanel() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return null;
			return ds.GetComponentInChildren<CosmeticsPanel>(true);
		}

		// =============================================
		// BUILDING MODE
		// =============================================

		private static void PopulateBuildingMode(
				CosmeticsPanel panel, GameObject target,
				List<DetailSection> sections) {
			LocText nameLabel;
			LocText descriptionLabel;
			FacadeSelectionPanel selectionPanel;
			KButton rotateButton;
			KButton copyButton;
			GameObject vistaPanel;
			try {
				var t = Traverse.Create(panel);
				nameLabel = t.Field<LocText>("nameLabel").Value;
				descriptionLabel = t.Field<LocText>("descriptionLabel").Value;
				selectionPanel = t.Field<FacadeSelectionPanel>("selectionPanel").Value;
				rotateButton = t.Field<KButton>("rotateButton").Value;
				copyButton = t.Field<KButton>("copyButton").Value;
				vistaPanel = t.Field<GameObject>("vistaSelectionPanel").Value;
			} catch (System.Exception ex) {
				Util.Log.Error($"BlueprintTab: field access failed: {ex.Message}");
				return;
			}

			AddBuildingSection(nameLabel, descriptionLabel, selectionPanel,
				rotateButton, copyButton, target, sections);
			AddVistaSection(vistaPanel, sections);
		}

		// =============================================
		// DUPE MODE
		// =============================================

		private static void PopulateDupeMode(
				CosmeticsPanel panel, List<DetailSection> sections) {
			LocText nameLabel;
			LocText descriptionLabel;
			KButton editButton;
			KButton copyButton;
			FacadeSelectionPanel selectionPanel;
			Dictionary<ClothingOutfitUtility.OutfitType, GameObject> outfitCategories;
			ClothingOutfitUtility.OutfitType selectedCategory;
			try {
				var t = Traverse.Create(panel);
				nameLabel = t.Field<LocText>("nameLabel").Value;
				descriptionLabel = t.Field<LocText>("descriptionLabel").Value;
				editButton = t.Field<KButton>("editButton").Value;
				copyButton = t.Field<KButton>("copyButton").Value;
				selectionPanel = t.Field<FacadeSelectionPanel>("selectionPanel").Value;
				outfitCategories = t.Field<Dictionary<ClothingOutfitUtility.OutfitType, GameObject>>(
					"outfitCategories").Value;
				selectedCategory = t.Field<ClothingOutfitUtility.OutfitType>(
					"selectedOutfitCategory").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"BlueprintTab: dupe field access failed: {ex.Message}");
				return;
			}

			AddDupeInfoSection(nameLabel, descriptionLabel, editButton, copyButton,
				outfitCategories, sections);
			AddFacadeGridSection(selectionPanel, sections,
				GetCategoryName(selectedCategory));
		}

		// =============================================
		// SHARED SECTIONS
		// =============================================

		private static void AddBuildingSection(
				LocText nameLabel, LocText descriptionLabel,
				FacadeSelectionPanel selectionPanel,
				KButton rotateButton, KButton copyButton, GameObject target,
				List<DetailSection> sections) {
			var section = new DetailSection();
			section.Key = "buildingBlueprint";
			section.Header = (string)STRINGS.UI.DETAILTABS.COSMETICS.NAME;

			if (nameLabel != null) {
				var label = nameLabel;
				section.Items.Add(new LabelWidget {
					Key = label.gameObject.GetInstanceID().ToString(),
					Label = label.GetParsedText(),
					GameObject = label.gameObject,
					SpeechFunc = () => label.GetParsedText()
				});
			}

			if (descriptionLabel != null) {
				string descText = descriptionLabel.GetParsedText();
				if (!string.IsNullOrEmpty(descText)) {
					var desc = descriptionLabel;
					section.Items.Add(new LabelWidget {
						Key = desc.gameObject.GetInstanceID().ToString(),
						Label = descText,
						GameObject = desc.gameObject,
						SpeechFunc = () => desc.GetParsedText()
					});
				}
			}

			AddRotateButton(rotateButton, target, section);
			AddCopyButton(copyButton, section);
			AddFacadeToggles(selectionPanel, section);

			if (section.Items.Count > 0)
				sections.Add(section);
		}

		private static void AddDupeInfoSection(
				LocText nameLabel, LocText descriptionLabel,
				KButton editButton, KButton copyButton,
				Dictionary<ClothingOutfitUtility.OutfitType, GameObject> outfitCategories,
				List<DetailSection> sections) {
			var section = new DetailSection();
			section.Key = "dupeBlueprint";
			section.Header = (string)STRINGS.UI.DETAILTABS.COSMETICS.NAME;

			if (nameLabel != null) {
				var label = nameLabel;
				section.Items.Add(new LabelWidget {
					Key = label.gameObject.GetInstanceID().ToString(),
					Label = label.GetParsedText(),
					GameObject = label.gameObject,
					SpeechFunc = () => label.GetParsedText()
				});
			}

			// The outfit's items, one " - name" line each
			if (descriptionLabel != null) {
				var desc = descriptionLabel;
				if (!string.IsNullOrEmpty(ReadItemList(desc)))
					section.Items.Add(new LabelWidget {
						Key = desc.gameObject.GetInstanceID().ToString(),
						Label = ReadItemList(desc),
						GameObject = desc.gameObject,
						SpeechFunc = () => ReadItemList(desc)
					});
			}

			if (editButton != null && editButton.gameObject.activeSelf) {
				var btn = editButton;
				var btnLocText = btn.GetComponentInChildren<LocText>();
				string btnLabel = btnLocText != null ? btnLocText.GetParsedText() : "";
				section.Items.Add(new ButtonWidget {
					Key = "editButton",
					Component = btn,
					GameObject = btn.gameObject,
					Label = btnLabel,
					SpeechFunc = () => btnLocText != null ? btnLocText.GetParsedText() : btnLabel
				});
			}

			AddCopyButton(copyButton, section);
			AddCategoryToggles(outfitCategories, section);

			if (section.Items.Count > 0)
				sections.Add(section);
		}

		/// <summary>
		/// The outfit item list CosmeticsPanel writes, one " - name" line per
		/// item, read as a comma-separated list.
		/// </summary>
		private static string ReadItemList(LocText label) {
			string text = label.GetParsedText();
			if (string.IsNullOrEmpty(text)) return text;
			var items = new List<string>();
			foreach (var line in text.Split('\n')) {
				string item = line.Trim();
				if (item.StartsWith("-")) item = item.Substring(1).Trim();
				if (item.Length > 0) items.Add(item);
			}
			return string.Join(", ", items);
		}

		/// <summary>
		/// Copy Blueprint: arms the Paste Blueprint tool with this object as
		/// the source. The game labels it for buildings or duplicants.
		/// </summary>
		private static void AddCopyButton(KButton copyButton, DetailSection section) {
			if (copyButton == null || !copyButton.gameObject.activeSelf) return;
			var btn = copyButton;
			section.Items.Add(new ButtonWidget {
				Key = "copyBlueprint",
				Component = btn,
				GameObject = btn.gameObject,
				Label = WidgetOps.GetButtonLabel(btn),
				SpeechFunc = () => WidgetOps.GetButtonLabel(btn)
			});
		}

		/// <summary>
		/// Drywall's rotate button turns the selected wall's pattern. Nothing
		/// else on the tab changes, so the button reads the wall's current
		/// orientation, and the re-read after a press carries the new one.
		/// </summary>
		private static void AddRotateButton(KButton rotateButton, GameObject target,
				DetailSection section) {
			if (rotateButton == null || !rotateButton.gameObject.activeSelf) return;
			var btn = rotateButton;
			var rotatable = target.GetComponent<Rotatable>();
			var def = target.GetComponent<Building>().Def;
			section.Items.Add(new UserMenuButtonWidget {
				Key = "rotateBlueprint",
				GameObject = btn.gameObject,
				Label = WidgetOps.GetButtonLabel(btn),
				SpeechFunc = () => string.Format(
					(string)STRINGS.ONIACCESS.DETAILS.BLUEPRINT_ROTATION,
					WidgetOps.GetButtonLabel(btn),
					Build.BuildMenuData.GetOrientationName(
						rotatable.GetOrientation(), def)),
				OnClick = () => WidgetOps.ClickButton(btn)
			});
		}

		// =============================================
		// VISTAS
		// =============================================

		/// <summary>
		/// The Printing Pod's vista picker: "No Vista" and each vista the
		/// player owns, the chosen one marked. The description is the
		/// toggle's tooltip.
		/// </summary>
		private static void AddVistaSection(GameObject vistaPanel,
				List<DetailSection> sections) {
			if (vistaPanel == null || !vistaPanel.activeSelf) return;
			var picker = vistaPanel.GetComponentInChildren<VistaSelectionPanel>(true);
			if (picker == null) {
				Util.Log.Error("BlueprintTab: vista panel has no VistaSelectionPanel");
				return;
			}
			Dictionary<string, MultiToggle> toggles;
			try {
				toggles = Traverse.Create(picker)
					.Field<Dictionary<string, MultiToggle>>("toggles").Value;
			} catch (System.Exception ex) {
				Util.Log.Error($"BlueprintTab: vista toggles read failed: {ex.Message}");
				return;
			}
			// "No Vista" is always there once the picker has spawned
			if (toggles == null || toggles.Count == 0) {
				Util.Log.Error("BlueprintTab: vista picker has no toggles");
				return;
			}

			var section = new DetailSection {
				Key = "vistas",
				Header = (string)STRINGS.ONIACCESS.DETAILS.VISTAS
			};
			foreach (var kvp in toggles) {
				var toggle = kvp.Value;
				string name = VistaName(kvp.Key);
				section.Items.Add(new ToggleWidget {
					Key = "vista_" + kvp.Key,
					Component = toggle,
					GameObject = toggle.gameObject,
					Label = name,
					SpeechFunc = () => toggle.CurrentState == 1
						? $"{name}, {(string)STRINGS.ONIACCESS.STATES.SELECTED}"
						: name
				});
			}
			sections.Add(section);
		}

		private static string VistaName(string id) {
			foreach (var vista in VistaConfigs.VISTAS)
				if (vista.id == id) return vista.Name;
			return STRINGS.UI.DETAILTABS.COSMETICS.VISTAS.NONE.NAME;
		}

		private static void AddCategoryToggles(
				Dictionary<ClothingOutfitUtility.OutfitType, GameObject> outfitCategories,
				DetailSection section) {
			if (outfitCategories == null) return;

			foreach (var kvp in outfitCategories) {
				var catType = kvp.Key;
				var catGO = kvp.Value;
				if (catGO == null || !catGO.activeSelf) continue;

				var multiToggle = catGO.GetComponent<MultiToggle>();
				if (multiToggle == null) continue;

				string catName = GetCategoryName(catType);
				var capturedToggle = multiToggle;
				var capturedName = catName;
				section.Items.Add(new ToggleWidget {
					Key = catType.ToString(),
					Component = capturedToggle,
					GameObject = catGO,
					Label = capturedName,
					SpeechFunc = () => {
						bool selected = capturedToggle.CurrentState == 1;
						return selected
							? $"{capturedName}, {(string)STRINGS.ONIACCESS.STATES.SELECTED}"
							: capturedName;
					}
				});
			}
		}

		private static void AddFacadeGridSection(
				FacadeSelectionPanel selectionPanel, List<DetailSection> sections,
				string header) {
			if (selectionPanel == null) return;

			var section = new DetailSection();
			section.Key = "facadeGrid";
			section.Header = header;
			AddFacadeToggles(selectionPanel, section);

			if (section.Items.Count > 0)
				sections.Add(section);
		}

		private static void AddFacadeToggles(
				FacadeSelectionPanel selectionPanel, DetailSection section) {
			if (selectionPanel == null) return;

			System.Collections.IDictionary toggles;
			try {
				toggles = Traverse.Create(selectionPanel)
					.Field("activeFacadeToggles").GetValue()
					as System.Collections.IDictionary;
			} catch (System.Exception ex) {
				Util.Log.Warn($"BlueprintTab: facade toggles read failed: {ex.Message}");
				return;
			}

			if (toggles == null || toggles.Count == 0) return;

			foreach (System.Collections.DictionaryEntry entry in toggles) {
				var facadeId = (string)entry.Key;
				var facadeToggle = entry.Value;

				GameObject toggleGO;
				MultiToggle multiToggle;
				try {
					var ft = Traverse.Create(facadeToggle);
					toggleGO = ft.Property<GameObject>("gameObject").Value;
					multiToggle = ft.Property<MultiToggle>("multiToggle").Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"BlueprintTab: toggle field access failed for {facadeId}: {ex.Message}");
					continue;
				}

				if (toggleGO == null || !toggleGO.activeSelf) continue;

				string name = ReadToggleName(toggleGO);
				var capturedId = facadeId;
				var capturedPanel = selectionPanel;
				var capturedGO = toggleGO;
				section.Items.Add(new ToggleWidget {
					Key = capturedId,
					Component = multiToggle,
					GameObject = capturedGO,
					Label = name,
					SpeechFunc = () => {
						string n = ReadToggleName(capturedGO);
						bool selected = capturedId == capturedPanel.SelectedFacade;
						return selected
							? $"{n}, {(string)STRINGS.ONIACCESS.STATES.SELECTED}"
							: n;
					}
				});
			}
		}

		private static string GetCategoryName(ClothingOutfitUtility.OutfitType type) {
			switch (type) {
				case ClothingOutfitUtility.OutfitType.Clothing:
					return (string)STRINGS.UI.UISIDESCREENS.BLUEPRINT_TAB.SUBCATEGORY_OUTFIT;
				case ClothingOutfitUtility.OutfitType.AtmoSuit:
					return (string)STRINGS.UI.UISIDESCREENS.BLUEPRINT_TAB.SUBCATEGORY_ATMOSUIT;
				case ClothingOutfitUtility.OutfitType.JetSuit:
					return (string)STRINGS.UI.UISIDESCREENS.BLUEPRINT_TAB.SUBCATEGORY_JETSUIT;
				default:
					return type.ToString();
			}
		}

		/// <summary>
		/// A blueprint toggle's tooltip is its name on the first line, then its
		/// description. Read raw: the cleaned tooltip text has its line breaks
		/// already turned into sentence breaks. Filtered so the tooltip, read
		/// after it, can drop its repeat of the name.
		/// </summary>
		private static string ReadToggleName(GameObject toggleGO) {
			var tooltip = toggleGO.GetComponent<ToolTip>();
			if (tooltip != null) {
				tooltip.RebuildDynamicTooltip();
				if (tooltip.multiStringCount > 0) {
					string text = tooltip.GetMultiString(0) ?? "";
					int nl = text.IndexOf('\n');
					string first = Speech.TextFilter.FilterForSpeech(
						nl >= 0 ? text.Substring(0, nl) : text).Trim();
					if (first.Length > 0) return first;
				}
			}
			return toggleGO.name;
		}
	}
}
