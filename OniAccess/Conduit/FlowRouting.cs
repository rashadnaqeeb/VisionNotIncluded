using System.Collections.Generic;

namespace OniAccess.ConduitTracking {
	/// <summary>
	/// Reads the game's routing for a conduit cell: the directions its
	/// network lets contents leave the cell on their way to an intake, and
	/// whether the cell is a Flow Conflict. The game works these out every
	/// time the network rebuilds, so they hold for an empty or blocked pipe
	/// too. Directions come back as a mask of (1 &lt;&lt; FlowTracker.Dir*).
	/// </summary>
	public static class FlowRouting {
		public static int GetFluidDirections(ConduitFlow flow, int cell) {
			var dirs = flow.GetPermittedFlow(cell);
			int mask = 0;
			if ((dirs & ConduitFlow.FlowDirections.Up) != 0)
				mask |= 1 << FlowTracker.DirUp;
			if ((dirs & ConduitFlow.FlowDirections.Down) != 0)
				mask |= 1 << FlowTracker.DirDown;
			if ((dirs & ConduitFlow.FlowDirections.Left) != 0)
				mask |= 1 << FlowTracker.DirLeft;
			if ((dirs & ConduitFlow.FlowDirections.Right) != 0)
				mask |= 1 << FlowTracker.DirRight;
			return mask;
		}

		public static int GetSolidDirections(SolidConduitFlow flow, int cell) {
			int bits = flow.GetPermittedFlow(cell);
			// The game treats -1 as a rail that routes nowhere.
			if (bits < 0) return 0;
			int mask = 0;
			if ((bits & SolidConduitFlow.FlowBit(SolidConduitFlow.FlowDirection.Up)) != 0)
				mask |= 1 << FlowTracker.DirUp;
			if ((bits & SolidConduitFlow.FlowBit(SolidConduitFlow.FlowDirection.Down)) != 0)
				mask |= 1 << FlowTracker.DirDown;
			if ((bits & SolidConduitFlow.FlowBit(SolidConduitFlow.FlowDirection.Left)) != 0)
				mask |= 1 << FlowTracker.DirLeft;
			if ((bits & SolidConduitFlow.FlowBit(SolidConduitFlow.FlowDirection.Right)) != 0)
				mask |= 1 << FlowTracker.DirRight;
			return mask;
		}

		/// <summary>
		/// The game's Flow Conflict test (SelectToolHoverTextCard): the
		/// network has an intake, this cell is not one, and the routing
		/// gives the cell no way out. The game only shows it while the Pipe
		/// Flow filter is on; the mod reads it regardless.
		/// </summary>
		public static bool IsFlowConflict(ConduitFlow flow, int cell) {
			var conduit = flow.GetConduit(cell);
			if (conduit.idx == ConduitFlow.Conduit.Invalid.idx) return false;
			if (!(flow.GetNetwork(conduit) is FlowUtilityNetwork network)
					|| !network.HasSinks)
				return false;
			foreach (var sink in network.sinks)
				if (sink.Cell == cell) return false;
			return conduit.GetPermittedFlowDirections(flow)
				== ConduitFlow.FlowDirections.None;
		}

		/// <summary>
		/// "will flow right", "will flow down and right". Null for an empty
		/// mask.
		/// </summary>
		public static string FormatWillFlow(int mask) {
			var names = new List<string>(4);
			for (int d = FlowTracker.DirUp; d <= FlowTracker.DirRight; d++)
				if ((mask & (1 << d)) != 0)
					names.Add(FlowSpeech.DirectionName(d));
			if (names.Count == 0) return null;
			string list = names[names.Count - 1];
			if (names.Count > 1)
				list = string.Format(
					STRINGS.ONIACCESS.GLANCE.FLOW_DIRECTION_AND,
					string.Join(", ", names.GetRange(0, names.Count - 1)),
					list);
			return string.Format(STRINGS.ONIACCESS.GLANCE.FLOW_WILL_FLOW, list);
		}
	}
}
