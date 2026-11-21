using System.Collections.Generic;
using OniAccess.Handlers.Tiles.Scanner.Routing;
using OniAccess.Util;
using ProcGen;
using UnityEngine;

namespace OniAccess.Handlers.Tiles.Scanner {

	// -----------------------------------------------------------------------
	// Output data structures — intermediate cluster descriptors.
	// Backends convert these to ScanEntry objects in a later phase.
	// -----------------------------------------------------------------------

	public class ElementCluster {
		public SimHashes ElementId;
		// Natural backwall layer cluster; ElementId is the backwall element
		public bool IsBackwall;
		public string Category;
		public string Subcategory;
		public string ElementName;
		public List<int> Cells = new List<int>();
		public float TotalMass;
	}

	public class TileCluster {
		public string PrefabId;
		public string Category;
		public string Subcategory;
		public List<int> Cells = new List<int>();
	}

	public class NetworkSegmentCluster {
		public string PrefabId;
		public string ScannerCategory;
		public string ScannerSubcategory;
		public List<int> Cells = new List<int>();
	}

	public class BridgeInstance {
		public int Cell;
		public GameObject Go;
		public string ScannerCategory;
		public string ScannerSubcategory;
	}

	public class OrderCluster {
		public OrderRouter.OrderType OrderType;
		public string TargetName;
		public bool IsMixed;
		public List<int> Cells = new List<int>();
	}

	public class IndividualOrder {
		public OrderRouter.OrderType OrderType;
		public int Cell;
		public GameObject Entity;
		public string EntityName;
	}

	public class BiomeCluster {
		public SubWorld.ZoneType ZoneType;
		public string DisplayName;
		public List<int> Cells = new List<int>();
	}

	public class GridScanResult {
		public List<ElementCluster> Elements = new List<ElementCluster>();
		public List<TileCluster> Tiles = new List<TileCluster>();
		public List<NetworkSegmentCluster> NetworkSegments = new List<NetworkSegmentCluster>();
		// The visible cells of each network, per NetworkLayerConfig type,
		// keyed by network id. Covers every segment type on the network.
		public Dictionary<int, List<int>>[] NetworkCells;
		public List<BridgeInstance> Bridges = new List<BridgeInstance>();
		public List<OrderCluster> OrderClusters = new List<OrderCluster>();
		public List<IndividualOrder> IndividualOrders = new List<IndividualOrder>();
		public List<BiomeCluster> Biomes = new List<BiomeCluster>();
	}

	// -----------------------------------------------------------------------
	// GridScanner — single-pass grid iteration with union-find clustering.
	// -----------------------------------------------------------------------

	public class GridScanner {
		private readonly BiomeNameResolver _biomeNameResolver;

		// UnionFind instances — one per clustering domain
		private UnionFind _ufElements;
		private UnionFind _ufBackwalls;
		private UnionFind _ufTiles;
		private readonly UnionFind[] _ufBoxOrders; // one per box-order type (4)
		private UnionFind _ufSameTypeOrders;
		private UnionFind _ufBiomes;

		// Per-cell type keys, indexed by cell. Set during forward pass,
		// used for union decisions and cluster extraction.
		private int[] _elementKey;       // SimHashes cast to int; 0 = none
		private int[] _backwallKey;      // backwall SimHashes cast to int; 0 = none
		private int[] _tileKey;          // prefab ID hash; 0 = none
		private int[][] _networkId;      // [networkTypeIndex][cell]; network.id + 1, 0 = none
		private int[][] _boxOrderKey;    // [boxOrderIndex][cell]; 1 = present, 0 = none
		private string[] _sameTypeKey;   // "Build:PrefabId" etc; null = none
		private int[] _biomeKey;         // ZoneType cast to int; -1 = unset

		// Cluster accumulator maps, keyed by union-find root cell
		private readonly Dictionary<int, ElementCluster> _elementClusters
			= new Dictionary<int, ElementCluster>();
		private readonly Dictionary<int, ElementCluster> _backwallClusters
			= new Dictionary<int, ElementCluster>();
		private readonly Dictionary<int, TileCluster> _tileClusters
			= new Dictionary<int, TileCluster>();
		private readonly Dictionary<long, NetworkSegmentCluster>[] _networkClusters;
		private readonly Dictionary<int, List<int>>[] _networkCells;
		private readonly Dictionary<int, OrderCluster>[] _boxOrderClusters;
		private readonly Dictionary<int, OrderCluster> _sameTypeOrderClusters
			= new Dictionary<int, OrderCluster>();
		private readonly Dictionary<int, BiomeCluster> _biomeClusters
			= new Dictionary<int, BiomeCluster>();

		// Non-clustered output, emitted directly during forward pass
		private readonly List<BridgeInstance> _bridges = new List<BridgeInstance>();
		private readonly HashSet<int> _seenBridges = new HashSet<int>();
		private readonly List<IndividualOrder> _individualOrders = new List<IndividualOrder>();

		// Multi-cell building dedup for same-type orders during extraction.
		// Keyed by union-find root; tracks seen GameObject instance IDs to
		// avoid counting multi-cell buildings multiple times in a cluster.
		private readonly Dictionary<int, HashSet<int>> _sameTypeSeenIds
			= new Dictionary<int, HashSet<int>>();

		// Box-order type definitions: detection function + OrderType + key array index
		private static readonly BoxOrderDef[] BoxOrderDefs = {
			new BoxOrderDef(OrderRouter.HasDigOrder, OrderRouter.Dig, 0),
			new BoxOrderDef(OrderRouter.HasMopOrder, OrderRouter.Mop, 1),
			new BoxOrderDef(OrderRouter.HasSweepOrder, OrderRouter.Sweep, 2),
			new BoxOrderDef(OrderRouter.HasDisinfectOrder, OrderRouter.Disinfect, 3),
		};

		private static readonly int[] ConduitLayers = {
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.SolidConduit,
		};

		private struct BoxOrderDef {
			public readonly System.Func<int, bool> Detect;
			public readonly OrderRouter.OrderType Type;
			public readonly int Index;

			public BoxOrderDef(
				System.Func<int, bool> detect, OrderRouter.OrderType type, int index) {
				Detect = detect;
				Type = type;
				Index = index;
			}
		}

		public GridScanner(BiomeNameResolver biomeNameResolver) {
			_biomeNameResolver = biomeNameResolver;

			int netCount = NetworkLayerConfig.Types.Length;
			_networkId = new int[netCount][];
			_networkClusters = new Dictionary<long, NetworkSegmentCluster>[netCount];
			_networkCells = new Dictionary<int, List<int>>[netCount];
			for (int i = 0; i < netCount; i++) {
				_networkClusters[i] = new Dictionary<long, NetworkSegmentCluster>();
				_networkCells[i] = new Dictionary<int, List<int>>();
			}

			int boxCount = BoxOrderDefs.Length;
			_ufBoxOrders = new UnionFind[boxCount];
			_boxOrderKey = new int[boxCount][];
			_boxOrderClusters = new Dictionary<int, OrderCluster>[boxCount];
			for (int i = 0; i < boxCount; i++)
				_boxOrderClusters[i] = new Dictionary<int, OrderCluster>();
		}

		/// <summary>
		/// Perform a full grid scan of the given world.
		/// Returns cluster descriptors for backends to convert to ScanEntry objects.
		/// </summary>
		public GridScanResult Scan(int worldId) {
			var world = ClusterManager.Instance.GetWorld(worldId);
			int cellCount = Grid.CellCount;

			AllocateOrReset(cellCount);
			ClearAccumulators();
			ForwardPass(world);
			ExtractClusters(cellCount);
			ResolveBoxOrderTargets();

			return BuildResult();
		}

		// -------------------------------------------------------------------
		// Allocation / reset
		// -------------------------------------------------------------------

		private void AllocateOrReset(int cellCount) {
			_ufElements = ResetUF(_ufElements, cellCount);
			_ufBackwalls = ResetUF(_ufBackwalls, cellCount);
			_ufTiles = ResetUF(_ufTiles, cellCount);
			for (int i = 0; i < _ufBoxOrders.Length; i++)
				_ufBoxOrders[i] = ResetUF(_ufBoxOrders[i], cellCount);
			_ufSameTypeOrders = ResetUF(_ufSameTypeOrders, cellCount);
			_ufBiomes = ResetUF(_ufBiomes, cellCount);

			_elementKey = ResetIntArray(_elementKey, cellCount, 0);
			_backwallKey = ResetIntArray(_backwallKey, cellCount, 0);
			_tileKey = ResetIntArray(_tileKey, cellCount, 0);
			for (int i = 0; i < _networkId.Length; i++)
				_networkId[i] = ResetIntArray(_networkId[i], cellCount, 0);
			for (int i = 0; i < _boxOrderKey.Length; i++)
				_boxOrderKey[i] = ResetIntArray(_boxOrderKey[i], cellCount, 0);
			_biomeKey = ResetIntArray(_biomeKey, cellCount, -1);

			if (_sameTypeKey == null || _sameTypeKey.Length != cellCount)
				_sameTypeKey = new string[cellCount];
			else
				System.Array.Clear(_sameTypeKey, 0, cellCount);
		}

		private static UnionFind ResetUF(UnionFind uf, int size) {
			if (uf == null) return new UnionFind(size);
			uf.Reset(size);
			return uf;
		}

		private static int[] ResetIntArray(int[] arr, int size, int sentinel) {
			if (arr == null || arr.Length != size)
				arr = new int[size];
			if (sentinel == 0)
				System.Array.Clear(arr, 0, size);
			else
				for (int i = 0; i < size; i++) arr[i] = sentinel;
			return arr;
		}

		private void ClearAccumulators() {
			_elementClusters.Clear();
			_backwallClusters.Clear();
			_tileClusters.Clear();
			for (int i = 0; i < _networkClusters.Length; i++) {
				_networkClusters[i].Clear();
				_networkCells[i].Clear();
			}
			for (int i = 0; i < _boxOrderClusters.Length; i++)
				_boxOrderClusters[i].Clear();
			_sameTypeOrderClusters.Clear();
			_biomeClusters.Clear();
			_bridges.Clear();
			_seenBridges.Clear();
			_individualOrders.Clear();
			_sameTypeSeenIds.Clear();
		}

		// -------------------------------------------------------------------
		// Forward pass — row-major iteration over world bounding rect
		// -------------------------------------------------------------------

		private void ForwardPass(WorldContainer world) {
			int ox = world.WorldOffset.x;
			int oy = world.WorldOffset.y;
			int w = world.Width;
			int h = world.Height;
			int worldId = world.id;

			for (int row = 0; row < h; row++) {
				for (int col = 0; col < w; col++) {
					int cell = Grid.XYToCell(ox + col, oy + row);
					if (!Grid.IsValidCellInWorld(cell, worldId)) continue;
					if (Grid.Visible[cell] == 0) continue;

					try {
						ProcessCell(cell);
					} catch (System.Exception ex) {
						Log.Error($"GridScanner: exception at cell {cell}: {ex}");
					}
				}
			}
		}

		private void ProcessCell(int cell) {
			ProcessElement(cell);
			ProcessBackwall(cell);
			ProcessTiles(cell);
			for (int i = 0; i < NetworkLayerConfig.Types.Length; i++)
				ProcessNetwork(cell, i);
			ProcessBridges(cell);
			ProcessBoxOrders(cell);
			ProcessSameTypeOrders(cell);
			ProcessPickupableOrders(cell);
			ProcessEmptyPipeOrders(cell);
			ProcessBiome(cell);
		}

		// --- Natural elements ---

		private void ProcessElement(int cell) {
			if (Grid.Objects[cell, (int)ObjectLayer.FoundationTile] != null) return;

			Element elem = Grid.Element[cell];

			_elementKey[cell] = (int)elem.id;
			UnionWithNeighbors(_ufElements, _elementKey, cell);
		}

		// --- Natural backwall (only visible behind non-solid cells) ---

		private void ProcessBackwall(int cell) {
			if (Grid.Solid[cell] || !BackwallManager.HasBackwall(cell)) return;

			_backwallKey[cell] = (int)BackwallManager.At(cell).Element.id;
			UnionWithNeighbors(_ufBackwalls, _backwallKey, cell);
		}

		// --- Constructed tiles (FoundationTile layer 12, LadderTile layer 27) ---

		private void ProcessTiles(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
			if (go == null) {
				go = Grid.Objects[cell, (int)ObjectLayer.LadderTile];
				if (go == null) return;
			}

			var building = go.GetComponent<Building>();
			if (building == null) return;
			if (building.Def.isUtility) return;
			if (!building.Def.isKAnimTile) return;

			_tileKey[cell] = building.Def.PrefabID.GetHashCode();
			UnionWithNeighbors(_ufTiles, _tileKey, cell);
		}

		// --- Network segments ---

		private void ProcessNetwork(int cell, int typeIndex) {
			var net = NetworkLayerConfig.Types[typeIndex];
			var go = Grid.Objects[cell, (int)net.SegmentLayer];
			if (go == null) return;

			var building = go.GetComponent<Building>();
			if (building == null) return;
			if (!building.Def.isUtility) return;

			var network = net.GetManager().GetNetworkForCell(cell);
			if (network == null) return;

			_networkId[typeIndex][cell] = network.id + 1;
		}

		// --- Bridge instances (non-clustered) ---

		private void ProcessBridges(int cell) {
			for (int i = 0; i < NetworkLayerConfig.Types.Length; i++) {
				var net = NetworkLayerConfig.Types[i];
				var go = Grid.Objects[cell, (int)net.BridgeLayer];
				if (go == null) continue;

				var building = go.GetComponent<Building>();
				if (building == null || !building.Def.isUtility) continue;

				if (_seenBridges.Contains(go.GetInstanceID())) continue;
				_seenBridges.Add(go.GetInstanceID());

				_bridges.Add(new BridgeInstance {
					Cell = cell,
					Go = go,
					ScannerCategory = net.ScannerCategory,
					ScannerSubcategory = net.ScannerSubcategory,
				});
			}
		}

		// --- Box-selection orders (dig, mop, sweep, disinfect) ---
		// Each type has its own UF and key array so a cell with both dig
		// and mop orders clusters independently in both domains.

		private void ProcessBoxOrders(int cell) {
			for (int i = 0; i < BoxOrderDefs.Length; i++) {
				if (!BoxOrderDefs[i].Detect(cell)) continue;
				_boxOrderKey[i][cell] = 1;
				UnionWithNeighbors(_ufBoxOrders[i], _boxOrderKey[i], cell);
			}
		}

		// --- Same-type orders (build, deconstruct, harvest, uproot) ---

		private void ProcessSameTypeOrders(int cell) {
			TrySameTypeOrder(cell, OrderRouter.GetBuildOrderType, "Build");
			TrySameTypeOrder(cell, OrderRouter.GetReplaceOrderType, "Replace");
			TrySameTypeOrder(cell, OrderRouter.GetDeconstructOrderType, "Deconstruct");
			TrySameTypeOrder(cell, OrderRouter.GetHarvestOrderType, "Harvest");
			TrySameTypeOrder(cell, OrderRouter.GetUprootOrderType, "Uproot");
		}

		private void TrySameTypeOrder(
				int cell, System.Func<int, string> detectFn, string orderLabel) {
			if (_sameTypeKey[cell] != null) return;

			string prefabId = detectFn(cell);
			if (prefabId == null) return;

			string key = orderLabel + ":" + prefabId;
			_sameTypeKey[cell] = key;
			UnionSameTypeNeighbors(cell);
		}

		// --- Individual orders from pickupable linked list ---

		private void ProcessPickupableOrders(int cell) {
			var headGo = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (headGo == null) return;

			var pickupable = headGo.GetComponent<Pickupable>();
			if (pickupable == null) return;

			var item = pickupable.objectLayerListItem;
			while (item != null) {
				var go = item.gameObject;

				var faction = go.GetComponent<FactionAlignment>();
				if (faction != null && faction.IsPlayerTargeted()) {
					_individualOrders.Add(new IndividualOrder {
						OrderType = OrderRouter.Attack,
						Cell = cell,
						Entity = go,
						EntityName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
					});
				}

				var capturable = go.GetComponent<Capturable>();
				if (capturable != null && capturable.IsMarkedForCapture) {
					_individualOrders.Add(new IndividualOrder {
						OrderType = OrderRouter.Capture,
						Cell = cell,
						Entity = go,
						EntityName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
					});
				}

				item = item.nextItem;
			}
		}

		// --- Empty pipe orders (individual, not clustered) ---

		private void ProcessEmptyPipeOrders(int cell) {
			for (int i = 0; i < ConduitLayers.Length; i++) {
				if (!OrderRouter.HasEmptyPipeOrder(cell, ConduitLayers[i])) continue;
				var go = Grid.Objects[cell, ConduitLayers[i]];
				_individualOrders.Add(new IndividualOrder {
					OrderType = OrderRouter.EmptyPipe,
					Cell = cell,
					Entity = go,
					EntityName = go.GetComponent<KSelectable>()?.GetName() ?? go.name,
				});
			}
		}

		// --- Biome zones ---

		private void ProcessBiome(int cell) {
			var zone = World.Instance.zoneRenderData.GetSubWorldZoneType(cell);
			_biomeKey[cell] = (int)zone;
			UnionWithNeighborsSigned(_ufBiomes, _biomeKey, cell);
		}

		// -------------------------------------------------------------------
		// Union helpers
		// -------------------------------------------------------------------

		// For domains where key 0 = absent. Union if both keys match and nonzero.
		private static void UnionWithNeighbors(UnionFind uf, int[] keys, int cell) {
			if (keys[cell] == 0) return;

			int left = Grid.CellLeft(cell);
			if (left != Grid.InvalidCell && keys[left] == keys[cell])
				uf.Union(cell, left);

			int below = Grid.CellBelow(cell);
			if (below >= 0 && below < keys.Length && keys[below] == keys[cell])
				uf.Union(cell, below);
		}

		// For biome domain where -1 = unset. Union if both >= 0 and equal.
		private static void UnionWithNeighborsSigned(
				UnionFind uf, int[] keys, int cell) {
			if (keys[cell] < 0) return;

			int left = Grid.CellLeft(cell);
			if (left != Grid.InvalidCell && keys[left] == keys[cell])
				uf.Union(cell, left);

			int below = Grid.CellBelow(cell);
			if (below >= 0 && below < keys.Length && keys[below] == keys[cell])
				uf.Union(cell, below);
		}

		// For same-type orders: string equality on _sameTypeKey.
		private void UnionSameTypeNeighbors(int cell) {
			int left = Grid.CellLeft(cell);
			if (left != Grid.InvalidCell
				&& _sameTypeKey[left] != null
				&& _sameTypeKey[left] == _sameTypeKey[cell])
				_ufSameTypeOrders.Union(cell, left);

			int below = Grid.CellBelow(cell);
			if (below >= 0 && below < _sameTypeKey.Length
				&& _sameTypeKey[below] != null
				&& _sameTypeKey[below] == _sameTypeKey[cell])
				_ufSameTypeOrders.Union(cell, below);
		}

		// -------------------------------------------------------------------
		// Cluster extraction — second pass over all cells
		// -------------------------------------------------------------------

		private void ExtractClusters(int cellCount) {
			for (int cell = 0; cell < cellCount; cell++) {
				if (_elementKey[cell] != 0)
					ExtractElement(cell);
				if (_backwallKey[cell] != 0)
					ExtractBackwall(cell);
				if (_tileKey[cell] != 0)
					ExtractTile(cell);
				for (int i = 0; i < _networkId.Length; i++)
					if (_networkId[i][cell] != 0)
						ExtractNetwork(cell, i);
				for (int i = 0; i < _boxOrderKey.Length; i++)
					if (_boxOrderKey[i][cell] != 0)
						ExtractBoxOrder(cell, i);
				if (_sameTypeKey[cell] != null)
					ExtractSameTypeOrder(cell);
				if (_biomeKey[cell] >= 0)
					ExtractBiome(cell);
			}
		}

		private void ExtractElement(int cell) {
			int root = _ufElements.Find(cell);
			if (!_elementClusters.TryGetValue(root, out var cluster)) {
				var elem = Grid.Element[cell];
				string category, subcategory;
				if (elem.IsSolid) {
					category = ScannerTaxonomy.Categories.Solids;
					subcategory = ElementRouter.GetSolidSubcategory(elem);
				} else if (elem.IsLiquid) {
					category = ScannerTaxonomy.Categories.Liquids;
					subcategory = ElementRouter.GetLiquidSubcategory(elem);
				} else {
					category = ScannerTaxonomy.Categories.Gases;
					subcategory = ElementRouter.GetGasSubcategory(elem);
				}
				cluster = new ElementCluster {
					ElementId = elem.id,
					Category = category,
					Subcategory = subcategory,
					ElementName = elem.name,
				};
				_elementClusters[root] = cluster;
			}
			cluster.Cells.Add(cell);
			cluster.TotalMass += Grid.Mass[cell];
		}

		private void ExtractBackwall(int cell) {
			int root = _ufBackwalls.Find(cell);
			if (!_backwallClusters.TryGetValue(root, out var cluster)) {
				var elem = BackwallManager.At(cell).Element;
				cluster = new ElementCluster {
					ElementId = elem.id,
					IsBackwall = true,
					Category = ScannerTaxonomy.Categories.Solids,
					Subcategory = ElementRouter.GetSolidSubcategory(elem),
					ElementName = Sections.ElementSection.FormatBackwallName(elem),
				};
				_backwallClusters[root] = cluster;
			}
			cluster.Cells.Add(cell);
			cluster.TotalMass += BackwallManager.At(cell).Mass;
		}

		private void ExtractTile(int cell) {
			int root = _ufTiles.Find(cell);
			if (!_tileClusters.TryGetValue(root, out var cluster)) {
				var go = Grid.Objects[cell, (int)ObjectLayer.FoundationTile]
					?? Grid.Objects[cell, (int)ObjectLayer.LadderTile];
				string prefabId = go.GetComponent<Building>().Def.PrefabID;
				var (cat, sub) = TileRouter.Route(prefabId);
				cluster = new TileCluster {
					PrefabId = prefabId,
					Category = cat,
					Subcategory = sub,
				};
				_tileClusters[root] = cluster;
			}
			cluster.Cells.Add(cell);
		}

		private void ExtractNetwork(int cell, int typeIndex) {
			var net = NetworkLayerConfig.Types[typeIndex];
			var go = Grid.Objects[cell, (int)net.SegmentLayer];
			int prefabHash = go.GetComponent<Building>().Def.PrefabID.GetHashCode();
			long key = ((long)_networkId[typeIndex][cell] << 32) | (uint)prefabHash;

			var clusters = _networkClusters[typeIndex];
			if (!clusters.TryGetValue(key, out var cluster)) {
				string prefabId = go.GetComponent<Building>().Def.PrefabID;
				cluster = new NetworkSegmentCluster {
					PrefabId = prefabId,
					ScannerCategory = net.ScannerCategory,
					ScannerSubcategory = net.ScannerSubcategory,
				};
				clusters[key] = cluster;
			}
			cluster.Cells.Add(cell);

			int networkId = _networkId[typeIndex][cell] - 1;
			if (!_networkCells[typeIndex].TryGetValue(networkId, out var cells)) {
				cells = new List<int>();
				_networkCells[typeIndex][networkId] = cells;
			}
			cells.Add(cell);
		}

		private void ExtractBoxOrder(int cell, int orderIndex) {
			int root = _ufBoxOrders[orderIndex].Find(cell);
			var clusters = _boxOrderClusters[orderIndex];
			if (!clusters.TryGetValue(root, out var cluster)) {
				cluster = new OrderCluster {
					OrderType = BoxOrderDefs[orderIndex].Type,
				};
				clusters[root] = cluster;
			}
			cluster.Cells.Add(cell);
		}

		private void ExtractSameTypeOrder(int cell) {
			int root = _ufSameTypeOrders.Find(cell);
			string key = _sameTypeKey[cell];
			string orderLabel = key.Substring(0, key.IndexOf(':'));

			if (!_sameTypeOrderClusters.TryGetValue(root, out var cluster)) {
				string targetName = ReadSameTypeOrderName(cell, orderLabel);
				cluster = new OrderCluster {
					OrderType = OrderTypeFromLabel(orderLabel),
					TargetName = targetName,
				};
				_sameTypeOrderClusters[root] = cluster;
			}

			// Multi-cell order dedup: a 3x3 building, a conduit bridge, or a
			// tile registers one GameObject across all the cells it occupies.
			// Count each unique GameObject once so the cluster's cell count
			// reflects the number of orders, not occupied cells.
			var go = OrderRouter.GetSameTypeOrderObject(cell, orderLabel);
			if (go != null) {
				if (!_sameTypeSeenIds.TryGetValue(root, out var seen)) {
					seen = new HashSet<int>();
					_sameTypeSeenIds[root] = seen;
				}
				if (!seen.Add(go.GetInstanceID())) return;
			}

			cluster.Cells.Add(cell);
		}

		private void ExtractBiome(int cell) {
			int root = _ufBiomes.Find(cell);
			if (!_biomeClusters.TryGetValue(root, out var cluster)) {
				var zone = (SubWorld.ZoneType)_biomeKey[cell];
				cluster = new BiomeCluster {
					ZoneType = zone,
					DisplayName = _biomeNameResolver.GetName(zone),
				};
				_biomeClusters[root] = cluster;
			}
			cluster.Cells.Add(cell);
		}

		// -------------------------------------------------------------------
		// Box-order target name resolution
		// -------------------------------------------------------------------
		// After extraction, determine whether each box-order cluster is
		// homogeneous (single target type) or mixed.

		private void ResolveBoxOrderTargets() {
			for (int i = 0; i < _boxOrderClusters.Length; i++) {
				var def = BoxOrderDefs[i];
				foreach (var kvp in _boxOrderClusters[i])
					ResolveBoxOrderTarget(kvp.Value, def);
			}
		}

		private static void ResolveBoxOrderTarget(OrderCluster cluster, BoxOrderDef def) {
			string firstName = null;
			bool mixed = false;

			for (int i = 0; i < cluster.Cells.Count; i++) {
				string name = GetBoxOrderTarget(cluster.Cells[i], def);
				if (name == null) continue;
				if (firstName == null) {
					firstName = name;
				} else if (firstName != name) {
					mixed = true;
					break;
				}
			}

			cluster.IsMixed = mixed;
			cluster.TargetName = mixed
				? (string)STRINGS.ONIACCESS.SCANNER.MIXED
				: firstName;
		}

		private static string GetBoxOrderTarget(int cell, BoxOrderDef def) {
			// Dig and Mop targets are the element at the cell.
			// Sweep and Disinfect don't have meaningful per-cell targets
			// — they cluster regardless of target type, so target name is
			// the order label itself (no material suffix).
			if (def.Index == 0) return OrderRouter.GetDigTarget(cell);
			if (def.Index == 1) return OrderRouter.GetMopTarget(cell);
			return null;
		}

		// -------------------------------------------------------------------
		// Result assembly
		// -------------------------------------------------------------------

		private GridScanResult BuildResult() {
			var result = new GridScanResult();

			foreach (var kvp in _elementClusters)
				result.Elements.Add(kvp.Value);
			foreach (var kvp in _backwallClusters)
				result.Elements.Add(kvp.Value);
			foreach (var kvp in _tileClusters)
				result.Tiles.Add(kvp.Value);
			for (int i = 0; i < _networkClusters.Length; i++)
				foreach (var kvp in _networkClusters[i])
					result.NetworkSegments.Add(kvp.Value);
			result.NetworkCells = _networkCells;
			result.Bridges.AddRange(_bridges);
			for (int i = 0; i < _boxOrderClusters.Length; i++)
				foreach (var kvp in _boxOrderClusters[i])
					result.OrderClusters.Add(kvp.Value);
			foreach (var kvp in _sameTypeOrderClusters)
				result.OrderClusters.Add(kvp.Value);
			result.IndividualOrders.AddRange(_individualOrders);
			foreach (var kvp in _biomeClusters)
				result.Biomes.Add(kvp.Value);

			return result;
		}

		// -------------------------------------------------------------------
		// Helpers
		// -------------------------------------------------------------------

		private static OrderRouter.OrderType OrderTypeFromLabel(string label) {
			switch (label) {
				case "Build": return OrderRouter.Build;
				case "Replace": return OrderRouter.Replace;
				case "Deconstruct": return OrderRouter.Deconstruct;
				case "Harvest": return OrderRouter.Harvest;
				case "Uproot": return OrderRouter.Uproot;
				default:
					Log.Warn($"GridScanner: unexpected order label '{label}'");
					return OrderRouter.Build;
			}
		}

		private static string ReadSameTypeOrderName(int cell, string orderLabel) {
			switch (orderLabel) {
				case "Build": return OrderRouter.GetBuildOrderName(cell);
				case "Replace": return OrderRouter.GetReplaceOrderName(cell);
				case "Deconstruct": return OrderRouter.GetDeconstructOrderName(cell);
				case "Harvest": return OrderRouter.GetHarvestOrderName(cell);
				case "Uproot": return OrderRouter.GetUprootOrderName(cell);
				default:
					Log.Warn($"GridScanner: unknown order label '{orderLabel}'");
					return orderLabel;
			}
		}
	}
}
