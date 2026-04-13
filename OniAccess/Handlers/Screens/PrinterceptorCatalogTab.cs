using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using OniAccess.Speech;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Catalog tab for PrinterceptorScreenHandler. 2-level NestedMenuHandler:
	/// Level 0 = Critters / Plants. Level 1 = individual printable entries,
	/// sorted alphabetically by the resulting creature or plant's proper name.
	///
	/// The catalog is built once on first activation. Printable tags and display
	/// names are static for a save; cost/affordability are re-read at speech time
	/// by the details tab.
	/// </summary>
	internal class PrinterceptorCatalogTab: NestedMenuHandler, IScreenTab {
		private readonly PrinterceptorScreenHandler _parent;

		private readonly List<CatalogEntry>[] _byCategory = new List<CatalogEntry>[2];
		private List<FlatEntry> _flatSearch;
		private bool _built;

		internal PrinterceptorCatalogTab(PrinterceptorScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.PRINTERCEPTOR.CATALOG_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => NestedNavHelpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			if (!_built) {
				BuildCatalog();
				_built = true;
			}
			ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(label);
			}
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// NestedMenuHandler abstracts
		// ========================================

		protected override int MaxLevel => 1;
		protected override int SearchLevel => 1;

		protected override int GetItemCount(int level, int[] indices) {
			if (level == 0) return _byCategory.Length;
			if (indices[0] < 0 || indices[0] >= _byCategory.Length) return 0;
			var entries = _byCategory[indices[0]];
			return entries?.Count ?? 0;
		}

		protected override string GetItemLabel(int level, int[] indices) {
			if (level == 0) return GetCategoryName(indices[0]);
			if (indices[0] < 0 || indices[0] >= _byCategory.Length) return null;
			var entries = _byCategory[indices[0]];
			if (entries == null || indices[1] < 0 || indices[1] >= entries.Count) return null;
			return entries[indices[1]].displayName;
		}

		protected override string GetParentLabel(int level, int[] indices) {
			if (level <= 0) return null;
			return GetCategoryName(indices[0]);
		}

		protected override void ActivateLeafItem(int[] indices) {
			if (indices[0] < 0 || indices[0] >= _byCategory.Length) return;
			var entries = _byCategory[indices[0]];
			if (entries == null || indices[1] < 0 || indices[1] >= entries.Count) return;
			var entry = entries[indices[1]];
			_parent.SetSelectedEntity(entry.printableTag);
			PlaySound("HUD_Click_Open");
			_parent.SwitchToDetailsTab(announce: true);
		}

		// ========================================
		// Search (flat leaf list across all categories)
		// ========================================

		protected override int GetSearchItemCount(int[] indices) {
			return GetFlatSearch().Count;
		}

		protected override string GetSearchItemLabel(int flatIndex) {
			var all = GetFlatSearch();
			if (flatIndex < 0 || flatIndex >= all.Count) return null;
			return all[flatIndex].displayName;
		}

		protected override void MapSearchIndex(int flatIndex, int[] outIndices) {
			var all = GetFlatSearch();
			if (flatIndex < 0 || flatIndex >= all.Count) return;
			outIndices[0] = all[flatIndex].categoryIndex;
			outIndices[1] = all[flatIndex].entryIndex;
		}

		private List<FlatEntry> GetFlatSearch() {
			if (_flatSearch != null) return _flatSearch;
			var result = new List<FlatEntry>();
			for (int c = 0; c < _byCategory.Length; c++) {
				var entries = _byCategory[c];
				if (entries == null) continue;
				for (int e = 0; e < entries.Count; e++) {
					result.Add(new FlatEntry {
						categoryIndex = c,
						entryIndex = e,
						displayName = entries[e].displayName,
					});
				}
			}
			_flatSearch = result;
			return result;
		}

		// ========================================
		// Catalog construction
		// ========================================

		private static string GetCategoryName(int index) {
			switch (index) {
				case 0: return (string)STRINGS.UI.CODEX.SUBWORLDS.CRITTERS;
				case 1: return (string)STRINGS.UI.CODEX.SUBWORLDS.PLANTS;
				default: return null;
			}
		}

		private void BuildCatalog() {
			_byCategory[0] = BuildCritters();
			_byCategory[1] = BuildPlants();
			_flatSearch = null;
		}

		private static List<CatalogEntry> BuildCritters() {
			var result = new List<CatalogEntry>();
			var seen = new HashSet<Tag>();

			try {
				foreach (var kvp in EggCrackerConfig.EggsBySpecies) {
					foreach (var egg in kvp.Value) {
						if (!egg.isBaseMorph) continue;
						if (!IsPrintablePrefabValid(egg.id)) continue;
						if (seen.Contains(egg.id)) continue;
						string name = GetCritterDisplayName(egg.id);
						if (string.IsNullOrEmpty(name)) continue;
						seen.Add(egg.id);
						result.Add(new CatalogEntry {
							printableTag = egg.id,
							displayName = name,
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorCatalogTab.BuildCritters(eggs): {ex.Message}");
			}

			try {
				Tag bee = "BeeBaby";
				if (IsPrintablePrefabValid(bee) && !seen.Contains(bee)) {
					var prefab = Assets.GetPrefab(bee);
					if (prefab != null) {
						result.Add(new CatalogEntry {
							printableTag = bee,
							displayName = prefab.GetProperName(),
						});
					}
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorCatalogTab.BuildCritters(bee): {ex.Message}");
			}

			result.Sort((a, b) => string.Compare(
				a.displayName, b.displayName, System.StringComparison.CurrentCultureIgnoreCase));
			return result;
		}

		private static List<CatalogEntry> BuildPlants() {
			var result = new List<CatalogEntry>();
			var seen = new HashSet<Tag>();

			try {
				var seedPrefabs = Assets.GetPrefabsWithTag(GameTags.Seed)
					.Concat(Assets.GetPrefabsWithTag(GameTags.CropSeed));
				foreach (var seedPrefab in seedPrefabs) {
					if (seedPrefab == null) continue;
					var prefabId = seedPrefab.GetComponent<KPrefabID>();
					if (prefabId == null) continue;
					Tag tag = prefabId.PrefabTag;
					if (seen.Contains(tag)) continue;
					if (!IsPrintablePrefabValid(tag)) continue;

					var plantable = seedPrefab.GetComponent<PlantableSeed>();
					if (plantable == null) continue;
					var plantPrefab = Assets.GetPrefab(plantable.PlantID);
					if (plantPrefab == null) continue;
					if (plantPrefab.HasTag(GameTags.DeprecatedContent)) continue;

					seen.Add(tag);
					result.Add(new CatalogEntry {
						printableTag = tag,
						displayName = plantPrefab.GetProperName(),
					});
				}
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorCatalogTab.BuildPlants: {ex.Message}");
			}

			result.Sort((a, b) => string.Compare(
				a.displayName, b.displayName, System.StringComparison.CurrentCultureIgnoreCase));
			return result;
		}

		private static bool IsPrintablePrefabValid(Tag id) {
			var go = Assets.TryGetPrefab(id);
			if (go == null) return false;
			var kpid = go.GetComponent<KPrefabID>();
			if (kpid == null) return false;
			if (!Game.IsCorrectDlcActiveForCurrentSave(kpid)) return false;
			if (go.HasTag(GameTags.DeprecatedContent)) return false;
			return true;
		}

		private static string GetCritterDisplayName(Tag eggId) {
			try {
				var eggPrefab = Assets.GetPrefab(eggId);
				if (eggPrefab == null) return null;
				var def = eggPrefab.GetDef<IncubationMonitor.Def>();
				if (def == null) return eggPrefab.GetProperName();
				var creaturePrefab = Assets.GetPrefab(def.spawnedCreature);
				if (creaturePrefab == null) return eggPrefab.GetProperName();
				return creaturePrefab.GetProperName();
			} catch (System.Exception ex) {
				Util.Log.Error($"PrinterceptorCatalogTab.GetCritterDisplayName({eggId}): {ex.Message}");
				return null;
			}
		}

		// ========================================
		// Data types
		// ========================================

		private struct CatalogEntry {
			internal Tag printableTag;
			internal string displayName;
		}

		private struct FlatEntry {
			internal int categoryIndex;
			internal int entryIndex;
			internal string displayName;
		}
	}
}
