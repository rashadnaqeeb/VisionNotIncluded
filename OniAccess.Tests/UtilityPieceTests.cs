using System.Collections.Generic;

using OniAccess.Handlers.Tiles.Skip;

namespace OniAccess.Tests {
	using Direction = OniAccess.Handlers.Tiles.Direction;

	/// <summary>
	/// Offline tests for the utility skip's step rule: whether a Ctrl+Arrow
	/// skip in a utility overlay carries on from one cell's piece into the
	/// next. Reading pieces off the grid needs the game; the rule does not.
	/// </summary>
	static class UtilityPieceTests {
		private static (string, bool, string) Check(string name, bool expected, bool actual)
			=> (name, expected == actual, $"expected {expected}, got {actual}");

		private const UtilityConnections Horizontal
			= UtilityConnections.Left | UtilityConnections.Right;

		private static UtilityPiece Built(UtilityConnections connections,
				bool junction = false) {
			return new UtilityPiece {
				Present = true,
				Connections = connections,
				Junction = junction,
			};
		}

		private static UtilityPiece Planned(string prefab,
				UtilityConnections connections = Horizontal) {
			return new UtilityPiece {
				Present = true,
				Blueprint = true,
				Prefab = prefab == null ? default : new Tag(prefab),
				Connections = connections,
			};
		}

		public static IEnumerable<(string, bool, string)> All() {
			yield return Check("StraightLineContinues", true,
				UtilityPiece.Continues(Built(Horizontal), Built(Horizontal),
					Direction.Right));
			// Direction matters: the pieces touch left-right, not up-down
			yield return Check("PerpendicularStepStops", false,
				UtilityPiece.Continues(Built(Horizontal), Built(Horizontal),
					Direction.Up));
			// A pipe ending beside another pipe it does not join
			yield return Check("UnjoinedNeighborStops", false,
				UtilityPiece.Continues(Built(Horizontal),
					Built(UtilityConnections.Right), Direction.Right));
			yield return Check("EnteringJunctionStops", false,
				UtilityPiece.Continues(Built(Horizontal),
					Built(Horizontal | UtilityConnections.Up, junction: true),
					Direction.Right));
			yield return Check("LeavingJunctionContinues", true,
				UtilityPiece.Continues(
					Built(Horizontal | UtilityConnections.Up, junction: true),
					Built(Horizontal), Direction.Right));
			yield return Check("LineIntoEmptyStops", false,
				UtilityPiece.Continues(Built(Horizontal), UtilityPiece.None,
					Direction.Right));
			yield return Check("EmptyIntoEmptyContinues", true,
				UtilityPiece.Continues(UtilityPiece.None, UtilityPiece.None,
					Direction.Right));
			yield return Check("BuiltIntoBlueprintStops", false,
				UtilityPiece.Continues(Built(Horizontal),
					Planned("LiquidConduit"), Direction.Right));
			yield return Check("BlueprintPrefabChangeStops", false,
				UtilityPiece.Continues(Planned("LiquidConduit"),
					Planned("InsulatedLiquidConduit"), Direction.Right));
			// A planned bridge's middle carries no prefab of its own
			yield return Check("BlueprintIntoBlueprintBridgeContinues", true,
				UtilityPiece.Continues(Planned("LiquidConduit"),
					Planned(null), Direction.Right));
			var replaced = Built(Horizontal);
			replaced.Replacement = new Tag("InsulatedLiquidConduit");
			yield return Check("ReplacementEdgeStops", false,
				UtilityPiece.Continues(Built(Horizontal), replaced,
					Direction.Right));
		}
	}
}
