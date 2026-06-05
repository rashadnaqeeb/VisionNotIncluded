using System.Collections.Generic;
using System.Reflection;

using OniAccess.Handlers.Build;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	public class SelectModuleHandler: NavTreeHandler {
		private SelectModuleSideScreen ModuleScreen =>
			(SelectModuleSideScreen)_screen;

		private bool _pendingActivation;

		// Reflection fields
		private static readonly FieldInfo _selectedModuleDefField = typeof(SelectModuleSideScreen)
			.GetField("selectedModuleDef", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _moduleBuildableStateField = typeof(SelectModuleSideScreen)
			.GetField("moduleBuildableState", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _materialSelectionPanelField = typeof(SelectModuleSideScreen)
			.GetField("materialSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _facadeSelectionPanelField = typeof(SelectModuleSideScreen)
			.GetField("facadeSelectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _materialSelectorsField = typeof(MaterialSelectionPanel)
			.GetField("materialSelectors", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _activeFacadeTogglesField = typeof(FacadeSelectionPanel)
			.GetField("activeFacadeToggles", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _activeRecipeField = typeof(MaterialSelector)
			.GetField("activeRecipe", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo _activeIngredientField = typeof(MaterialSelector)
			.GetField("activeIngredient", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo _getErrorTooltipsMethod = typeof(SelectModuleSideScreen)
			.GetMethod("GetErrorTooltips", BindingFlags.NonPublic | BindingFlags.Instance);

		static SelectModuleHandler() {
			if (_selectedModuleDefField == null) Util.Log.Warn("SelectModuleHandler: selectedModuleDef field not found");
			if (_moduleBuildableStateField == null) Util.Log.Warn("SelectModuleHandler: moduleBuildableState field not found");
			if (_materialSelectionPanelField == null) Util.Log.Warn("SelectModuleHandler: materialSelectionPanel field not found");
			if (_facadeSelectionPanelField == null) Util.Log.Warn("SelectModuleHandler: facadeSelectionPanel field not found");
			if (_materialSelectorsField == null) Util.Log.Warn("SelectModuleHandler: materialSelectors field not found");
			if (_activeFacadeTogglesField == null) Util.Log.Warn("SelectModuleHandler: activeFacadeToggles field not found");
			if (_activeRecipeField == null) Util.Log.Warn("SelectModuleHandler: activeRecipe field not found");
			if (_activeIngredientField == null) Util.Log.Warn("SelectModuleHandler: activeIngredient field not found");
			if (_getErrorTooltipsMethod == null) Util.Log.Warn("SelectModuleHandler: GetErrorTooltips method not found");
		}

		private enum Section { Modules, Materials, Skin, Build }

		public override string DisplayName => null;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public SelectModuleHandler(SelectModuleSideScreen screen) : base(screen) {
			HelpEntries = new List<HelpEntry>(DrillNavHelpEntries).AsReadOnly();
			// Type-ahead searches the modules only.
			Nav.SearchFilter = n => n.RoleKey == NavRoles.Module;
		}

		// ========================================
		// DATA ACCESS
		// ========================================

		private BuildingDef GetSelectedModuleDef() {
			return _selectedModuleDefField.GetValue(ModuleScreen) as BuildingDef;
		}

		private Dictionary<BuildingDef, bool> GetBuildableState() {
			return _moduleBuildableStateField.GetValue(ModuleScreen)
				as Dictionary<BuildingDef, bool>;
		}

		private MaterialSelectionPanel GetMaterialPanel() {
			return _materialSelectionPanelField.GetValue(ModuleScreen)
				as MaterialSelectionPanel;
		}

		private FacadeSelectionPanel GetFacadePanel() {
			return _facadeSelectionPanelField.GetValue(ModuleScreen)
				as FacadeSelectionPanel;
		}

		private List<MaterialSelector> GetMaterialSelectors() {
			var panel = GetMaterialPanel();
			if (panel == null) return null;
			return _materialSelectorsField.GetValue(panel) as List<MaterialSelector>;
		}

		private string GetModuleErrorTooltip(BuildingDef def) {
			if (_getErrorTooltipsMethod == null || def == null) return "";
			var raw = _getErrorTooltipsMethod.Invoke(ModuleScreen, new object[] { def }) as string;
			if (string.IsNullOrEmpty(raw)) return "";
			return TextFilter.FilterForSpeech(raw);
		}

		private Dictionary<string, object> GetActiveFacadeToggles() {
			var panel = GetFacadePanel();
			if (panel == null) return null;
			// activeFacadeToggles is Dictionary<string, FacadeToggle> where FacadeToggle is a private struct.
			// We need to access it via reflection as a non-generic IDictionary.
			var value = _activeFacadeTogglesField.GetValue(panel);
			if (value == null) return null;
			var dict = value as System.Collections.IDictionary;
			if (dict == null) return null;
			var result = new Dictionary<string, object>();
			foreach (System.Collections.DictionaryEntry entry in dict)
				result[(string)entry.Key] = entry.Value;
			return result;
		}

		// ========================================
		// MODULE LIST
		// ========================================

		private List<BuildingDef> GetVisibleModules() {
			var result = new List<BuildingDef>();
			foreach (var id in SelectModuleSideScreen.moduleButtonSortOrder) {
				foreach (var kvp in ModuleScreen.buttons) {
					if (kvp.Key.PrefabID == id && kvp.Value.activeSelf)
						result.Add(kvp.Key);
				}
			}
			return result;
		}

		// ========================================
		// SECTION HELPERS
		// ========================================

		private List<Section> GetVisibleSections() {
			var sections = new List<Section> { Section.Modules };
			if (GetSelectedModuleDef() != null && GetMaterialPanel() != null)
				sections.Add(Section.Materials);
			var facadePanel = GetFacadePanel();
			if (facadePanel != null && facadePanel.gameObject.activeSelf) {
				var toggles = GetActiveFacadeToggles();
				if (toggles != null && toggles.Count > 0)
					sections.Add(Section.Skin);
			}
			sections.Add(Section.Build);
			return sections;
		}

		private Section GetSection(int visibleIndex) {
			var sections = GetVisibleSections();
			return (visibleIndex >= 0 && visibleIndex < sections.Count)
				? sections[visibleIndex]
				: Section.Modules;
		}

		private List<MaterialSelector> GetActiveSelectors() {
			var selectors = GetMaterialSelectors();
			if (selectors == null) return new List<MaterialSelector>();
			var result = new List<MaterialSelector>();
			foreach (var s in selectors) {
				if (s.gameObject.activeSelf)
					result.Add(s);
			}
			return result;
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var sections = GetVisibleSections();
			var roots = new List<NavItem>(sections.Count);
			foreach (var s in sections) {
				var section = s;
				switch (section) {
					case Section.Modules:
						roots.Add(new MenuNode(
							() => GetSectionLabel(Section.Modules),
							children: BuildModuleNodes));
						break;
					case Section.Materials:
						roots.Add(new MenuNode(
							() => GetSectionLabel(Section.Materials),
							children: BuildMaterialSlotNodes));
						break;
					case Section.Skin:
						roots.Add(new MenuNode(
							() => GetSectionLabel(Section.Skin),
							children: BuildFacadeNodes));
						break;
					case Section.Build:
						roots.Add(new MenuNode(
							() => GetSectionLabel(Section.Build),
							activate: () => { ClickBuild(); return true; }));
						break;
				}
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildModuleNodes() {
			var modules = GetVisibleModules();
			var list = new List<NavItem>(modules.Count);
			for (int i = 0; i < modules.Count; i++) {
				int idx = i;
				var def = modules[i];
				list.Add(new MenuNode(
					() => GetModuleLabel(idx),
					activate: () => { SelectModuleAtIndex(idx); return true; },
					roleKey: NavRoles.Module,
					searchText: () => def.Name));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildMaterialSlotNodes() {
			var selectors = GetActiveSelectors();
			var list = new List<NavItem>(selectors.Count);
			for (int i = 0; i < selectors.Count; i++) {
				int slot = i;
				list.Add(new MenuNode(
					() => GetMaterialSlotLabel(slot),
					children: () => BuildMaterialItems(slot)));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildMaterialItems(int slotIndex) {
			var materials = GetMaterialsForSlot(slotIndex);
			var list = new List<NavItem>(materials.Count);
			for (int i = 0; i < materials.Count; i++) {
				int mat = i;
				list.Add(new MenuNode(
					() => GetMaterialItemLabel(slotIndex, mat),
					activate: () => { SelectMaterialAtIndex(slotIndex, mat); return true; }));
			}
			return list;
		}

		private IReadOnlyList<NavItem> BuildFacadeNodes() {
			var keys = GetFacadeKeys();
			var list = new List<NavItem>(keys.Count);
			for (int i = 0; i < keys.Count; i++) {
				int idx = i;
				list.Add(new MenuNode(
					() => GetFacadeLabel(idx),
					activate: () => { SelectFacadeAtIndex(idx); return true; }));
			}
			return list;
		}

		/// <summary>
		/// After a selection, drop back to the section list (level 0) and announce the
		/// current section, as the old handler did with Level=0 + SpeakCurrentItem.
		/// </summary>
		private void AfterSelect() {
			int section = Nav.Path[0];
			_search.Clear();
			Nav.SetPath(new[] { section });
			AnnounceCurrent();
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
				string title = (string)STRINGS.UI.UISIDESCREENS.SELECTMODULESIDESCREEN.TITLE;
				string firstSection = Nav.Current()?.Announce();
				SpeechPipeline.SpeakInterrupt(title + ", " + firstSection);
				return false;
			}
			return base.Tick();
		}

		// ========================================
		// LABELS
		// ========================================

		private string GetSectionLabel(Section section) {
			switch (section) {
				case Section.Modules: {
						string label = (string)STRINGS.ONIACCESS.MODULE_SCREEN.MODULES;
						var selected = GetSelectedModuleDef();
						if (selected != null)
							label += ", " + selected.Name;
						return label;
					}
				case Section.Materials:
					return (string)STRINGS.ONIACCESS.MODULE_SCREEN.MATERIALS;
				case Section.Skin:
					return (string)STRINGS.ONIACCESS.MODULE_SCREEN.FACADE;
				case Section.Build: {
						string label = (string)STRINGS.UI.UISIDESCREENS.SELECTMODULESIDESCREEN.BUILDBUTTON;
						if (!ModuleScreen.buildSelectedModuleButton.isInteractable)
							label += ", " + (string)STRINGS.ONIACCESS.STATES.DISABLED;
						return label;
					}
				default:
					return null;
			}
		}

		private string GetModuleLabel(int index) {
			var modules = GetVisibleModules();
			if (index < 0 || index >= modules.Count) return null;
			var def = modules[index];
			string label = def.Name;
			var buildable = GetBuildableState();
			if (buildable != null && buildable.TryGetValue(def, out bool isBuildable) && !isBuildable) {
				label += ", " + (string)STRINGS.ONIACCESS.BUILD_MENU.NOT_BUILDABLE;
				string reasons = GetModuleErrorTooltip(def);
				if (!string.IsNullOrEmpty(reasons))
					label += ", " + reasons;
			}
			if (def == GetSelectedModuleDef())
				label += ", " + (string)STRINGS.ONIACCESS.STATES.SELECTED;
			string desc = def.Desc;
			if (!string.IsNullOrEmpty(desc))
				label += ", " + TextFilter.FilterForSpeech(desc);
			string effect = def.Effect;
			if (!string.IsNullOrEmpty(effect))
				label += ", " + TextFilter.FilterForSpeech(effect);
			return label;
		}

		private string GetMaterialSlotLabel(int slotIndex) {
			var selectors = GetActiveSelectors();
			if (slotIndex < 0 || slotIndex >= selectors.Count) return null;
			var selector = selectors[slotIndex];
			var ingredient = _activeIngredientField.GetValue(selector) as Recipe.Ingredient;
			string category = ingredient != null
				? ingredient.tag.ProperName()
				: (string)STRINGS.ONIACCESS.MODULE_SCREEN.MATERIALS;
			string label = ingredient != null
				? GameUtil.GetFormattedMass(ingredient.amount) + " " + category
				: category;
			if (selector.CurrentSelectedElement != null)
				label += ", " + selector.CurrentSelectedElement.ProperName();
			return label;
		}

		private List<Tag> GetMaterialsForSlot(int slotIndex) {
			var selectors = GetActiveSelectors();
			if (slotIndex < 0 || slotIndex >= selectors.Count) return new List<Tag>();
			var selector = selectors[slotIndex];
			var tags = new List<Tag>();
			foreach (var kvp in selector.ElementToggles) {
				if (kvp.Value.gameObject.activeSelf)
					tags.Add(kvp.Key);
			}
			return tags;
		}

		private string GetMaterialItemLabel(int slotIndex, int materialIndex) {
			var materials = GetMaterialsForSlot(slotIndex);
			if (materialIndex < 0 || materialIndex >= materials.Count) return null;
			var tag = materials[materialIndex];
			string label = tag.ProperName();
			float available = ClusterManager.Instance.activeWorld.worldInventory
				.GetAmount(tag, includeRelatedWorlds: true);
			label += ", " + GameUtil.GetFormattedByTag(tag, available);
			var selectors = GetActiveSelectors();
			if (slotIndex < selectors.Count && selectors[slotIndex].CurrentSelectedElement == tag)
				label += ", " + (string)STRINGS.ONIACCESS.STATES.SELECTED;
			return label;
		}

		private List<string> GetFacadeKeys() {
			var toggles = GetActiveFacadeToggles();
			if (toggles == null) return new List<string>();
			var keys = new List<string>(toggles.Keys);
			keys.Sort();
			// Default facade first
			if (keys.Remove(BuildMenuData.DefaultFacadeId))
				keys.Insert(0, BuildMenuData.DefaultFacadeId);
			return keys;
		}

		private string GetFacadeLabel(int index) {
			var keys = GetFacadeKeys();
			if (index < 0 || index >= keys.Count) return null;
			string id = keys[index];
			string label;
			if (id == BuildMenuData.DefaultFacadeId) {
				var selectedDef = GetSelectedModuleDef();
				label = selectedDef != null ? selectedDef.Name : id;
			} else {
				var facade = Db.GetBuildingFacades().TryGet(id);
				label = facade != null ? facade.Name : id;
			}
			var facadePanel = GetFacadePanel();
			if (facadePanel != null && facadePanel.SelectedFacade == id)
				label += ", " + (string)STRINGS.ONIACCESS.STATES.SELECTED;
			return label;
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void SelectModuleAtIndex(int index) {
			var modules = GetVisibleModules();
			if (index < 0 || index >= modules.Count) return;
			ModuleScreen.SelectModule(modules[index]);
			AfterSelect();
		}

		private void SelectMaterialAtIndex(int slotIndex, int materialIndex) {
			var materials = GetMaterialsForSlot(slotIndex);
			if (materialIndex < 0 || materialIndex >= materials.Count) return;
			var selectors = GetActiveSelectors();
			if (slotIndex < 0 || slotIndex >= selectors.Count) return;
			var selector = selectors[slotIndex];
			var recipe = _activeRecipeField.GetValue(selector) as Recipe;
			selector.OnSelectMaterial(materials[materialIndex], recipe);
			AfterSelect();
		}

		private void SelectFacadeAtIndex(int index) {
			var keys = GetFacadeKeys();
			if (index < 0 || index >= keys.Count) return;
			var facadePanel = GetFacadePanel();
			if (facadePanel == null) return;
			facadePanel.SelectedFacade = keys[index];
			AfterSelect();
		}

		private void ClickBuild() {
			if (!ModuleScreen.buildSelectedModuleButton.isInteractable) {
				PlaySound("Slider_Boundary_Low");
				string message = (string)STRINGS.ONIACCESS.STATES.DISABLED;
				string reasons = GetModuleErrorTooltip(GetSelectedModuleDef());
				if (!string.IsNullOrEmpty(reasons))
					message += ", " + reasons;
				SpeechPipeline.SpeakInterrupt(message);
				return;
			}
			Widgets.WidgetOps.ClickButton(ModuleScreen.buildSelectedModuleButton);
		}

		private void CloseScreen() {
			DetailsScreenHandler.PreserveNavigationOnReactivate = true;
			HandlerStack.Pop();
			DetailsScreen.Instance?.ClearSecondarySideScreen();
		}
	}
}
