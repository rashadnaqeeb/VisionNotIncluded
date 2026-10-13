using System.Collections.Generic;
using Database;
using Klei.AI;
using OniAccess.Speech;
using OniAccess.Widgets;
using UnityEngine;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Modal info panel for a building being placed. Shows combined
	/// description, attributes, operation requirements, operation effects,
	/// room type, facade selector, and material selectors.
	/// Enter on a material item opens MaterialPickerHandler.
	/// </summary>
	public class BuildInfoHandler: BaseMenuHandler {
		private readonly BuildingDef _def;
		private List<InfoItem> _items;

		private static readonly HashSet<Tag> _hiddenRoomTags = new HashSet<Tag> {
			RoomConstraints.ConstraintTags.Refrigerator,
			RoomConstraints.ConstraintTags.FarmStationType,
			RoomConstraints.ConstraintTags.LuxuryBedType,
			RoomConstraints.ConstraintTags.MassageTable,
			RoomConstraints.ConstraintTags.MessTable,
			RoomConstraints.ConstraintTags.NatureReserve,
			RoomConstraints.ConstraintTags.Park,
			RoomConstraints.ConstraintTags.SpiceStation,
			RoomConstraints.ConstraintTags.DeStressingBuilding,
			RoomConstraints.ConstraintTags.MachineShopType,
		};

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;
		public override string DisplayName => (string)STRINGS.ONIACCESS.BUILD_MENU.INFO_PANEL;

		public BuildInfoHandler(BuildingDef def) {
			_def = def;
		}

		public override int ItemCount => _items != null ? _items.Count : 0;

		public override string GetItemLabel(int index) {
			if (_items == null || index < 0 || index >= _items.Count) return null;
			return _items[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_items != null && CurrentIndex >= 0 && CurrentIndex < _items.Count)
				SpeechPipeline.SpeakInterrupt(ComposeItem(_items[CurrentIndex].Label, CurrentIndex));
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			RebuildItems();
			CurrentIndex = 0;
			_search.Clear();

			if (_items.Count > 0)
				SpeechPipeline.SpeakInterrupt(ComposeItem(_items[0].Label, 0));
			else
				SpeechPipeline.SpeakInterrupt(DisplayName);
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (_items == null || CurrentIndex < 0 || CurrentIndex >= _items.Count)
				return;

			var item = _items[CurrentIndex];
			if (item.SelectorIndex >= 0) {
				HandlerStack.Push(new MaterialPickerHandler(_def, item.SelectorIndex));
			} else if (item.SelectorIndex == -2) {
				HandlerStack.Push(new FacadePickerHandler(_def));
			}
		}

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				HandlerStack.Pop();
				return true;
			}
			return false;
		}

		private void RebuildItems() {
			_items = new List<InfoItem>();

			AddDescriptionItem();
			var baseAttrs = AddBaseAttributeItem();
			AddDescriptorItems();
			AddMaterialEffectsItem(baseAttrs);
			AddRoomTypeItem();
			AddFacadeItem();
			AddMaterialItems();
		}

		private void AddDescriptionItem() {
			string effect = _def.Effect;
			string desc = _def.Desc;
			string effectClean = string.IsNullOrEmpty(effect)
				? null : STRINGS.UI.StripLinkFormatting(effect);
			string descClean = string.IsNullOrEmpty(desc)
				? null : STRINGS.UI.StripLinkFormatting(desc);

			string combined;
			if (effectClean != null && descClean != null)
				combined = effectClean + " " + descClean;
			else
				combined = effectClean ?? descClean;

			if (combined != null)
				_items.Add(new InfoItem(
					string.Format(STRINGS.ONIACCESS.BUILD_MENU.DESCRIPTION_FMT, combined), -1));
		}

		private Dictionary<Klei.AI.Attribute, float> AddBaseAttributeItem() {
			var baseAttrs = new Dictionary<Klei.AI.Attribute, float>();
			try {
				foreach (var attribute in _def.attributes) {
					if (!baseAttrs.ContainsKey(attribute))
						baseAttrs[attribute] = 0f;
				}

				foreach (var modifier in _def.attributeModifiers) {
					var attr = Db.Get().BuildingAttributes.Get(modifier.AttributeId);
					float value;
					baseAttrs.TryGetValue(attr, out value);
					value += modifier.Value;
					baseAttrs[attr] = value;
				}

				if (baseAttrs.Count > 0) {
					var parts = new List<string>();
					foreach (var pair in baseAttrs)
						parts.Add(string.Format(STRINGS.ONIACCESS.BUILD_MENU.ATTR_VALUE, pair.Key.Name, pair.Value));
					_items.Add(new InfoItem(
						string.Format(STRINGS.ONIACCESS.BUILD_MENU.ATTRIBUTES_FMT,
							string.Join(", ", parts.ToArray())), -1));
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.AddBaseAttributeItem: {ex.Message}");
			}
			return baseAttrs;
		}

		private void AddMaterialEffectsItem(Dictionary<Klei.AI.Attribute, float> baseAttrs) {
			try {
				var materialMods = new Dictionary<Klei.AI.Attribute, float>();
				var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;
				if (panel.CurrentSelectedElement == null) return;

				Element element = ElementLoader.GetElement(panel.CurrentSelectedElement);
				if (element != null) {
					foreach (var modifier in element.attributeModifiers) {
						var attr = Db.Get().BuildingAttributes.Get(modifier.AttributeId);
						float value;
						materialMods.TryGetValue(attr, out value);
						value += modifier.Value;
						materialMods[attr] = value;
					}
				} else {
					var prefab = Assets.TryGetPrefab(panel.CurrentSelectedElement);
					var prefabMods = prefab.GetComponent<PrefabAttributeModifiers>();
					if (prefabMods != null) {
						foreach (var descriptor in prefabMods.descriptors) {
							var attr = Db.Get().BuildingAttributes.Get(descriptor.AttributeId);
							float value;
							materialMods.TryGetValue(attr, out value);
							value += descriptor.Value;
							materialMods[attr] = value;
						}
					}
				}

				if (materialMods.Count > 0) {
					var parts = new List<string>();
					foreach (var pair in materialMods) {
						float scaled = baseAttrs.ContainsKey(pair.Key)
							? Mathf.Abs(baseAttrs[pair.Key] * pair.Value)
							: pair.Value;
						string sign = scaled >= 0 ? "+" : "";
						parts.Add(string.Format(STRINGS.ONIACCESS.BUILD_MENU.ATTR_MODIFIER, pair.Key.Name, sign, scaled));
					}
					_items.Add(new InfoItem(
						string.Format(STRINGS.ONIACCESS.BUILD_MENU.MATERIAL_EFFECTS_FMT,
							string.Join(", ", parts.ToArray())), -1));
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.AddMaterialEffectsItem: {ex.Message}");
			}
		}

		private void AddFacadeItem() {
			try {
				if (_def.AvailableFacades == null || _def.AvailableFacades.Count == 0) return;

				var facadePanel = PlanScreen.Instance.ProductInfoScreen.FacadeSelectionPanel;
				string facadeId = facadePanel.SelectedFacade;
				string name;
				if (facadeId == BuildMenuData.DefaultFacadeId || facadeId == null) {
					name = (string)STRINGS.ONIACCESS.BUILD_MENU.FACADE_DEFAULT;
				} else {
					var resource = Db.GetBuildingFacades().TryGet(facadeId);
					name = resource != null ? resource.Name : facadeId;
				}
				_items.Add(new InfoItem(
					string.Format(STRINGS.ONIACCESS.BUILD_MENU.FACADE_FMT, name), -2));
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.AddFacadeItem: {ex.Message}");
			}
		}

		private void AddDescriptorItems() {
			try {
				var allDescriptors = GameUtil.GetAllDescriptors(_def.BuildingComplete);
				AddMergedDescriptorItem(
					(string)STRINGS.ONIACCESS.BUILD_MENU.REQUIREMENTS,
					GameUtil.GetRequirementDescriptors(allDescriptors));
				AddMergedDescriptorItem(
					(string)STRINGS.ONIACCESS.BUILD_MENU.EFFECTS,
					GameUtil.GetEffectDescriptors(allDescriptors));
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.AddDescriptorItems: {ex.Message}");
			}
		}

		private void AddMergedDescriptorItem(string prefix, List<Descriptor> descriptors) {
			if (descriptors.Count == 0) return;

			var texts = new List<string>();
			foreach (var d in descriptors) {
				string text = STRINGS.UI.StripLinkFormatting(d.text).Trim();
				if (!string.IsNullOrEmpty(text))
					texts.Add(text);
			}
			if (texts.Count == 0) return;

			_items.Add(new InfoItem(
				string.Format(STRINGS.ONIACCESS.BUILD_MENU.DESCRIPTOR_FMT,
					prefix, string.Join(", ", texts.ToArray())), -1));
		}

		private void AddRoomTypeItem() {
			try {
				var tags = _def.BuildingComplete.GetComponent<KPrefabID>().Tags;
				var roomLabels = new List<string>();
				foreach (var tag in tags) {
					if (RoomConstraints.ConstraintTags.AllTags.Contains(tag)
							&& !_hiddenRoomTags.Contains(tag)) {
						string label = RoomConstraints.ConstraintTags.GetRoomConstraintLabelText(tag);
						if (!string.IsNullOrEmpty(label))
							roomLabels.Add(label);
					}
				}
				if (roomLabels.Count > 0)
					_items.Add(new InfoItem(
						string.Format(STRINGS.ONIACCESS.BUILD_MENU.CATEGORY_FMT,
							string.Join(", ", roomLabels.ToArray())), -1));
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.AddRoomTypeItem: {ex.Message}");
			}
		}

		private void AddMaterialItems() {
			var recipe = _def.CraftRecipe;
			if (recipe == null || recipe.Ingredients == null) return;

			var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;

			for (int i = 0; i < recipe.Ingredients.Count; i++) {
				var ingredient = recipe.Ingredients[i];
				string label = BuildMaterialLabel(ingredient, panel, i);
				_items.Add(new InfoItem(label, i));
			}
		}

		private static string BuildMaterialLabel(
				Recipe.Ingredient ingredient, MaterialSelectionPanel panel, int index) {
			string categoryName = BuildMenuData.GetIngredientCategoryName(ingredient.tag);
			string selectedName = (string)STRINGS.ONIACCESS.STATES.NONE;
			string quantity = GameUtil.GetFormattedMass(ingredient.amount);
			bool insufficient = false;

			try {
				if (panel.AllSelectorsSelected()) {
					var selected = panel.GetSelectedElementAsList;
					if (index < selected.Count) {
						var tag = selected[index];
						selectedName = tag.ProperName();
						float available = ClusterManager.Instance.activeWorld.worldInventory
							.GetAmount(tag, includeRelatedWorlds: true);
						insufficient = available < ingredient.amount
							&& !MaterialSelector.AllowInsufficientMaterialBuild();
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildInfoHandler.BuildMaterialLabel: {ex.Message}");
			}

			if (insufficient)
				return string.Format(
					(string)STRINGS.ONIACCESS.BUILD_MENU.MATERIAL_SLOT_INSUFFICIENT,
					categoryName, selectedName, quantity);
			return string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.MATERIAL_SLOT,
				categoryName, selectedName, quantity);
		}


		private struct InfoItem {
			public string Label;
			public int SelectorIndex;

			public InfoItem(string label, int selectorIndex) {
				Label = label;
				SelectorIndex = selectorIndex;
			}
		}
	}
}
