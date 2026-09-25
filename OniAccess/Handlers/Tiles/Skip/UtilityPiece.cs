namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// The piece of a utility line a skip sees in one cell: a pipe, wire,
	/// or rail with its connections, or the middle of a bridge.
	/// </summary>
	internal struct UtilityPiece {
		public bool Present;
		public bool Blueprint;
		// Set on blueprint pipes only; blueprints have no network, so their
		// type is what separates one planned run from the next
		public Tag Prefab;
		public Tag Replacement;
		// Includes the directions of bridges that end in this cell
		public UtilityConnections Connections;
		public bool Junction;

		public static UtilityPiece None => default;

		/// <summary>
		/// True when a skip moving in direction carries on from one piece
		/// into the next. It stops on entering a junction, at the edges of
		/// blueprint and replacement runs, and where the line does not
		/// connect across the step. Leaving a junction is not a stop.
		/// </summary>
		public static bool Continues(UtilityPiece from, UtilityPiece to,
				Direction direction) {
			if (!from.Present && !to.Present) return true;
			if (!from.Present || !to.Present) return false;
			if (to.Junction) return false;
			if (from.Blueprint != to.Blueprint) return false;
			if (from.Prefab.IsValid && to.Prefab.IsValid
				&& from.Prefab != to.Prefab)
				return false;
			if (from.Replacement != to.Replacement) return false;
			return (from.Connections & Toward(direction)) != 0
				&& (to.Connections & Toward(Opposite(direction))) != 0;
		}

		public static int CountDirections(UtilityConnections connections) {
			int count = 0;
			if ((connections & UtilityConnections.Up) != 0) count++;
			if ((connections & UtilityConnections.Down) != 0) count++;
			if ((connections & UtilityConnections.Left) != 0) count++;
			if ((connections & UtilityConnections.Right) != 0) count++;
			return count;
		}

		private static UtilityConnections Toward(Direction direction) {
			switch (direction) {
				case Direction.Up: return UtilityConnections.Up;
				case Direction.Down: return UtilityConnections.Down;
				case Direction.Left: return UtilityConnections.Left;
				default: return UtilityConnections.Right;
			}
		}

		private static Direction Opposite(Direction direction) {
			switch (direction) {
				case Direction.Up: return Direction.Down;
				case Direction.Down: return Direction.Up;
				case Direction.Left: return Direction.Right;
				default: return Direction.Left;
			}
		}
	}
}
