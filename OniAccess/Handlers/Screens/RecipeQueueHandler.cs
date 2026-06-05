using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using OniAccess.Input;
using OniAccess.Navigation;
using OniAccess.Patches;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for SelectedRecipeQueueScreen (secondary side screen opened from
	/// ComplexFabricatorSideScreen when a recipe toggle is clicked).
	/// Two-level navigation: level 0 lists recipe info, ingredient slots, queue
	/// count, infinite toggle, and confirm; level 1 shows material options within
	/// a drilled ingredient slot. Picking a material returns to the slot.
	///
	/// All labels are built from game model data (ComplexRecipe, ComplexFabricator,
	/// world inventory) rather than LocText UI components. The Harmony postfix
	/// fires before SetRecipeCategory populates the UI, and LocText.SetText vs
	/// .text property can disagree due to TMPro's m_inputSource tracking.
	/// </summary>
	public class RecipeQueueHandler: NavTreeHandler {
		private SelectedRecipeQueueScreen RecipeScreen =>
			(SelectedRecipeQueueScreen)_screen;

		private bool _pendingActivation;

		private static readonly FieldInfo _containersField = typeof(SelectedRecipeQueueScreen)
			.GetField("materialSelectionContainers", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _rowsByContainerField = typeof(SelectedRecipeQueueScreen)
			.GetField("materialSelectionRowsByContainer", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _targetField = typeof(SelectedRecipeQueueScreen)
			.GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _ownerScreenField = typeof(SelectedRecipeQueueScreen)
			.GetField("ownerScreen", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _selectedMaterialField = typeof(SelectedRecipeQueueScreen)
			.GetField("selectedMaterialOption", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _categoryIdField = typeof(SelectedRecipeQueueScreen)
			.GetField("selectedRecipeCategoryID", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _ownerSelectedToggleField = typeof(ComplexFabricatorSideScreen)
			.GetField("selectedToggle", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _ownerSelectedCategoryField = typeof(ComplexFabricatorSideScreen)
			.GetField("selectedRecipeCategory", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _ownerRecipeTogglesField = typeof(ComplexFabricatorSideScreen)
			.GetField("recipeToggles", BindingFlags.NonPublic | BindingFlags.Instance);

		static RecipeQueueHandler() {
			if (_containersField == null) Util.Log.Warn("RecipeQueueHandler: materialSelectionContainers field not found");
			if (_rowsByContainerField == null) Util.Log.Warn("RecipeQueueHandler: materialSelectionRowsByContainer field not found");
			if (_targetField == null) Util.Log.Warn("RecipeQueueHandler: target field not found");
			if (_ownerScreenField == null) Util.Log.Warn("RecipeQueueHandler: ownerScreen field not found");
			if (_selectedMaterialField == null) Util.Log.Warn("RecipeQueueHandler: selectedMaterialOption field not found");
			if (_categoryIdField == null) Util.Log.Warn("RecipeQueueHandler: selectedRecipeCategoryID field not found");
			if (_ownerSelectedToggleField == null) Util.Log.Warn("RecipeQueueHandler: ComplexFabricatorSideScreen.selectedToggle field not found");
			if (_ownerSelectedCategoryField == null) Util.Log.Warn("RecipeQueueHandler: ComplexFabricatorSideScreen.selectedRecipeCategory field not found");
			if (_ownerRecipeTogglesField == null) Util.Log.Warn("RecipeQueueHandler: ComplexFabricatorSideScreen.recipeToggles field not found");
		}

		private enum ItemKind { RecipeInfo, IngredientSlot, QueueCount, InfiniteToggle, Confirm }

		public override string DisplayName => null;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		// Search the items at the current level (level-0 rows, or a slot's materials).
		protected override SearchScope SearchScope => SearchScope.CurrentLevel;

		public RecipeQueueHandler(SelectedRecipeQueueScreen screen) : base(screen) {
			var list = new List<HelpEntry>();
			list.AddRange(DrillNavHelpEntries);
			list.Add(new HelpEntry("Left/Right", STRINGS.ONIACCESS.HELP.ADJUST_VALUE));
			list.Add(new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.CYCLE_RECIPE));
			HelpEntries = list.AsReadOnly();
		}

		// ========================================
		// ITEM STRUCTURE
		// ========================================

		private int IngredientCount {
			get {
				var recipes = GetRecipesInCategory();
				if (recipes == null || recipes.Count == 0) return 0;
				return recipes[0].ingredients.Length;
			}
		}

		private ItemKind GetItemKind(int index) {
			if (index == 0) return ItemKind.RecipeInfo;
			int ingredientCount = IngredientCount;
			if (index <= ingredientCount) return ItemKind.IngredientSlot;
			if (index == ingredientCount + 1) return ItemKind.QueueCount;
			if (index == ingredientCount + 2) return ItemKind.InfiniteToggle;
			return ItemKind.Confirm;
		}

		private int GetSlotIndex(int level0Index) {
			return level0Index - 1;
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem> {
				// Recipe info (read-only summary).
				new MenuNode(() => BuildRecipeInfoLabel()),
			};
			int slots = IngredientCount;
			for (int s = 0; s < slots; s++) {
				int slot = s;
				roots.Add(new MenuNode(
					() => BuildSlotLabel(slot),
					children: () => BuildMaterials(slot),
					activate: () => { SpeakUndiscovered(); return true; }));
			}
			roots.Add(new MenuNode(() => BuildQueueLabel()));
			roots.Add(new MenuNode(
				() => BuildInfiniteLabel(),
				activate: () => { ToggleInfinite(); return true; }));
			roots.Add(new MenuNode(
				() => (string)STRINGS.UI.CONFIRMDIALOG.OK,
				activate: () => { CloseScreen(); return true; }));
			return roots;
		}

		private IReadOnlyList<NavItem> BuildMaterials(int slotIdx) {
			var tags = GetMaterialTagsForSlot(slotIdx);
			var list = new List<NavItem>(tags.Count);
			for (int r = 0; r < tags.Count; r++) {
				int row = r;
				list.Add(new MenuNode(
					() => GetMaterialLabel(slotIdx, row),
					activate: () => { PickMaterial(slotIdx, row); return true; }));
			}
			return list;
		}

		private void PickMaterial(int slotIdx, int rowIdx) {
			ClickMaterial(slotIdx, rowIdx);
			// Auto-drill out to the ingredient slot.
			_search.Clear();
			Nav.SetPath(new[] { slotIdx + 1 });
			AnnounceCurrent();
		}

		// ========================================
		// LEFT/RIGHT OVERRIDE
		// ========================================

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (Nav.Depth == 0) {
				var kind = GetItemKind(Nav.Path[0]);
				if (kind == ItemKind.QueueCount) {
					AdjustQueueCount(direction, stepLevel);
					return;
				}
				if (direction > 0 && kind == ItemKind.IngredientSlot
					&& GetMaterialTagsForSlot(GetSlotIndex(Nav.Path[0])).Count == 0) {
					SpeakUndiscovered();
					return;
				}
			}
			base.HandleLeftRight(direction, stepLevel);
		}

		// ========================================
		// TAB: RECIPE CYCLING
		// ========================================

		protected override void NavigateTabForward() {
			CycleRecipe(1);
		}

		protected override void NavigateTabBackward() {
			CycleRecipe(-1);
		}

		// ========================================
		// ESCAPE
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				CloseScreen();
				return true;
			}
			return false;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		public override void OnActivate() {
			base.OnActivate();
			_pendingActivation = true;
		}

		public override bool Tick() {
			if (_pendingActivation) {
				_pendingActivation = false;
				AnnounceCurrent(interrupt: true);
				return false;
			}
			return base.Tick();
		}

		// ========================================
		// LABEL BUILDERS (from game model data)
		// ========================================

		private string BuildRecipeInfoLabel() {
			var recipe = GetFirstRecipe();
			if (recipe == null) return null;

			string name = recipe.GetUIName(includeAmounts: false);
			string label = TextFilter.FilterForSpeech(name);

			if (!string.IsNullOrEmpty(recipe.description))
				label += ", " + TextFilter.FilterForSpeech(recipe.description);

			string duration = (recipe.time + " "
				+ (string)STRINGS.UI.UNITSUFFIXES.SECONDS).ToLower();
			label += ", " + duration;

			// Results
			foreach (var result in recipe.results) {
				string resultName = result.facadeID.IsNullOrWhiteSpace()
					? result.material.ProperName()
					: GameTagExtensions.ProperName(result.facadeID);
				string resultAmount = GameUtil.GetFormattedByTag(
					result.material, result.amount);
				label += ", " + TextFilter.FilterForSpeech(string.Format(
					(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPEPRODUCT,
					resultName, resultAmount));
			}
			if (recipe.producedHEP > 0)
				label += ", " + TextFilter.FilterForSpeech(string.Format(
					(string)STRINGS.ITEMS.RADIATION.HIGHENERGYPARITCLE.NAME))
					+ ": " + recipe.producedHEP;

			// Radbolt cost
			var selectedRecipe = GetSelectedRecipe();
			if (selectedRecipe != null && selectedRecipe.consumedHEP > 0)
				label += ", " + GameUtil.SafeStringFormat(
					(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_RADBOLTS_REQUIRED,
					selectedRecipe.consumedHEP.ToString());

			// Warnings
			if (!recipe.IsRequiredTechUnlocked())
				label += ", " + (string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_RESEARCH_REQUIRED;
			var selectedTags = GetSelectedMaterialOption();
			if (selectedTags != null) {
				foreach (var tag in selectedTags) {
					if (!DiscoveredResources.Instance.IsDiscovered(tag)) {
						label += ", " + (string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_UNDISCOVERED_INGREDIENTS;
						break;
					}
				}
			}

			return label;
		}

		private string BuildSlotLabel(int slotIdx) {
			string header = GameUtil.SafeStringFormat(
				(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.INGREDIENT_CATEGORY,
				slotIdx + 1);
			header = TextFilter.FilterForSpeech(header);

			var selectedTags = GetSelectedMaterialOption();
			if (selectedTags != null && slotIdx < selectedTags.Count) {
				string materialName = selectedTags[slotIdx].ProperName();
				if (!string.IsNullOrEmpty(materialName))
					header += ", " + materialName;
			}

			int otherCount = GetMaterialTagsForSlot(slotIdx).Count - 1;
			if (otherCount > 0)
				header += ", " + string.Format(
					(string)STRINGS.ONIACCESS.RECIPE.OTHER_OPTIONS, otherCount);

			return header;
		}

		private string BuildQueueLabel() {
			var target = GetTarget();
			if (target == null) return null;

			var recipe = GetSelectedRecipe();
			if (recipe == null) return null;

			int count = target.GetRecipeQueueCount(recipe);
			if (count == ComplexFabricator.QUEUE_INFINITE)
				return string.Format(STRINGS.ONIACCESS.RECIPE.QUEUE_COUNT,
					(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_FOREVER);
			return string.Format(STRINGS.ONIACCESS.RECIPE.QUEUE_COUNT, count);
		}

		private string BuildInfiniteLabel() {
			var target = GetTarget();
			var recipe = GetSelectedRecipe();
			if (target == null || recipe == null) return null;

			bool isInfinite = target.GetRecipeQueueCount(recipe) == ComplexFabricator.QUEUE_INFINITE;
			return (string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_FOREVER
				+ ", " + (string)(isInfinite ? STRINGS.ONIACCESS.STATES.ON : STRINGS.ONIACCESS.STATES.OFF);
		}

		private string GetMaterialLabel(int slotIdx, int rowIdx) {
			var tags = GetMaterialTagsForSlot(slotIdx);
			if (rowIdx < 0 || rowIdx >= tags.Count) return null;

			Tag tag = tags[rowIdx];
			var target = GetTarget();
			var recipes = GetRecipesInCategory();
			if (target == null || recipes == null || recipes.Count == 0) return null;

			// Find the recipe ingredient that uses this tag at this slot
			float requiredAmount = 0;
			foreach (var recipe in recipes) {
				if (slotIdx < recipe.ingredients.Length
					&& recipe.ingredients[slotIdx].material == tag) {
					requiredAmount = recipe.ingredients[slotIdx].amount;
					break;
				}
			}

			string materialName = tag.ProperName();
			string formattedRequired = GameUtil.GetFormattedByTag(tag, requiredAmount);
			float available = target.GetMyWorld().worldInventory
				.GetAmount(tag, includeRelatedWorlds: true);
			string formattedAvailable = GameUtil.GetFormattedByTag(tag, available);

			string label = GameUtil.SafeStringFormat(
				(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_REQUIREMENT,
				materialName, formattedRequired);
			label += ", " + GameUtil.SafeStringFormat(
				(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE,
				formattedAvailable);

			var selectedTags = GetSelectedMaterialOption();
			if (selectedTags != null && slotIdx < selectedTags.Count
				&& selectedTags[slotIdx] == tag) {
				label += ", " + (string)STRINGS.ONIACCESS.STATES.SELECTED;
			}

			return TextFilter.FilterForSpeech(label);
		}

		// ========================================
		// MATERIAL TAG DISCOVERY
		// ========================================

		/// <summary>
		/// Collect discovered material tags for the given ingredient slot,
		/// mirroring RefreshIngredientDescriptors' ordering.
		/// </summary>
		private List<Tag> GetMaterialTagsForSlot(int slotIdx) {
			var result = new List<Tag>();
			var recipes = GetRecipesInCategory();
			if (recipes == null || recipes.Count == 0) return result;
			if (slotIdx < 0 || slotIdx >= recipes[0].ingredients.Length) return result;

			var seen = new HashSet<Tag>();
			foreach (var recipe in recipes) {
				if (slotIdx >= recipe.ingredients.Length) continue;
				Tag tag = recipe.ingredients[slotIdx].material;
				if (!seen.Add(tag)) continue;
				if (DiscoveredResources.Instance.IsDiscovered(tag)
					|| DebugHandler.InstantBuildMode)
					result.Add(tag);
			}
			return result;
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void AdjustQueueCount(int direction, int stepLevel) {
			var recipe = GetSelectedRecipe();
			var target = GetTarget();
			if (recipe == null || target == null) return;

			int before = target.GetRecipeQueueCount(recipe);

			int[] steps = { 1, 5, 10, 25 };
			int amount = steps[Mathf.Clamp(stepLevel, 0, steps.Length - 1)];

			for (int i = 0; i < amount; i++) {
				if (direction > 0)
					RecipeScreen.IncrementButton.onClick?.Invoke();
				else
					RecipeScreen.DecrementButton.onClick?.Invoke();
			}

			int after = target.GetRecipeQueueCount(recipe);
			bool wrapped = (before == ComplexFabricator.QUEUE_INFINITE && after != ComplexFabricator.QUEUE_INFINITE)
				|| (before != ComplexFabricator.QUEUE_INFINITE && after == ComplexFabricator.QUEUE_INFINITE);

			if (wrapped)
				PlaySound(direction > 0 ? "Slider_Boundary_High" : "Slider_Boundary_Low");
			else
				PlaySound("Slider_Move");

			SpeechPipeline.SpeakInterrupt(BuildQueueLabel());
		}

		private void ToggleInfinite() {
			RecipeScreen.InfiniteButton.SignalClick(KKeyCode.Mouse0);
			SpeechPipeline.SpeakInterrupt(BuildInfiniteLabel());
		}

		private void SpeakUndiscovered() {
			PlaySound("Slider_Boundary_Low");
			SpeechPipeline.SpeakInterrupt(
				(string)STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_UNDISCOVERED_INGREDIENTS);
		}

		private void CloseScreen() {
			ResetOwnerToggleState();
			DetailsScreenHandler.PreserveNavigationOnReactivate = true;
			HandlerStack.Pop();
			var ds = DetailsScreen.Instance;
			if (ds != null)
				ds.ClearSecondarySideScreen();
		}

		private void ClickMaterial(int slotIdx, int rowIdx) {
			var rows = GetRowsForSlot(slotIdx);
			if (rows == null) {
				Util.Log.Warn($"RecipeQueueHandler.ClickMaterial: no rows for slot {slotIdx}");
				return;
			}
			if (rowIdx < 0 || rowIdx >= rows.Count) {
				Util.Log.Warn($"RecipeQueueHandler.ClickMaterial: rowIdx {rowIdx} out of range [0,{rows.Count})");
				return;
			}

			var toggle = rows[rowIdx].GetComponent<MultiToggle>();
			if (toggle != null)
				toggle.onClick?.Invoke();
		}

		private void CycleRecipe(int direction) {
			var ownerScreen = GetOwnerScreen();
			if (ownerScreen == null) return;
			var toggles = _ownerRecipeTogglesField.GetValue(ownerScreen) as List<GameObject>;
			if (toggles == null || toggles.Count <= 1) {
				PlaySound("Slider_Boundary_Low");
				return;
			}
			SecondarySideScreenPatches.SuppressClearPop = true;
			try {
				ownerScreen.CycleRecipe(direction);
			} finally {
				SecondarySideScreenPatches.SuppressClearPop = false;
			}
		}

		private void ResetOwnerToggleState() {
			var ownerScreen = GetOwnerScreen();
			if (ownerScreen == null) return;
			var toggle = _ownerSelectedToggleField.GetValue(ownerScreen) as KToggle;
			if (toggle != null)
				toggle.isOn = false;
			_ownerSelectedToggleField.SetValue(ownerScreen, null);
			_ownerSelectedCategoryField.SetValue(ownerScreen, "");
		}

		// ========================================
		// REFLECTION ACCESSORS
		// ========================================

		private List<GameObject> GetContainers() {
			return _containersField.GetValue(RecipeScreen) as List<GameObject>;
		}

		private Dictionary<GameObject, List<GameObject>> GetRowsByContainer() {
			return _rowsByContainerField.GetValue(RecipeScreen)
				as Dictionary<GameObject, List<GameObject>>;
		}

		private List<GameObject> GetRowsForSlot(int slotIdx) {
			var containers = GetContainers();
			if (containers == null || slotIdx < 0 || slotIdx >= containers.Count)
				return null;
			var rowMap = GetRowsByContainer();
			if (rowMap == null) return null;
			rowMap.TryGetValue(containers[slotIdx], out var rows);
			return rows;
		}

		private ComplexFabricator GetTarget() {
			return _targetField.GetValue(RecipeScreen) as ComplexFabricator;
		}

		private ComplexFabricatorSideScreen GetOwnerScreen() {
			return _ownerScreenField.GetValue(RecipeScreen) as ComplexFabricatorSideScreen;
		}

		private List<Tag> GetSelectedMaterialOption() {
			return _selectedMaterialField.GetValue(RecipeScreen) as List<Tag>;
		}

		private string GetCategoryId() {
			return _categoryIdField.GetValue(RecipeScreen) as string;
		}

		private List<ComplexRecipe> GetRecipesInCategory() {
			var target = GetTarget();
			var categoryId = GetCategoryId();
			if (target == null || categoryId == null) return null;
			return target.GetRecipesWithCategoryID(categoryId);
		}

		private ComplexRecipe GetFirstRecipe() {
			var recipes = GetRecipesInCategory();
			if (recipes == null || recipes.Count == 0) return null;
			return recipes[0];
		}

		private ComplexRecipe GetSelectedRecipe() {
			var target = GetTarget();
			if (target == null) return null;
			var selectedTags = GetSelectedMaterialOption();
			if (selectedTags == null || selectedTags.Count == 0) return null;

			var categoryId = GetCategoryId();
			if (categoryId == null) return null;

			foreach (var recipe in target.GetRecipesWithCategoryID(categoryId)) {
				if (recipe.ingredients.Length != selectedTags.Count) continue;
				bool match = true;
				for (int i = 0; i < selectedTags.Count; i++) {
					if (recipe.ingredients[i].material != selectedTags[i]) {
						match = false;
						break;
					}
				}
				if (match) return recipe;
			}
			return null;
		}
	}
}
