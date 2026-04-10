namespace OniAccess.Handlers.Tiles.Skip {
	/// <summary>
	/// Walks cells in a direction until the active strategy's signature
	/// changes. Handles unexplored cells and world boundaries.
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
				return SkipCore(direction, _registry.GetStrategy(mode));
			} catch (System.Exception ex) {
				Util.Log.Error($"SkipEngine.Skip: {ex}");
				return (string)STRINGS.ONIACCESS.SKIP.NO_CHANGE_BOUNDARY;
			}
		}

		public string SkipDefault(Direction direction) {
			try {
				return SkipCore(direction, _coarse);
			} catch (System.Exception ex) {
				Util.Log.Error($"SkipEngine.SkipDefault: {ex}");
				return (string)STRINGS.ONIACCESS.SKIP.NO_CHANGE_BOUNDARY;
			}
		}

		private string SkipCore(Direction direction, ISkipStrategy strategy) {
			var cursor = TileCursor.Instance;
			int startCell = cursor.Cell;
			bool startedUnexplored = !Grid.IsVisible(startCell);
			object startSignature = strategy.GetSignature(startCell);

			int current = startCell;
			int steps = 0;
			while (true) {
				if (TileCursor.IsAtWorldEdge(current, direction))
					break;
				int next = TileCursor.GetNeighbor(current, direction);
				if (next == Grid.InvalidCell || !TileCursor.IsInWorldBounds(next))
					break;

				steps++;
				current = next;

				var ruler = CursorRuler.Instance;
				if (ruler.IsOnRulerLine(current)
					&& !ruler.IsOnRulerLine(startCell)) {
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
					object sig = strategy.GetSignature(current);
					if (!object.Equals(startSignature, sig)) {
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
