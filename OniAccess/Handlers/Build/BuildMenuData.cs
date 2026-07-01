using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace OniAccess.Handlers.Build {
	/// <summary>
	/// Static helpers for querying PlanScreen categories, buildings,
	/// materials, and programmatically selecting buildings in the game.
	/// All methods re-query live game data on every call.
	/// </summary>
	public static class BuildMenuData {
		internal const string DefaultFacadeId = "DEFAULT_FACADE";
		internal static bool _selectBuildingInProgress;

		private static PropertyInfo _selectedBuildingGameObject;
		private static PropertyInfo SelectedBuildingGameObject =>
			_selectedBuildingGameObject ??= AccessTools.Property(
				typeof(PlanScreen), "SelectedBuildingGameObject");

		public struct CategoryEntry {
			public HashedString Category;
			public string DisplayName;
		}

		public struct BuildingEntry {
			public BuildingDef Def;
			public PlanScreen.RequirementsState State;
			public string Label;
		}

		public class SubcategoryGroup {
			public string Name;
			public List<BuildingEntry> Buildings;
		}

		public struct CategoryGroup {
			public HashedString Category;
			public string DisplayName;
			public List<SubcategoryGroup> Subcategories;
		}

		/// <summary>
		/// Returns visible categories from TUNING.BUILDINGS.PLANORDER.
		/// Categories with hideIfNotResearched are hidden when no building
		/// in the category has been researched, matching the sighted UI.
		/// </summary>
		public static List<CategoryEntry> GetVisibleCategories() {
			var result = new List<CategoryEntry>();
			foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
				if (!Game.IsCorrectDlcActiveForCurrentSave(planInfo))
					continue;
				if (planInfo.hideIfNotResearched && !HasAnyResearchedBuilding(planInfo))
					continue;
				string name = GetCategoryDisplayName(planInfo.category);
				result.Add(new CategoryEntry {
					Category = planInfo.category,
					DisplayName = name
				});
			}
			return result;
		}

		/// <summary>
		/// Returns buildings grouped by subcategory. Each group has the
		/// subcategory display name from STRINGS.UI.NEWBUILDCATEGORIES and
		/// a list of visible buildings.
		///
		public static List<SubcategoryGroup> GetGroupedBuildings(HashedString category) {
			var result = new List<SubcategoryGroup>();
			PlanScreen.PlanInfo? found = null;
			foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
				if (planInfo.category == category) {
					found = planInfo;
					break;
				}
			}
			if (found == null) return result;

			var planInfoVal = found.Value;
			// Preserve game ordering: iterate buildingAndSubcategoryData in order,
			// group buildings by their subcategory value.
			var groupMap = new Dictionary<string, SubcategoryGroup>();
			foreach (var kv in planInfoVal.buildingAndSubcategoryData) {
				var def = Assets.GetBuildingDef(kv.Key);
				if (def == null) continue;
				if (!def.IsAvailable() || !def.ShouldShowInBuildMenu()
					|| !Game.IsCorrectDlcActiveForCurrentSave(def)) continue;

				var state = PlanScreen.Instance.GetBuildableState(def);
				if (state == PlanScreen.RequirementsState.Invalid) continue;
				if (state == PlanScreen.RequirementsState.Tech) continue;

				string subcatKey = kv.Value;
				if (!groupMap.TryGetValue(subcatKey, out var group)) {
					group = new SubcategoryGroup {
						Name = GetSubcategoryDisplayName(subcatKey),
						Buildings = new List<BuildingEntry>()
					};
					groupMap[subcatKey] = group;
					result.Add(group);
				}
				string label = BuildLabel(def, state);
				group.Buildings.Add(new BuildingEntry { Def = def, State = state, Label = label });
			}

			return result;
		}

		/// <summary>
		/// Returns the full 3-level build tree: categories → subcategories → buildings.
		/// Same visibility filters as GetVisibleCategories and GetGroupedBuildings.
		/// Categories with no visible buildings are excluded.
		/// </summary>
		public static List<CategoryGroup> GetFullBuildTree() {
			var result = new List<CategoryGroup>();
			foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
				if (!Game.IsCorrectDlcActiveForCurrentSave(planInfo))
					continue;
				if (planInfo.hideIfNotResearched && !HasAnyResearchedBuilding(planInfo))
					continue;
				var subcategories = GetGroupedBuildings(planInfo.category);
				if (subcategories.Count == 0) continue;
				string name = GetCategoryDisplayName(planInfo.category);
				result.Add(new CategoryGroup {
					Category = planInfo.category,
					DisplayName = name,
					Subcategories = subcategories
				});
			}
			return result;
		}

		/// <summary>
		/// Programmatically select a building in PlanScreen, triggering the
		/// full game chain: ProductInfoScreen configuration, material
		/// auto-selection, and build tool activation.
		/// </summary>
		public static bool SelectBuilding(BuildingDef def, HashedString category) {
			try {
				string categoryName = HashCache.Get().Get(category);
				_selectBuildingInProgress = true;
				try {
					PlanScreen.Instance.OpenCategoryByName(categoryName);
				} finally {
					_selectBuildingInProgress = false;
				}
				if (!PlanScreen.Instance.activeCategoryBuildingToggles.TryGetValue(def, out var toggle)) {
					Util.Log.Warn($"BuildMenuData.SelectBuilding: no toggle for {def.PrefabID}");
					return false;
				}
				SelectedBuildingGameObject.SetValue(PlanScreen.Instance, null);
				PlanScreen.Instance.OnSelectBuilding(toggle.gameObject, def);
				return true;
			} catch (System.Exception ex) {
				Util.Log.Error($"BuildMenuData.SelectBuilding: {ex}");
				return false;
			}
		}

		public static bool IsUtilityBuilding(BuildingDef def) {
			return def.isKAnimTile && def.isUtility;
		}

		public static string GetOrientationName(
				Orientation orientation, BuildingDef def) {
			if (def.BuildLocationRule == BuildLocationRule.HighWattBridgeTile)
				return orientation == Orientation.R90 || orientation == Orientation.R270
					? (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_VERTICAL
					: (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_HORIZONTAL;
			if (def.BuildingComplete.GetComponent<TravelTubeBridge>() != null)
				return orientation == Orientation.Neutral
					? (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_HORIZONTAL
					: (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_VERTICAL;
			bool isHorizontalFlow = IsHorizontalFlowBuilding(def);
			bool isReverseFlow = isHorizontalFlow
				&& def.UseHighEnergyParticleInputPort
				&& def.UseHighEnergyParticleOutputPort;
			return GetOrientationName(
				orientation, def.PermittedRotations,
				isHorizontalFlow, isReverseFlow);
		}

		internal static string GetOrientationName(
				Orientation orientation, PermittedRotations permitted,
				bool horizontalFlow = false, bool reverseFlow = false) {
			switch (permitted) {
				case PermittedRotations.R90:
					return orientation == Orientation.Neutral
						? (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_VERTICAL
						: (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_HORIZONTAL;
				case PermittedRotations.FlipH:
					return orientation == Orientation.FlipH
						? (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_LEFT
						: (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT;
				case PermittedRotations.FlipV:
					return orientation == Orientation.FlipV
						? (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_DOWN
						: (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP;
				default:
					if (reverseFlow) {
						orientation = orientation switch {
							Orientation.R90 => Orientation.Neutral,
							Orientation.R180 => Orientation.R90,
							Orientation.R270 => Orientation.R180,
							_ => Orientation.R270,
						};
					} else if (horizontalFlow) {
						orientation = orientation switch {
							Orientation.R90 => Orientation.R180,
							Orientation.R180 => Orientation.R270,
							Orientation.R270 => Orientation.Neutral,
							_ => Orientation.R90,
						};
					}
					switch (orientation) {
						case Orientation.R90: return (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_RIGHT;
						case Orientation.R180: return (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_DOWN;
						case Orientation.R270: return (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_LEFT;
						default: return (string)STRINGS.ONIACCESS.BUILD_MENU.ORIENT_UP;
					}
			}
		}

		internal static string AppendOrientation(string name, string orientation,
				PermittedRotations permitted) {
			if (permitted == PermittedRotations.R90)
				return name + ", " + orientation;
			return name + ", " + string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.FACING, orientation);
		}

		/// <summary>
		/// Returns the placement offset with minimum x — the input end of a
		/// horizontal flow building at Neutral orientation.
		/// </summary>
		internal static CellOffset InputEndOffset(BuildingDef def) {
			int minX = int.MaxValue;
			CellOffset result = default;
			foreach (var offset in def.PlacementOffsets) {
				if (offset.x < minX) {
					minX = offset.x;
					result = offset;
				}
			}
			return result;
		}

		internal static bool IsHorizontalFlowBuilding(BuildingDef def) {
			if (def.ObjectLayer == ObjectLayer.LogicGate)
				return true;
			if (def.WidthInCells <= def.HeightInCells)
				return false;
			if (def.InputConduitType != ConduitType.None || def.OutputConduitType != ConduitType.None)
				return true;
			if (def.BuildLocationRule == BuildLocationRule.WireBridge
				|| def.BuildLocationRule == BuildLocationRule.HighWattBridgeTile)
				return true;
			if (def.UseHighEnergyParticleInputPort && def.UseHighEnergyParticleOutputPort)
				return true;
			return false;
		}

		/// <summary>
		/// Builds a brief material summary for the building's auto-selected
		/// materials. Returns e.g. "copper, 25 kg".
		/// </summary>
		public static string GetMaterialSummary(BuildingDef def) {
			try {
				var panel = PlanScreen.Instance.ProductInfoScreen.materialSelectionPanel;
				var firstTag = panel.CurrentSelectedElement;
				if (firstTag == null) return null;
				var element = ElementLoader.GetElement(firstTag);
				string name = element != null ? element.name : firstTag.ProperName();
				float mass = def.Mass != null && def.Mass.Length > 0 ? def.Mass[0] : 0f;
				return $"{name}, {GameUtil.GetFormattedMass(mass)}";
			} catch (System.Exception ex) {
				Util.Log.Warn($"BuildMenuData.GetMaterialSummary: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Returns the building name and facing direction (no material).
		/// Used for the immediate interrupt announcement; material is queued
		/// separately so the selection panel has time to initialize.
		/// </summary>
		public static string BuildNameAnnouncement(BuildingDef def) {
			string name = def.Name;
			if (def.PermittedRotations == PermittedRotations.Unrotatable)
				return name;
			string dir = GetOrientationName(GetCurrentOrientation(), def);
			return AppendOrientation(name, dir, def.PermittedRotations);
		}

		/// <summary>
		/// Reads the current build tool orientation from the visualizer.
		/// </summary>
		public static Orientation GetCurrentOrientation() {
			if (BuildTool.Instance != null && BuildTool.Instance.visualizer != null) {
				var rot = BuildTool.Instance.visualizer.GetComponent<Rotatable>();
				if (rot != null) return rot.GetOrientation();
			}
			return Orientation.Neutral;
		}

		private static bool HasAnyResearchedBuilding(PlanScreen.PlanInfo planInfo) {
			foreach (var kv in planInfo.buildingAndSubcategoryData) {
				var def = Assets.GetBuildingDef(kv.Key);
				if (def == null) continue;
				if (!def.IsAvailable() || !def.ShouldShowInBuildMenu()
					|| !Game.IsCorrectDlcActiveForCurrentSave(def)) continue;
				var state = PlanScreen.Instance.GetBuildableState(def);
				if (state != PlanScreen.RequirementsState.Tech)
					return true;
			}
			return false;
		}

		private static string GetSubcategoryDisplayName(string subcategoryKey) {
			StringEntry entry;
			if (Strings.TryGet("STRINGS.UI.NEWBUILDCATEGORIES." + subcategoryKey.ToUpper() + ".BUILDMENUTITLE", out entry))
				return entry.String;
			return subcategoryKey;
		}

		private static string GetCategoryDisplayName(HashedString category) {
			string text = HashCache.Get().Get(category).ToUpper();
			string name = Strings.Get("STRINGS.UI.BUILDCATEGORIES." + text + ".NAME");
			return STRINGS.UI.StripLinkFormatting(name);
		}

		private static string BuildLabel(BuildingDef def, PlanScreen.RequirementsState state) {
			string name = def.Name;
			if (!IsUtilityBuilding(def))
				name += ", " + def.WidthInCells + "x" + def.HeightInCells;

			string cost = state != PlanScreen.RequirementsState.Materials
				? FormatCost(def) : null;
			string effect = STRINGS.UI.StripLinkFormatting(def.Effect);

			var sb = new System.Text.StringBuilder(name);
			if (cost != null)
				sb.Append(", ").Append(cost);
			if (!string.IsNullOrEmpty(effect))
				sb.Append(", ").Append(effect);

			if (state == PlanScreen.RequirementsState.Complete)
				return sb.ToString();
			if (state == PlanScreen.RequirementsState.Materials)
				return sb.Append(", ").Append(FormatMissingMaterials(def)).ToString();
			string reason = PlanScreen.GetTooltipForRequirementsState(def, state);
			if (string.IsNullOrEmpty(reason))
				return sb.ToString();
			return sb.Append(", ").Append(reason).ToString();
		}

		private static string FormatCost(BuildingDef def) {
			var ingredients = def.CraftRecipe.Ingredients;
			if (ingredients.Count == 0) return null;
			return FormatIngredientList(ingredients);
		}

		private static string FormatMissingMaterials(BuildingDef def) {
			return (string)STRINGS.UI.PRODUCTINFO_MISSINGRESOURCES_HOVER
				+ ": " + FormatIngredientList(def.CraftRecipe.Ingredients);
		}

		private static string FormatIngredientList(
				System.Collections.Generic.IList<Recipe.Ingredient> ingredients) {
			bool allSameAmount = true;
			float sharedAmount = ingredients.Count > 0 ? ingredients[0].amount : 0f;
			for (int i = 1; i < ingredients.Count; i++) {
				if (ingredients[i].amount != sharedAmount) {
					allSameAmount = false;
					break;
				}
			}

			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < ingredients.Count; i++) {
				if (i > 0) sb.Append(", ");
				sb.Append(ingredients[i].tag.ProperName());
				if (!allSameAmount) {
					sb.Append(' ');
					sb.Append(GameUtil.GetFormattedMass(ingredients[i].amount));
				}
			}
			if (allSameAmount && ingredients.Count > 0) {
				sb.Append(' ');
				sb.Append(GameUtil.GetFormattedMass(sharedAmount));
			}
			return sb.ToString();
		}
	}
}
