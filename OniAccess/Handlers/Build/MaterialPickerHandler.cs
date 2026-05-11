using System.Collections.Generic;
using OniAccess.Speech;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Modal material picker for a single recipe ingredient slot.
	/// Lists discovered materials with available quantities.
	/// Enter selects the material and pops back to BuildInfoHandler.
	/// </summary>
	public class MaterialPickerHandler: BaseMenuHandler {
		private readonly BuildingDef _def;
		private readonly int _selectorIndex;
		private List<MaterialEntry> _materials;

		private static readonly IReadOnlyList<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("A-Z", STRINGS.ONIACCESS.HELP.TYPE_SEARCH),
			new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
			new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
			new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			new HelpEntry("Escape", STRINGS.ONIACCESS.HELP.CLOSE),
		}.AsReadOnly();

		public override IReadOnlyList<HelpEntry> HelpEntries => _helpEntries;
		public override string DisplayName => "";

		public MaterialPickerHandler(BuildingDef def, int selectorIndex) {
			_def = def;
			_selectorIndex = selectorIndex;
		}

		public override int ItemCount => _materials != null ? _materials.Count : 0;

		public override string GetItemLabel(int index) {
			if (_materials == null || index < 0 || index >= _materials.Count) return null;
			return _materials[index].Label;
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (_materials != null && CurrentIndex >= 0 && CurrentIndex < _materials.Count)
				SpeechPipeline.SpeakInterrupt(_materials[CurrentIndex].Label);
		}

		public override void OnActivate() {
			PlaySound("HUD_Click_Open");
			RebuildList();
			CurrentIndex = 0;
			_search.Clear();
			UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;

			// Position cursor on the currently selected material
			PositionOnSelected();

			if (_materials.Count > 0)
				SpeechPipeline.SpeakInterrupt(_materials[CurrentIndex].Label);
		}

		public override void OnDeactivate() {
			PlaySound("HUD_Click_Close");
			base.OnDeactivate();
		}

		protected override void ActivateCurrentItem() {
			if (_materials == null || CurrentIndex < 0 || CurrentIndex >= _materials.Count)
				return;

			var entry = _materials[CurrentIndex];
			if (!entry.Sufficient) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(_materials[CurrentIndex].Label);
				return;
			}
			SelectMaterial(entry.Tag);
			HandlerStack.Pop();
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

		private void RebuildList() {
			_materials = new List<MaterialEntry>();

			var recipe = _def.CraftRecipe;
			if (recipe == null || _selectorIndex >= recipe.Ingredients.Count)
				return;

			var selector = GetSelector();
			if (selector == null)
				return;

			var ingredient = recipe.Ingredients[_selectorIndex];
			var sufficient = new List<MaterialEntry>();
			var insufficient = new List<MaterialEntry>();

			foreach (var pair in selector.ElementToggles) {
				var tag = pair.Key;
				if (!pair.Value.gameObject.activeSelf)
					continue;

				float available = ClusterManager.Instance.activeWorld.worldInventory
					.GetAmount(tag, includeRelatedWorlds: true);
				string name = tag.ProperName();
				string quantity = GameUtil.GetFormattedMass(available);
				bool hasSufficient = available >= ingredient.amount
					|| MaterialSelector.AllowInsufficientMaterialBuild();

				string label;
				if (hasSufficient)
					label = string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.MATERIAL_ENTRY,
						name, quantity);
				else
					label = string.Format(
						(string)STRINGS.ONIACCESS.BUILD_MENU.MATERIAL_INSUFFICIENT,
						name, quantity);

				var descriptors = GameUtil.GetMaterialDescriptors(tag);
				if (descriptors.Count > 0) {
					var effects = new List<string>();
					foreach (var desc in descriptors)
						effects.Add(global::Util.StripTextFormatting(desc.text));
					label += ", " + string.Join(", ", effects.ToArray());
				}

				var entry = new MaterialEntry { Tag = tag, Label = label, Sufficient = hasSufficient };
				if (hasSufficient)
					sufficient.Add(entry);
				else
					insufficient.Add(entry);
			}

			_materials.AddRange(sufficient);
			_materials.AddRange(insufficient);
		}

		private void PositionOnSelected() {
			try {
				// Read from the target selector directly — the panel's
				// GetSelectedElementAsList getter Debug.Asserts that every active
				// selector has a CurrentSelectedElement, and ONI treats the
				// assertion as a crash.
				var selector = GetSelector();
				var currentTag = selector?.CurrentSelectedElement;
				if (currentTag == null)
					return;
				for (int i = 0; i < _materials.Count; i++) {
					if (_materials[i].Tag == currentTag) {
						CurrentIndex = i;
						break;
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Warn($"MaterialPickerHandler.PositionOnSelected: {ex.Message}");
			}
		}

		private MaterialSelector GetSelector() {
			try {
				var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;
				var field = HarmonyLib.AccessTools.Field(
					typeof(MaterialSelectionPanel), "materialSelectors");
				var selectors = (List<MaterialSelector>)field.GetValue(panel);
				if (_selectorIndex < selectors.Count)
					return selectors[_selectorIndex];
			} catch (System.Exception ex) {
				Util.Log.Error($"MaterialPickerHandler.GetSelector: {ex}");
			}
			return null;
		}

		private void SelectMaterial(Tag tag) {
			try {
				var selector = GetSelector();
				if (selector == null)
					return;

				if (!selector.ElementToggles.ContainsKey(tag)) {
					Util.Log.Warn(
						$"MaterialPickerHandler.SelectMaterial: " +
						$"'{tag}' not in ElementToggles " +
						$"(count={selector.ElementToggles.Count})");
					return;
				}

				selector.OnSelectMaterial(tag, _def.CraftRecipe, false);
			} catch (System.Exception ex) {
				Util.Log.Error($"MaterialPickerHandler.SelectMaterial: {ex}");
			}
		}


		private struct MaterialEntry {
			public Tag Tag;
			public string Label;
			public bool Sufficient;
		}
	}
}
