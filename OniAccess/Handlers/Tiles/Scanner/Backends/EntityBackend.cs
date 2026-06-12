using System.Collections.Generic;
using OniAccess.Handlers.Tiles.Scanner.Routing;
using OniAccess.Util;
using UnityEngine;
using static OniAccess.Util.DebrisNameHelper;

namespace OniAccess.Handlers.Tiles.Scanner.Backends {
	/// <summary>
	/// Backend for entity-scanned categories: Buildings (non-tile, non-utility),
	/// Debris, and Life. Iterates multiple Components registries.
	/// Each entity is one ScanEntry with a GameObject reference as BackendData.
	/// </summary>
	public class EntityBackend: IScannerBackend {
		private readonly BuildingRouter _buildingRouter;

		public EntityBackend(BuildingRouter buildingRouter) {
			_buildingRouter = buildingRouter;
		}

		public IEnumerable<ScanEntry> Scan(int worldId) {
			foreach (var entry in ScanBuildings(worldId))
				yield return entry;
			foreach (var entry in ScanGravitasProps(worldId))
				yield return entry;
			foreach (var entry in ScanMinnowPOIs(worldId))
				yield return entry;
			foreach (var entry in ScanDebris(worldId))
				yield return entry;
			foreach (var entry in ScanDuplicants(worldId))
				yield return entry;
			foreach (var entry in ScanRobots(worldId))
				yield return entry;
			foreach (var entry in ScanCritters(worldId))
				yield return entry;
			foreach (var entry in ScanPlants(worldId))
				yield return entry;
		}

		public bool ValidateEntry(ScanEntry entry, int cursorCell) {
			var go = (GameObject)entry.BackendData;
			if (go == null || go.IsNullOrDestroyed()) return false;
			int cell = Grid.PosToCell(go.transform.GetPosition());
			if (!Grid.IsVisible(cell)) return false;
			entry.Cell = cell;
			return true;
		}

		public string FormatName(ScanEntry entry) {
			var go = (GameObject)entry.BackendData;
			var facade = go.GetComponent<BuildingFacade>();
			if (facade != null && !facade.IsOriginal) {
				var building = go.GetComponent<Building>();
				if (building != null)
					return building.Def.Name;
			}
			return GetDisplayName(go);
		}

		private IEnumerable<ScanEntry> ScanBuildings(int worldId) {
			foreach (var building in Components.BuildingCompletes.GetWorldItems(worldId)) {
				var def = building.Def;
				if (def.isKAnimTile) continue;
				if (def.isUtility) continue;

				string prefabId = def.PrefabID;
				var (category, subcategory) = _buildingRouter.Route(prefabId);
				if (category == null) continue;

				var go = building.gameObject;
				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				var facade = go.GetComponent<BuildingFacade>();
				string name = (facade != null && !facade.IsOriginal)
					? def.Name
					: go.GetComponent<KSelectable>()?.GetName() ?? prefabId;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = category,
					Subcategory = subcategory,
					ItemName = name,
				};
			}
		}

		private IEnumerable<ScanEntry> ScanGravitasProps(int worldId) {
			var world = ClusterManager.Instance.GetWorld(worldId);
			var bounds = world.WorldOffset;
			var size = world.WorldSize;
			int minX = bounds.x;
			int minY = bounds.y;
			int maxX = minX + size.x;
			int maxY = minY + size.y;
			var seen = new HashSet<int>();

			for (int y = minY; y < maxY; y++) {
				for (int x = minX; x < maxX; x++) {
					int cell = Grid.XYToCell(x, y);
					var go = Grid.Objects[cell, (int)ObjectLayer.Building];
					if (go == null) continue;
					if (go.GetComponent<Building>() != null) continue;
					if (go.GetComponent<KPrefabID>()?.HasTag(GameTags.Gravitas) != true) continue;
					if (!seen.Add(go.GetInstanceID())) continue;
					if (!Grid.IsVisible(cell)) continue;

					yield return new ScanEntry {
						Cell = Grid.PosToCell(go.transform.GetPosition()),
						Backend = this,
						BackendData = go,
						Category = ScannerTaxonomy.Categories.Buildings,
						Subcategory = ScannerTaxonomy.Subcategories.Gravitas,
						ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
					};
				}
			}
		}

		/// <summary>
		/// Minnow quest sites (Aquatic DLC "Unknown Duplicant") are placed
		/// entities without a Building component or Gravitas tag, so neither
		/// building scan sees them; the game registers them only in this list.
		/// Cmps.GetWorldItems casts to KMonoBehaviour and these are state
		/// machine instances, so world filtering must be done manually.
		/// </summary>
		private IEnumerable<ScanEntry> ScanMinnowPOIs(int worldId) {
			foreach (var smi in Components.MinnowImperativePOIs.Items) {
				var go = smi.gameObject;
				if (go.GetMyWorldId() != worldId) continue;

				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Buildings,
					Subcategory = ScannerTaxonomy.Subcategories.Gravitas,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
		}

		private IEnumerable<ScanEntry> ScanDebris(int worldId) {
			foreach (var pickupable in Components.Pickupables.GetWorldItems(worldId)) {
				if (pickupable.storage != null) continue;
				var prefabId = pickupable.GetComponent<KPrefabID>();
				if (prefabId == null) continue;
				if (DebrisRouter.ShouldExclude(prefabId)) continue;

				string subcategory = DebrisRouter.GetSubcategory(prefabId);
				var go = pickupable.gameObject;
				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Debris,
					Subcategory = subcategory,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
		}

		private IEnumerable<ScanEntry> ScanDuplicants(int worldId) {
			foreach (var identity in Components.LiveMinionIdentities.GetWorldItems(worldId)) {
				var go = identity.gameObject;
				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Life,
					Subcategory = ScannerTaxonomy.Subcategories.Duplicants,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
		}

		private IEnumerable<ScanEntry> ScanRobots(int worldId) {
			var seen = new HashSet<int>();
			foreach (var brain in Components.Brains.GetWorldItems(worldId)) {
				var go = brain.gameObject;
				if (!go.GetComponent<KPrefabID>().HasTag(GameTags.Robot)) continue;
				seen.Add(go.GetInstanceID());

				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Life,
					Subcategory = ScannerTaxonomy.Subcategories.Robots,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
			var docks = Components.RemoteWorkerDocks.GetItems(worldId);
			if (docks != null) {
				foreach (var dock in docks) {
					var rw = dock.RemoteWorker;
					if (rw == null) continue;
					var go = rw.gameObject;
					if (!seen.Add(go.GetInstanceID())) continue;

					int cell = Grid.PosToCell(go.transform.GetPosition());
					if (!Grid.IsVisible(cell)) continue;

					yield return new ScanEntry {
						Cell = cell,
						Backend = this,
						BackendData = go,
						Category = ScannerTaxonomy.Categories.Life,
						Subcategory = ScannerTaxonomy.Subcategories.Robots,
						ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
					};
				}
			}
		}

		private IEnumerable<ScanEntry> ScanCritters(int worldId) {
			foreach (var brain in Components.Brains.GetWorldItems(worldId)) {
				var go = brain.gameObject;
				if (go.GetComponent<CreatureBrain>() == null) continue;
				if (go.GetComponent<KPrefabID>().HasTag(GameTags.Robot)) continue;

				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				string subcategory = LifeRouter.IsWild(go)
					? ScannerTaxonomy.Subcategories.WildCritters
					: ScannerTaxonomy.Subcategories.TameCritters;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Life,
					Subcategory = subcategory,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
		}

		private IEnumerable<ScanEntry> ScanPlants(int worldId) {
			foreach (var uprootable in Components.Uprootables.GetWorldItems(worldId)) {
				var go = uprootable.gameObject;
				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;

				string subcategory = LifeRouter.IsFarmPlant(uprootable)
					? ScannerTaxonomy.Subcategories.FarmPlants
					: ScannerTaxonomy.Subcategories.WildPlants;

				yield return new ScanEntry {
					Cell = cell,
					Backend = this,
					BackendData = go,
					Category = ScannerTaxonomy.Categories.Life,
					Subcategory = subcategory,
					ItemName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				};
			}
		}
	}
}
