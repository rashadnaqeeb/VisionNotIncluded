namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Walks cells in a direction until the active strategy says to stop:
	/// a line strategy judges each step, a signature strategy stops where
	/// the signature differs from the starting cell's. Handles unexplored
	/// cells and world boundaries.
	/// Returns the speech string with tile count prepended.
	/// </summary>
	public class SkipEngine {
		private readonly SkipStrategyRegistry _registry;
		private readonly ISkipStrategy _coarse = new CoarseSkipStrategy();

		public SkipEngine(SkipStrategyRegistry registry) {
			_registry = registry;
		}

		/// <summary>
		/// Skip from the current cursor position in the given direction.
		/// Returns the speech string to be spoken.
		/// </summary>
		public string Skip(Direction direction) {
			try {
				HashedString mode = OverlayModes.None.ID;
				var overlayScreen = OverlayScreen.Instance;
				if (overlayScreen != null)
					mode = overlayScreen.GetMode();
				var line = _registry.GetLineStrategy(mode);
				if (line != null)
					return SkipCore(direction, (from, to) =>
						line.Continues(from, to, direction));
				return SkipBySignature(direction, _registry.GetStrategy(mode));
			} catch (System.Exception ex) {
				Util.Log.Error($"SkipEngine.Skip: {ex}");
				return (string)STRINGS.ONIACCESS.SKIP.NO_CHANGE_BOUNDARY;
			}
		}

		public string SkipDefault(Direction direction) {
			try {
				return SkipBySignature(direction, _coarse);
			} catch (System.Exception ex) {
				Util.Log.Error($"SkipEngine.SkipDefault: {ex}");
				return (string)STRINGS.ONIACCESS.SKIP.NO_CHANGE_BOUNDARY;
			}
		}

		private string SkipBySignature(Direction direction, ISkipStrategy strategy) {
			object startSignature = strategy.GetSignature(TileCursor.Instance.Cell);
			return SkipCore(direction, (from, to) =>
				object.Equals(startSignature, strategy.GetSignature(to)));
		}

		private string SkipCore(Direction direction,
				System.Func<int, int, bool> continues) {
			var cursor = TileCursor.Instance;
			int startCell = cursor.Cell;
			bool startedUnexplored = !Grid.IsVisible(startCell);

			int current = startCell;
			int steps = 0;
			while (true) {
				if (TileCursor.IsAtWorldEdge(current, direction))
					break;
				int next = TileCursor.GetNeighbor(current, direction);
				if (next == Grid.InvalidCell || !TileCursor.IsInWorldBounds(next))
					break;

				int previous = current;
				steps++;
				current = next;

				if (CursorRuler.Instance.EntersRulerLine(startCell, current)) {
					string cellSpeech = cursor.JumpTo(current);
					return FormatTileCount(steps) + ", " + cellSpeech;
				}

				if (startedUnexplored && Grid.IsVisible(current)) {
					string cellSpeech = cursor.JumpTo(current);
					return FormatTileCount(steps) + ", " + cellSpeech;
				}

				if (!startedUnexplored && !Grid.IsVisible(current)) {
					string speech = cursor.JumpTo(current);
					return FormatTileCount(steps) + ", " + speech;
				}

				if (!startedUnexplored) {
					if (!continues(previous, current)) {
						string cellSpeech = cursor.JumpTo(current);
						return FormatTileCount(steps) + ", " + cellSpeech;
					}
				}
			}

			if (steps == 0)
				return (string)STRINGS.ONIACCESS.SKIP.AT_BOUNDARY;

			string edgeSpeech = cursor.JumpTo(current);
			return FormatTileCount(steps) + ", " + edgeSpeech;
		}

		private static string FormatTileCount(int count) {
			string noun = count == 1
				? (string)STRINGS.ONIACCESS.SKIP.TILE_SINGULAR
				: (string)STRINGS.ONIACCESS.SKIP.TILE_PLURAL;
			return string.Format(
				(string)STRINGS.ONIACCESS.SKIP.COUNT_FORMAT, count, noun);
		}
	}
}
