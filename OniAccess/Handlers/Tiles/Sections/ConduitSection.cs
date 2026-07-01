using System;
using System.Collections.Generic;
using OniAccess.ConduitTracking;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Reads conduit/wire infrastructure at a cell. Skips buildings that
	/// are registered on a layer only for port tracking (handled by
	/// BuildingSection). Parameterized by the object layers to scan.
	/// </summary>
	public class ConduitSection: ICellSection {
		private readonly int[] _layers;
		private readonly int _replacementLayer;
		private readonly Func<IUtilityNetworkMgr> _getManager;
		private readonly Func<FlowTracker> _getFlowTracker;
		private readonly Func<int, int> _getConduitIdx;
		private readonly Func<int, bool> _isConduitEmpty;

		public ConduitSection(Func<IUtilityNetworkMgr> getManager,
				ObjectLayer replacementLayer,
				params int[] layers)
			: this(getManager, null, null, null, replacementLayer, layers) {
		}

		public ConduitSection(Func<IUtilityNetworkMgr> getManager,
				Func<FlowTracker> getFlowTracker,
				Func<int, int> getConduitIdx,
				Func<int, bool> isConduitEmpty,
				ObjectLayer replacementLayer,
				params int[] layers) {
			_getManager = getManager;
			_getFlowTracker = getFlowTracker;
			_getConduitIdx = getConduitIdx;
			_isConduitEmpty = isConduitEmpty;
			_replacementLayer = (int)replacementLayer;
			_layers = layers;
		}

		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();
			var bridgeConnections = (UtilityConnections)0;
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null || ctx.Claimed.Contains(go)) continue;
				if (IsPortRegistration(go, layer)) continue;
				if (IsBridgeEndpoint(go)) {
					if (go.GetComponent<Constructable>() != null) {
						ctx.Claimed.Add(go);
						var bsel = go.GetComponent<KSelectable>();
						if (bsel != null)
							tokens.Add(ConstructionName(go, bsel));
					} else {
						bridgeConnections |= GetBridgeDirection(go, cell);
					}
					continue;
				}
				ctx.Claimed.Add(go);
				var sel = go.GetComponent<KSelectable>();
				if (sel != null)
					tokens.Add(ConstructionName(go, sel));
			}
			var repGo = Grid.Objects[cell, _replacementLayer];
			if (repGo != null) {
				var repSel = repGo.GetComponent<KSelectable>();
				if (repSel != null)
					tokens.Add(string.Format(
						(string)STRINGS.ONIACCESS.GLANCE.REPLACING_WITH,
						repSel.GetName()));
			}
			bridgeConnections |= FindJointPlateConnections(cell);
			if (tokens.Count > 0 && !ConfigManager.Config.PipeShapeEarcons)
				tokens.Add(FormatConnections(
					_getManager().GetConnections(cell, true)
					| bridgeConnections));
			FindBridgeMiddle(cell, _layers, ctx, tokens);
			if (tokens.Count > 0 && ConfigManager.Config.FlowDirectionReadout
					&& _getFlowTracker != null) {
				var flowText = FlowSpeech.Format(
					_getFlowTracker(), _getConduitIdx(cell),
					_isConduitEmpty(cell));
				if (flowText != null)
					tokens.Add(flowText);
			}
			if (tokens.Count > 0 && ConfigManager.Config.FlowDirectionReadout
					&& OverlayScreen.Instance != null
					&& OverlayScreen.Instance.GetMode() == OverlayModes.Power.ID) {
				ushort circuitID = Game.Instance.circuitManager.GetCircuitID(cell);
				if (circuitID != ushort.MaxValue) {
					float wattsUsed = Game.Instance.circuitManager.GetWattsUsedByCircuit(circuitID);
					float maxWatts = Game.Instance.circuitManager.GetMaxSafeWattageForCircuit(circuitID);
					if (maxWatts > 0f) {
						int percent = (int)(wattsUsed / maxWatts * 100f);
						tokens.Add(string.Format(
							(string)STRINGS.ONIACCESS.GLANCE.WIRE_LOAD_PERCENT, percent));
					}
				}
			}
			return tokens;
		}

		internal static UtilityConnections GetBridgeDirection(
				UnityEngine.GameObject go, int cell) {
			var building = go.GetComponent<Building>();
			int origin = Grid.PosToCell(building.transform.GetPosition());
			int dx = Grid.CellColumn(origin) - Grid.CellColumn(cell);
			int dy = Grid.CellRow(origin) - Grid.CellRow(cell);
			if (dx > 0) return UtilityConnections.Right;
			if (dx < 0) return UtilityConnections.Left;
			if (dy > 0) return UtilityConnections.Up;
			return UtilityConnections.Down;
		}

		/// <summary>
		/// Joint plates (HighWattBridgeTile) have link cells outside their
		/// 1x1 footprint that aren't on any conduit layer. Scan adjacent
		/// cells on Building/FoundationTile for WireUtilityNetworkLink
		/// components whose link cells match the cursor, and return the
		/// bridge direction so it's included in the wire shape.
		/// </summary>
		internal static UtilityConnections FindJointPlateConnections(int cell) {
			if (OverlayScreen.Instance == null
				|| OverlayScreen.Instance.GetMode() != OverlayModes.Power.ID)
				return (UtilityConnections)0;

			var result = (UtilityConnections)0;
			int cx = Grid.CellColumn(cell);
			int cy = Grid.CellRow(cell);
			var seen = new HashSet<UnityEngine.GameObject>();

			for (int dy = -1; dy <= 1; dy++) {
				for (int dx = -1; dx <= 1; dx++) {
					if (dx == 0 && dy == 0) continue;
					int nc = Grid.XYToCell(cx + dx, cy + dy);
					if (!Grid.IsValidCell(nc)) continue;

					result |= CheckJointPlateAt(nc, (int)ObjectLayer.Building,
						seen, cell);
					result |= CheckJointPlateAt(nc, (int)ObjectLayer.FoundationTile,
						seen, cell);
				}
			}
			return result;
		}

		private static UtilityConnections CheckJointPlateAt(
				int nearbyCell, int layer,
				HashSet<UnityEngine.GameObject> seen, int targetCell) {
			var go = Grid.Objects[nearbyCell, layer];
			if (go == null || !seen.Add(go)) return (UtilityConnections)0;

			var building = go.GetComponent<Building>();
			if (building == null) return (UtilityConnections)0;
			if (building.Def.BuildLocationRule != BuildLocationRule.HighWattBridgeTile)
				return (UtilityConnections)0;

			var wireLink = go.GetComponent<WireUtilityNetworkLink>();
			if (wireLink == null) return (UtilityConnections)0;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			wireLink.GetCells(origin, building.Orientation,
				out int linkCell1, out int linkCell2);
			if (targetCell != linkCell1 && targetCell != linkCell2)
				return (UtilityConnections)0;

			return GetBridgeDirection(go, targetCell);
		}

		/// <summary>
		/// Bridges (Conduit, WireBridge, LogicBridge build rules) are not
		/// registered on any object layer at their middle cell. Scan
		/// adjacent cells on the given layers for buildings whose
		/// PlacementCells include the current cell.
		/// </summary>
		internal static void FindBridgeMiddle(
				int cell, int[] layers, CellContext ctx,
				List<string> tokens) {
			int cx = Grid.CellColumn(cell);
			int cy = Grid.CellRow(cell);
			foreach (int layer in layers) {
				CheckBridgeNeighbor(Grid.XYToCell(cx - 1, cy),
					layer, cell, ctx, tokens);
				CheckBridgeNeighbor(Grid.XYToCell(cx + 1, cy),
					layer, cell, ctx, tokens);
				CheckBridgeNeighbor(Grid.XYToCell(cx, cy - 1),
					layer, cell, ctx, tokens);
				CheckBridgeNeighbor(Grid.XYToCell(cx, cy + 1),
					layer, cell, ctx, tokens);
			}
		}

		private static void CheckBridgeNeighbor(
				int neighbor, int layer, int targetCell,
				CellContext ctx, List<string> tokens) {
			if (!Grid.IsValidCell(neighbor)) return;
			var go = Grid.Objects[neighbor, layer];
			if (go == null || ctx.Claimed.Contains(go)) return;
			if (!IsBridgeEndpoint(go)) return;
			var building = go.GetComponent<Building>();
			if (!building.PlacementCellsContainCell(targetCell)) return;
			ctx.Claimed.Add(go);
			var sel = go.GetComponent<KSelectable>();
			if (sel != null) {
				string label = string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.BRIDGE_MIDDLE,
					ConstructionName(go, sel));
				tokens.Add(BuildingSection.AddOrientationIfImportant(
					go, building, label));
			}
		}

		/// <summary>
		/// True when the object is a bridge endpoint. Bridge endpoints
		/// are handled by BuildingSection (port labels + name), not here.
		/// </summary>
		internal static bool IsBridgeEndpoint(UnityEngine.GameObject go) {
			var building = go.GetComponent<Building>();
			if (building == null) return false;
			var rule = building.Def.BuildLocationRule;
			return rule == BuildLocationRule.Conduit
				|| rule == BuildLocationRule.WireBridge
				|| rule == BuildLocationRule.LogicBridge
				|| rule == BuildLocationRule.HighWattBridgeTile;
		}

		internal static string ConstructionName(
				UnityEngine.GameObject go, KSelectable sel) {
			string name = sel.GetName();
			if (go.GetComponent<Constructable>() != null)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.UNDER_CONSTRUCTION, name);
			var decon = go.GetComponent<Deconstructable>();
			if (decon != null && decon.IsMarkedForDeconstruction())
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.MARKED_DECONSTRUCTION, name);
			return name;
		}

		/// <summary>
		/// True when a building was registered on this layer for port
		/// tracking rather than being infrastructure that lives here.
		/// </summary>
		internal static bool IsPortRegistration(
				UnityEngine.GameObject go, int layer) {
			var building = go.GetComponent<Building>();
			return building != null
				&& (int)building.Def.ObjectLayer != layer;
		}

		/// <summary>
		/// Formats a UtilityConnections flags value as a shape name
		/// (e.g. "vertical", "up right corner"). Returns "unconnected"
		/// when no connections exist.
		/// </summary>
		internal static string FormatConnections(UtilityConnections connections) {
			bool up = (connections & UtilityConnections.Up) != 0;
			bool down = (connections & UtilityConnections.Down) != 0;
			bool left = (connections & UtilityConnections.Left) != 0;
			bool right = (connections & UtilityConnections.Right) != 0;
			int count = (up ? 1 : 0) + (down ? 1 : 0)
				+ (left ? 1 : 0) + (right ? 1 : 0);

			switch (count) {
				case 0:
					return STRINGS.ONIACCESS.GLANCE.SHAPE_ALONE;
				case 1:
					string dir = up ? STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN
						: down ? STRINGS.ONIACCESS.SCANNER.DIRECTION_UP
						: left ? STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT
						: STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT;
					return string.Format(
						STRINGS.ONIACCESS.GLANCE.SHAPE_END, dir);
				case 2:
					if (up && down)
						return STRINGS.ONIACCESS.GLANCE.SHAPE_VERTICAL;
					if (left && right)
						return STRINGS.ONIACCESS.GLANCE.SHAPE_HORIZONTAL;
					string d1 = up
						? STRINGS.ONIACCESS.SCANNER.DIRECTION_UP
						: STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN;
					string d2 = left
						? STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT
						: STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT;
					return string.Format(
						STRINGS.ONIACCESS.GLANCE.SHAPE_CORNER, d1, d2);
				case 3:
					string branch = !up
						? STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN
						: !down ? STRINGS.ONIACCESS.SCANNER.DIRECTION_UP
						: !left ? STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT
						: STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT;
					return string.Format(
						STRINGS.ONIACCESS.GLANCE.SHAPE_T, branch);
				default:
					return STRINGS.ONIACCESS.GLANCE.SHAPE_CROSS;
			}
		}
	}
}
