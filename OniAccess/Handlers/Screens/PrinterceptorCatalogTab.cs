using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Catalog tab for PrinterceptorScreenHandler. 2-level tree:
	/// Level 0 = Critters / Plants. Level 1 = individual printable entries,
	/// sorted alphabetically by the resulting creature or plant's proper name.
	///
	/// The catalog is built once on first activation. Printable tags and display
	/// names are static for a save; cost/affordability are re-read at speech time
	/// by the details tab.
	/// </summary>
	internal class PrinterceptorCatalogTab: NavTreeHandler, IScreenTab {
		private readonly PrinterceptorScreenHandler _parent;

		private List<CatalogEntry> _critters;
		private List<CatalogEntry> _plants;
		private bool _built;

		private const int CATEGORY_COUNT = 2;

		private List<CatalogEntry> GetCategory(int index) {
			switch (index) {
				case 0: return _critters;
				case 1: return _plants;
				default: return null;
			}
		}

		internal PrinterceptorCatalogTab(PrinterceptorScreenHandler parent) : base(screen: null) {
			_parent = parent;
			// Search the entries (activatable leaves), not the two category rows.
			Nav.SearchFilter = n => n.IsActivatable();
		}

		public string TabName => (string)STRINGS.ONIACCESS.PRINTERCEPTOR.CATALOG_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => DrillNavHelpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			OnTabActivatedOnTag(announce, tag: default);
		}

		/// <summary>
		/// Activate and optionally position the cursor on a specific printable tag.
		/// Used when returning from the details tab so the user lands on the same
		/// leaf they were inspecting instead of resetting to the first category.
		/// </summary>
		internal void OnTabActivatedOnTag(bool announce, Tag tag) {
			if (!_built) {
				BuildCatalog();
				_built = true;
			}
			if (!tag.IsValid || !NavigateToTag(tag))
				ResetState();
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0)
				AnnounceCurrent(interrupt: false);
		}

		/// <summary>
		/// Returns the printable tag for the entry under the cursor, or
		/// default(Tag) if the cursor is on a category (level 0) or out of range.
		/// </summary>
		internal Tag CurrentLeafTag() {
			if (Nav.Depth < 1) return default;
			int c = Nav.Path[0];
			int i = Nav.Path[1];
			if (c < 0 || c >= CATEGORY_COUNT) return default;
			var entries = GetCategory(c);
			if (entries == null || i < 0 || i >= entries.Count) return default;
			return entries[i].printableTag;
		}

		private bool NavigateToTag(Tag tag) {
			for (int c = 0; c < CATEGORY_COUNT; c++) {
				var entries = GetCategory(c);
				if (entries == null) continue;
				for (int i = 0; i < entries.Count; i++) {
					if (entries[i].printableTag == tag) {
						Nav.SetPath(new[] { c, i });
						_search.Clear();
						SuppressSearchThisFrame();
						return true;
					}
				}
			}
			return false;
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
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>(CATEGORY_COUNT);
			for (int c = 0; c < CATEGORY_COUNT; c++) {
				int cat = c;
				roots.Add(new MenuNode(
					() => GetCategoryName(cat),
					children: () => BuildEntries(cat)));
			}
			return roots;
		}

		private IReadOnlyList<NavItem> BuildEntries(int categoryIndex) {
			var entries = GetCategory(categoryIndex);
			if (entries == null) return System.Array.Empty<NavItem>();
			var list = new List<NavItem>(entries.Count);
			foreach (var entry in entries) {
				var e = entry;
				list.Add(new MenuNode(
					() => e.displayName,
					activate: () => { ActivateEntry(e); return true; }));
			}
			return list;
		}

		private void ActivateEntry(CatalogEntry entry) {
			_parent.SetSelectedEntity(entry.printableTag);
			PlaySound("HUD_Click_Open");
			_parent.SwitchToDetailsTab(announce: true);
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
			_critters = BuildCritters();
			_plants = BuildPlants();
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
	}
}
