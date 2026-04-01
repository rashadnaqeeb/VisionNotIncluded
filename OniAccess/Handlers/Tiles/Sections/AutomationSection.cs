using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Reads automation infrastructure (wires, gates) at a cell.
	/// </summary>
	public class AutomationSection: ICellSection {
		private static readonly int[] _layers = {
			(int)ObjectLayer.LogicWire, (int)ObjectLayer.LogicGate
		};

		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();
			var bridgeConnections = (UtilityConnections)0;
			LogicWire wire = null;
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null || !ctx.Claimed.Add(go)) continue;
				if (ConduitSection.IsBridgeEndpoint(go)) {
					if (go.GetComponent<Constructable>() != null) {
						var bsel = go.GetComponent<KSelectable>();
						if (bsel != null)
							tokens.Add(ConduitSection.ConstructionName(go, bsel));
					} else {
						bridgeConnections |= ConduitSection.GetBridgeDirection(
							go, cell);
					}
					continue;
				}
				if (wire == null)
					wire = go.GetComponent<LogicWire>();
				var sel = go.GetComponent<KSelectable>();
				if (sel != null)
					tokens.Add(ConduitSection.ConstructionName(go, sel));
			}
			var repGo = Grid.Objects[cell, (int)ObjectLayer.ReplacementLogicWire];
			if (repGo != null) {
				var repSel = repGo.GetComponent<KSelectable>();
				if (repSel != null)
					tokens.Add(string.Format(
						(string)STRINGS.ONIACCESS.GLANCE.REPLACING_WITH,
						repSel.GetName()));
			}
			if (tokens.Count > 0) {
				if (!ConfigManager.Config.PipeShapeEarcons) {
					var conn = ConduitSection.FormatConnections(
						Game.Instance.logicCircuitSystem
							.GetConnections(cell, true)
						| bridgeConnections);
					if (conn != null)
						tokens.Add(conn);
				}
				if (wire != null) {
					var signal = FormatSignal(cell, wire);
					if (signal != null)
						tokens.Add(signal);
				}
			}
			ConduitSection.FindBridgeMiddle(cell, _layers, ctx, tokens);
			return tokens;
		}

		private static string FormatSignal(int cell, LogicWire wire) {
			var network = Game.Instance.logicCircuitManager
				.GetNetworkForCell(cell) as LogicCircuitNetwork;
			if (network == null)
				return null;
			if (wire.MaxBitDepth == LogicWire.BitDepth.OneBit)
				return BitLabel(network.IsBitActive(0));
			var bits = new string[4];
			for (int i = 0; i < 4; i++)
				bits[i] = BitLabel(network.IsBitActive(i));
			return string.Join(" ", bits);
		}

		private static string BitLabel(bool active) {
			return active
				? STRINGS.UI.OVERLAYS.LOGIC.ONE
				: STRINGS.UI.OVERLAYS.LOGIC.ZERO;
		}
	}
}
