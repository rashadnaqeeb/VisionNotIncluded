namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Skips along utility networks (power, plumbing, ventilation,
	/// conveyor, automation). Stops at junctions, network boundaries,
	/// transitions between utility and non-utility cells, blueprint
	/// boundaries, and the edges of runs marked for replacement.
	/// Blueprints have no physical network, so they are signed by
	/// prefab type with junctions read from the visual connection grid.
	/// Parameterized by object layers, a network manager accessor,
	/// and the overlay's replacement layer.
	/// </summary>
	public class UtilitySkipStrategy: ISkipStrategy {
		private static readonly object Empty = new object();

		private readonly int[] _layers;
		private readonly int _replacementLayer;
		private readonly System.Func<IUtilityNetworkMgr> _getManager;

		public UtilitySkipStrategy(System.Func<IUtilityNetworkMgr> getManager,
				ObjectLayer replacementLayer, int[] layers) {
			_getManager = getManager;
			_replacementLayer = (int)replacementLayer;
			_layers = layers;
		}

		public object GetSignature(int cell) {
			UnityEngine.GameObject go = FindObject(cell);
			if (go == null) return Empty;

			var building = go.GetComponent<Building>();
			if (building == null || !building.Def.isUtility)
				return Empty;

			if (go.GetComponent<Constructable>() != null) {
				var connections = _getManager().GetConnections(cell, false);
				return (go.PrefabID(), CountDirections(connections) >= 3);
			}

			int networkId = _getManager().GetNetworkForCell(cell)?.id ?? -1;
			if (networkId == -1) return Empty;
			bool isJunction = CountSameNetworkNeighbors(cell, networkId) >= 3;

			var replacement = Grid.Objects[cell, _replacementLayer];
			Tag replacementId = replacement != null
				? replacement.PrefabID() : default;
			return (networkId, isJunction, replacementId);
		}

		private UnityEngine.GameObject FindObject(int cell) {
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go != null
					&& !Sections.ConduitSection.IsPortRegistration(go, layer))
					return go;
			}
			return null;
		}

		private static int CountDirections(UtilityConnections connections) {
			int count = 0;
			if ((connections & UtilityConnections.Up) != 0) count++;
			if ((connections & UtilityConnections.Down) != 0) count++;
			if ((connections & UtilityConnections.Left) != 0) count++;
			if ((connections & UtilityConnections.Right) != 0) count++;
			return count;
		}

		private int CountSameNetworkNeighbors(int cell, int networkId) {
			int count = 0;
			if (HasSameNetwork(Grid.CellAbove(cell), networkId)) count++;
			if (HasSameNetwork(Grid.CellBelow(cell), networkId)) count++;
			if (HasSameNetwork(Grid.CellLeft(cell), networkId)) count++;
			if (HasSameNetwork(Grid.CellRight(cell), networkId)) count++;
			return count;
		}

		private bool HasSameNetwork(int neighbor, int networkId) {
			if (!Grid.IsValidCell(neighbor)) return false;
			var net = _getManager().GetNetworkForCell(neighbor);
			return net != null && net.id == networkId;
		}
	}
}
