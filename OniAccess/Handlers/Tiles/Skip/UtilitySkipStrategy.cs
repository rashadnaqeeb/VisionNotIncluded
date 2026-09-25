namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Skips along utility lines (power, plumbing, ventilation, conveyor,
	/// automation). A step carries on while the line connects across it,
	/// including through the middle of a bridge that runs the way the skip
	/// goes. It stops on entering a junction, at the edges of blueprint and
	/// replacement runs, and where the line ends. A bridge ending on a line
	/// counts as one of its branches, so a bridge landing mid-pipe makes a
	/// junction. Settings add a stop at every bridge middle and count
	/// building ports as branches.
	/// Blueprints have no physical network, so their connections come from
	/// the visual connection grid and their prefab separates planned runs.
	/// Parameterized by object layers (the line's own layer first, then the
	/// layer its bridges and ports register on), a network manager
	/// accessor, and the overlay's replacement layer.
	/// </summary>
	public class UtilitySkipStrategy: ILineSkipStrategy {
		private readonly int[] _layers;
		private readonly int _replacementLayer;
		private readonly System.Func<IUtilityNetworkMgr> _getManager;

		public UtilitySkipStrategy(System.Func<IUtilityNetworkMgr> getManager,
				ObjectLayer replacementLayer, int[] layers) {
			_getManager = getManager;
			_replacementLayer = (int)replacementLayer;
			_layers = layers;
		}

		public bool Continues(int from, int to, Direction direction) {
			if (ConfigManager.Config.SkipStopsAtBridgeMiddles
				&& FindBridgeSpanning(to) != null)
				return false;
			bool vertical = direction == Direction.Up
				|| direction == Direction.Down;
			return UtilityPiece.Continues(
				ReadPiece(from, vertical), ReadPiece(to, vertical), direction);
		}

		/// <summary>
		/// A cell under a bridge's middle holds two lines: the bridge and
		/// whatever it crosses. The one running along the skip's axis is
		/// the one being followed.
		/// </summary>
		private UtilityPiece ReadPiece(int cell, bool vertical) {
			var bridge = FindBridgeSpanning(cell);
			var line = FindLine(cell);
			if (bridge != null && (line == null || IsVertical(bridge) == vertical))
				return BridgePiece(bridge);
			if (line != null)
				return LinePiece(cell, line);
			return UtilityPiece.None;
		}

		private UnityEngine.GameObject FindLine(int cell) {
			var go = Grid.Objects[cell, _layers[0]];
			if (go == null) return null;
			var building = go.GetComponent<Building>();
			return building != null && building.Def.isUtility ? go : null;
		}

		private UtilityPiece LinePiece(int cell, UnityEngine.GameObject go) {
			bool blueprint = go.GetComponent<Constructable>() != null;
			var connections = _getManager().GetConnections(cell, !blueprint)
				| BridgeEndDirections(cell, blueprint);
			int branches = UtilityPiece.CountDirections(connections);
			if (ConfigManager.Config.SkipStopsAtPorts && HasPort(cell))
				branches++;
			Tag replacement = default;
			if (!blueprint) {
				var replacementGo = Grid.Objects[cell, _replacementLayer];
				if (replacementGo != null)
					replacement = replacementGo.PrefabID();
			}
			return new UtilityPiece {
				Present = true,
				Blueprint = blueprint,
				Prefab = blueprint ? go.PrefabID() : default,
				Replacement = replacement,
				Connections = connections,
				Junction = branches >= 3,
			};
		}

		private static UtilityPiece BridgePiece(UnityEngine.GameObject bridge) {
			return new UtilityPiece {
				Present = true,
				Blueprint = bridge.GetComponent<Constructable>() != null,
				Connections = IsVertical(bridge)
					? UtilityConnections.Up | UtilityConnections.Down
					: UtilityConnections.Left | UtilityConnections.Right,
			};
		}

		/// <summary>
		/// Directions toward the middles of bridges that end in this cell.
		/// A blueprint bridge is not part of a built line yet, nor a built
		/// bridge part of a planned one.
		/// </summary>
		private UtilityConnections BridgeEndDirections(int cell, bool blueprint) {
			var result = (UtilityConnections)0;
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null || !Sections.ConduitSection.IsBridgeEndpoint(go))
					continue;
				if ((go.GetComponent<Constructable>() != null) != blueprint)
					continue;
				result |= Sections.ConduitSection.GetBridgeDirection(go, cell);
			}
			return result;
		}

		/// <summary>
		/// Bridges are registered on the grid at their two ends only, so
		/// the bridge spanning a cell is found from the cells beside it.
		/// </summary>
		private UnityEngine.GameObject FindBridgeSpanning(int cell) {
			int x = Grid.CellColumn(cell);
			int y = Grid.CellRow(cell);
			return BridgeCenteredOn(cell, x - 1, y)
				?? BridgeCenteredOn(cell, x + 1, y)
				?? BridgeCenteredOn(cell, x, y - 1)
				?? BridgeCenteredOn(cell, x, y + 1);
		}

		private UnityEngine.GameObject BridgeCenteredOn(int cell, int x, int y) {
			int neighbor = Grid.XYToCell(x, y);
			if (!Grid.IsValidCell(neighbor)) return null;
			foreach (int layer in _layers) {
				var go = Grid.Objects[neighbor, layer];
				if (go != null && Sections.ConduitSection.IsBridgeEndpoint(go)
					&& Grid.PosToCell(go.transform.GetPosition()) == cell)
					return go;
			}
			return null;
		}

		/// <summary>
		/// True when a building's port is on this cell. Conduit and power
		/// ports register on the connection layer; automation ports are
		/// only known to the circuit system as endpoints. Bridge ends
		/// register the same ways and are not ports.
		/// </summary>
		private bool HasPort(int cell) {
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null) continue;
				if (Sections.ConduitSection.IsBridgeEndpoint(go)) return false;
				if (Sections.ConduitSection.IsPortRegistration(go, layer))
					return true;
			}
			return _getManager().GetEndpoint(cell) != null;
		}

		// Unrotated, every bridge runs left to right
		private static bool IsVertical(UnityEngine.GameObject bridge) {
			var orientation = bridge.GetComponent<Building>().Orientation;
			return orientation == Orientation.R90
				|| orientation == Orientation.R270;
		}
	}
}
