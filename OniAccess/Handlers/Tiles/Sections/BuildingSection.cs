using System.Collections.Generic;
using OniAccess.Handlers.Build;
using UnityEngine;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Reads all buildings at a cell across ObjectLayer.Building,
	/// ObjectLayer.FoundationTile, ObjectLayer.Backwall, and
	/// ObjectLayer.AttachableBuilding.
	/// Plants also occupy ObjectLayer.Building.
	///
	/// For each building: utility ports (when overlay active), name,
	/// status items, construction state. Ports come first so the
	/// overlay-specific info is the first thing the player hears.
	/// Door access state comes through status items automatically.
	/// </summary>
	public class BuildingSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var tokens = new List<string>();
			try {
				var buildingGo = Grid.Objects[cell, (int)ObjectLayer.Building];
				var foundationGo = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
				var backwallGo = Grid.Objects[cell, (int)ObjectLayer.Backwall];

				ReadPortCell(cell, buildingGo, foundationGo, ctx, tokens);

				if (buildingGo != null && !ctx.Claimed.Contains(buildingGo))
					ReadBuilding(buildingGo, cell, tokens);
				ReadReplacement(cell, ObjectLayer.ReplacementTravelTube, tokens);

				if (foundationGo != null && foundationGo != buildingGo
					&& !ctx.Claimed.Contains(foundationGo))
					ReadBuilding(foundationGo, cell, tokens);
				ReadReplacement(cell, ObjectLayer.ReplacementTile, tokens);
				ReadReplacement(cell, ObjectLayer.ReplacementLadder, tokens);

				if (!Grid.HasTube[cell])
					ScanForTubeConnections(cell, tokens);

				if (backwallGo != null && !ctx.Claimed.Contains(backwallGo)
					&& !IsOverlayFocused()) {
					var selectable = backwallGo.GetComponent<KSelectable>();
					if (selectable != null) {
						string backwallName = GetBuildingName(backwallGo, selectable);
						if (backwallGo.GetComponent<Constructable>() != null)
							backwallName = string.Format(
								(string)STRINGS.ONIACCESS.GLANCE.UNDER_CONSTRUCTION,
								backwallName);
						else {
							var decon = backwallGo.GetComponent<Deconstructable>();
							if (decon != null && decon.IsMarkedForDeconstruction())
								backwallName = string.Format(
									(string)STRINGS.ONIACCESS.GLANCE.MARKED_DECONSTRUCTION,
									backwallName);
						}
						tokens.Add(backwallName);
						ReadPixelPack(backwallGo, cell, tokens);
					}
				}
				if (!IsOverlayFocused())
					ReadReplacement(cell, ObjectLayer.ReplacementBackwall, tokens);

				var attachableGo = Grid.Objects[cell, (int)ObjectLayer.AttachableBuilding];
				if (attachableGo != null && !ctx.Claimed.Contains(attachableGo))
					ReadBuilding(attachableGo, cell, tokens);
			} catch (System.Exception ex) {
				Util.Log.Error($"BuildingSection.Read: {ex}");
			}
			return tokens;
		}

		private static void ReadBuilding(GameObject go, int cell, List<string> tokens) {
			var selectable = go.GetComponent<KSelectable>();
			if (selectable == null) return;

			var uncoverable = go.GetComponent<Uncoverable>();
			if (uncoverable != null && !uncoverable.IsUncovered) return;

			var building = go.GetComponent<Building>();
			bool isExtension = building != null
				&& !building.PlacementCellsContainCell(cell);

			if (building != null && !isExtension)
				ReadPorts(go, building, cell, tokens);

			string displayName = GetBuildingName(go, selectable);

			if (isExtension) {
				tokens.Add(ExtensionLabel(go, displayName));
				return;
			}

			displayName = AddOrientationIfImportant(go, building, displayName);

			var constructable = go.GetComponent<Constructable>();
			if (constructable != null) {
				tokens.Add(string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.UNDER_CONSTRUCTION,
					displayName));
			} else {
				var deconstructable = go.GetComponent<Deconstructable>();
				if (deconstructable != null && deconstructable.IsMarkedForDeconstruction())
					tokens.Add(string.Format(
						(string)STRINGS.ONIACCESS.GLANCE.MARKED_DECONSTRUCTION,
						displayName));
				else
					tokens.Add(displayName);
			}

			if (Grid.HasTube[cell])
				AppendTubeShape(cell, tokens);

			bool isPlant = go.GetComponent<Growing>() != null;
			ReadStatusItems(selectable, isPlant, tokens);

			if (OverlayScreen.Instance != null
				&& OverlayScreen.Instance.GetMode() == OverlayModes.Temperature.ID) {
				var pe = go.GetComponent<PrimaryElement>();
				if (pe != null) {
					float cellTemp = Grid.Temperature[cell];
					float buildingTemp = pe.Temperature;
					if (cellTemp <= 0f
						|| System.Math.Abs(buildingTemp - cellTemp) >= 1f)
						tokens.Add(GameUtil.GetFormattedTemperature(buildingTemp));
					TemperatureWarnings.AppendPhaseWarnings(pe, tokens);
					TemperatureWarnings.AppendOverheatWarning(go, pe, tokens);
				}
			}

			if (building != null && building.PlacementCells.Length > 1) {
				int origin = Grid.PosToCell(building.transform.GetPosition());
				ReadCellOfInterest(go, building, origin, cell, tokens);
			}
		}

		internal static string AddOrientationIfImportant(
				GameObject go, Building building, string name) {
			if (building == null || building.Def == null)
				return name;
			if (building.Def.PermittedRotations == PermittedRotations.Unrotatable)
				return name;
			if (!ShouldAnnounceOrientation(go))
				return name;

			string orientation = BuildMenuData.GetOrientationName(
				building.Orientation, building.Def);
			return BuildMenuData.AppendOrientation(
				name, orientation, building.Def.PermittedRotations);
		}

		private static bool ShouldAnnounceOrientation(GameObject go) {
			return ConduitSection.IsBridgeEndpoint(go)
				|| go.GetComponent<TravelTubeBridge>() != null
				|| go.GetComponent<SuitMarker>() != null
				|| go.GetComponent<Checkpoint>() != null;
		}

		private static void ReadReplacement(
				int cell, ObjectLayer layer, List<string> tokens) {
			var go = Grid.Objects[cell, (int)layer];
			if (go == null) return;
			var sel = go.GetComponent<KSelectable>();
			if (sel == null) return;
			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.REPLACING_WITH,
				sel.GetName()));
		}

		private static void AppendTubeShape(int cell, List<string> tokens) {
			if (Game.Instance?.travelTubeSystem == null) return;
			var ownBridge = FindOwnBridgeConnections(cell);
			var connections = Game.Instance.travelTubeSystem
				.GetConnections(cell, true)
				| ownBridge;
			if (ownBridge == (UtilityConnections)0)
				connections |= FindTubeLinkConnections(cell, out _);
			tokens.Add(ConduitSection.FormatConnections(connections));
		}

		private static UtilityConnections FindOwnBridgeConnections(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return (UtilityConnections)0;
			var link = go.GetComponent<TravelTubeUtilityNetworkLink>();
			if (link == null || link.visualizeOnly) return (UtilityConnections)0;
			var building = go.GetComponent<Building>();
			if (building == null) return (UtilityConnections)0;
			int origin = Grid.PosToCell(building.transform.GetPosition());
			link.GetCells(origin, building.Orientation,
				out int linkCell1, out int linkCell2);
			return DirectionTo(cell, linkCell1) | DirectionTo(cell, linkCell2);
		}

		private static UtilityConnections DirectionTo(int from, int to) {
			int dx = Grid.CellColumn(to) - Grid.CellColumn(from);
			int dy = Grid.CellRow(to) - Grid.CellRow(from);
			if (dx > 0) return UtilityConnections.Right;
			if (dx < 0) return UtilityConnections.Left;
			if (dy > 0) return UtilityConnections.Up;
			return UtilityConnections.Down;
		}

		private static void ScanForTubeConnections(
				int cell, List<string> tokens) {
			var connections = FindTubeLinkConnections(cell, out string name);
			if (connections == (UtilityConnections)0) return;
			if (name != null)
				tokens.Add(name);
			tokens.Add((string)STRINGS.ONIACCESS.GLANCE.TUBE_CONNECTION);
		}

		private static UtilityConnections FindTubeLinkConnections(
				int cell, out string buildingName) {
			buildingName = null;
			var connections = (UtilityConnections)0;
			int cx = Grid.CellColumn(cell);
			int cy = Grid.CellRow(cell);

			// Entrance: connection cell is at (tx, ty+2)
			int belowCell = Grid.XYToCell(cx, cy - 2);
			if (Grid.IsValidCell(belowCell)) {
				var go = Grid.Objects[belowCell, (int)ObjectLayer.Building];
				if (go != null && go.GetComponent<TravelTubeEntrance>() != null) {
					int tx = (int)go.transform.GetPosition().x;
					int ty = (int)go.transform.GetPosition().y;
					if (tx == cx && ty + 2 == cy) {
						connections |= UtilityConnections.Down;
						if (buildingName == null) {
							var sel = go.GetComponent<KSelectable>();
							if (sel != null)
								buildingName = sel.GetName();
						}
					}
				}
			}

			// Wall bridge: link cells at rotated offsets
			var seen = new HashSet<GameObject>();
			for (int dy = -1; dy <= 1; dy++) {
				for (int dx = -1; dx <= 1; dx++) {
					if (dx == 0 && dy == 0) continue;
					int nc = Grid.XYToCell(cx + dx, cy + dy);
					if (!Grid.IsValidCell(nc)) continue;
					CheckTubeBridgeLink(nc, (int)ObjectLayer.Building,
						seen, cell, ref connections, ref buildingName);
					CheckTubeBridgeLink(nc, (int)ObjectLayer.FoundationTile,
						seen, cell, ref connections, ref buildingName);
				}
			}
			return connections;
		}

		private static void CheckTubeBridgeLink(
				int nearbyCell, int layer, HashSet<GameObject> seen,
				int targetCell, ref UtilityConnections connections,
				ref string buildingName) {
			var go = Grid.Objects[nearbyCell, layer];
			if (go == null || !seen.Add(go)) return;

			var link = go.GetComponent<TravelTubeUtilityNetworkLink>();
			if (link == null || link.visualizeOnly) return;

			var building = go.GetComponent<Building>();
			if (building == null) return;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			link.GetCells(origin, building.Orientation,
				out int linkCell1, out int linkCell2);
			if (targetCell != linkCell1 && targetCell != linkCell2) return;

			connections |= ConduitSection.GetBridgeDirection(go, targetCell);
			if (buildingName == null) {
				var sel = go.GetComponent<KSelectable>();
				if (sel != null)
					buildingName = sel.GetName();
			}
		}

		private static string ExtensionLabel(GameObject go, string buildingName) {
			if (go.GetComponent<LiquidPumpingStation>() != null)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.INTAKE_PIPE, buildingName);
			if (go.GetSMI<WaterTrapTrail.Instance>() != null)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.LURE, buildingName);
			return buildingName;
		}

		/// <summary>
		/// When the cursor is on a port cell that's outside the building's
		/// footprint, the building won't be found on ObjectLayer.Building.
		///
		/// For power and conduit overlays the game registers the building
		/// on port-specific object layers, so a direct lookup works.
		///
		/// For automation and radbolt overlays there is no port layer.
		/// We scan nearby cells on the Building and FoundationTile layers
		/// to find buildings whose ports resolve to the cursor cell.
		/// </summary>
		private static void ReadPortCell(
				int cell, GameObject buildingGo, GameObject foundationGo,
				CellContext ctx, List<string> tokens) {
			if (OverlayScreen.Instance == null) return;
			var activeMode = OverlayScreen.Instance.GetMode();

			if (activeMode == OverlayModes.Logic.ID
				|| activeMode == OverlayModes.Radiation.ID) {
				ScanNearbyForPorts(cell, buildingGo, foundationGo,
					activeMode, ctx, tokens);
				return;
			}

			ObjectLayer portLayer;
			if (activeMode == OverlayModes.Power.ID)
				portLayer = ObjectLayer.WireConnectors;
			else if (activeMode == OverlayModes.LiquidConduits.ID)
				portLayer = ObjectLayer.LiquidConduitConnection;
			else if (activeMode == OverlayModes.GasConduits.ID)
				portLayer = ObjectLayer.GasConduitConnection;
			else if (activeMode == OverlayModes.SolidConveyor.ID)
				portLayer = ObjectLayer.SolidConduitConnection;
			else
				return;

			var portGo = Grid.Objects[cell, (int)portLayer];
			if (portGo == null) {
				if (activeMode == OverlayModes.Power.ID)
					ScanNearbyForWireLinks(cell, buildingGo, foundationGo,
						ctx, tokens);
				return;
			}

			// Already processed through building or foundation layers
			if (portGo == buildingGo || portGo == foundationGo) return;
			if (ctx.Claimed.Contains(portGo)) return;

			var building = portGo.GetComponent<Building>();
			if (building == null) return;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			int beforeCount = tokens.Count;
			ReadOverlayDetails(portGo, building, origin, cell, tokens);

			if (tokens.Count > beforeCount) {
				var selectable = portGo.GetComponent<KSelectable>();
				if (selectable != null)
					tokens.Add(AddOrientationIfImportant(portGo, building,
						GetBuildingName(portGo, selectable)));
			}
		}

		/// <summary>
		/// High-watt joint plates (1x1, HighWattBridgeTile) have link cells
		/// outside their footprint that aren't registered on any object layer.
		/// Scan adjacent cells on Building and FoundationTile for buildings
		/// with WireUtilityNetworkLink whose link cells match the cursor.
		/// </summary>
		private static void ScanNearbyForWireLinks(
				int cell, GameObject buildingGo, GameObject foundationGo,
				CellContext ctx, List<string> tokens) {
			int cx = Grid.CellColumn(cell);
			int cy = Grid.CellRow(cell);
			var seen = new HashSet<GameObject>();
			if (buildingGo != null) seen.Add(buildingGo);
			if (foundationGo != null) seen.Add(foundationGo);

			for (int dy = -1; dy <= 1; dy++) {
				for (int dx = -1; dx <= 1; dx++) {
					if (dx == 0 && dy == 0) continue;
					int nc = Grid.XYToCell(cx + dx, cy + dy);
					if (!Grid.IsValidCell(nc)) continue;

					CheckWireLinkNeighbor(nc, (int)ObjectLayer.Building,
						seen, cell, ctx, tokens);
					CheckWireLinkNeighbor(nc, (int)ObjectLayer.FoundationTile,
						seen, cell, ctx, tokens);
				}
			}
		}

		private static void CheckWireLinkNeighbor(
				int nearbyCell, int layer, HashSet<GameObject> seen,
				int targetCell, CellContext ctx, List<string> tokens) {
			var go = Grid.Objects[nearbyCell, layer];
			if (go == null || !seen.Add(go)) return;

			var building = go.GetComponent<Building>();
			if (building == null) return;

			var wireLink = go.GetComponent<WireUtilityNetworkLink>();
			if (wireLink == null) return;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			wireLink.GetCells(origin, building.Orientation,
				out int linkCell1, out int linkCell2);
			if (targetCell != linkCell1 && targetCell != linkCell2) return;

			tokens.Add((string)STRINGS.ONIACCESS.GLANCE.CONNECTION);
			if (!ctx.Claimed.Contains(go)) {
				var selectable = go.GetComponent<KSelectable>();
				if (selectable != null)
					tokens.Add(AddOrientationIfImportant(go, building,
						GetBuildingName(go, selectable)));
			}
		}

		private const int PortScanRadius = 5;

		private static void ScanNearbyForPorts(
				int cell, GameObject buildingGo, GameObject foundationGo,
				HashedString activeMode, CellContext ctx, List<string> tokens) {
			int cx = Grid.CellColumn(cell);
			int cy = Grid.CellRow(cell);
			var seen = new HashSet<GameObject>();
			if (buildingGo != null) seen.Add(buildingGo);
			if (foundationGo != null) seen.Add(foundationGo);

			for (int dy = -PortScanRadius; dy <= PortScanRadius; dy++) {
				for (int dx = -PortScanRadius; dx <= PortScanRadius; dx++) {
					int nc = Grid.XYToCell(cx + dx, cy + dy);
					if (!Grid.IsValidCell(nc)) continue;

					CheckScanCell(nc, (int)ObjectLayer.Building,
						seen, cell, activeMode, ctx, tokens);
					CheckScanCell(nc, (int)ObjectLayer.FoundationTile,
						seen, cell, activeMode, ctx, tokens);
					if (activeMode == OverlayModes.Logic.ID) {
						CheckScanCell(nc, (int)ObjectLayer.LogicGate,
							seen, cell, activeMode, ctx, tokens);
						CheckScanCell(nc, (int)ObjectLayer.AttachableBuilding,
							seen, cell, activeMode, ctx, tokens);
						CheckScanCell(nc, (int)ObjectLayer.Gantry,
							seen, cell, activeMode, ctx, tokens);
						CheckScanCell(nc, (int)ObjectLayer.Backwall,
							seen, cell, activeMode, ctx, tokens);
					}
				}
			}
		}

		private static void CheckScanCell(
				int nearbyCell, int layer, HashSet<GameObject> seen,
				int targetCell, HashedString activeMode,
				CellContext ctx, List<string> tokens) {
			var go = Grid.Objects[nearbyCell, layer];
			if (go == null || !seen.Add(go)) return;

			var building = go.GetComponent<Building>();
			if (building == null) return;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			int beforeCount = tokens.Count;

			if (activeMode == OverlayModes.Logic.ID)
				ReadAutomationPorts(building, origin, targetCell, tokens);
			else
				ReadRadboltPorts(building, origin, targetCell, tokens);

			if (tokens.Count > beforeCount && !ctx.Claimed.Contains(go)) {
				var selectable = go.GetComponent<KSelectable>();
				if (selectable != null)
					tokens.Add(AddOrientationIfImportant(go, building,
						GetBuildingName(go, selectable)));
			}
		}

		private static void ReadStatusItems(
				KSelectable selectable, bool isPlant, List<string> tokens) {
			var group = selectable.GetStatusItemGroup();
			if (group == null) return;

			var activeOverlay = OverlayScreen.Instance != null
				? OverlayScreen.Instance.GetMode()
				: OverlayModes.None.ID;

			var enumerator = group.GetEnumerator();
			try {
				while (enumerator.MoveNext()) {
					var entry = enumerator.Current;
					if (!StatusFilter.ShouldSpeak(entry.item, activeOverlay, isPlant))
						continue;
					string name = entry.GetName();
					if (!string.IsNullOrEmpty(name))
						tokens.Add(name);
				}
			} finally {
				enumerator.Dispose();
			}
		}

		private static void ReadPorts(
				GameObject go, Building building, int cell, List<string> tokens) {
			int origin = Grid.PosToCell(building.transform.GetPosition());
			ReadOverlayDetails(go, building, origin, cell, tokens);
			ReadAutomationPorts(building, origin, cell, tokens);
			ReadRadboltPorts(building, origin, cell, tokens);
		}

		private static void ReadCellOfInterest(
				GameObject go, Building building, int origin, int cell,
				List<string> tokens) {
			Vector3 originPos = building.transform.GetPosition();

			bool isAccess = (cell == origin);

			int outputCell = origin;
			var fabricator = go.GetComponent<ComplexFabricator>();
			if (fabricator != null && fabricator.outputOffset != Vector3.zero)
				outputCell = Grid.PosToCell(originPos + fabricator.outputOffset);

			var geyser = go.GetComponent<Geyser>();
			if (geyser != null && (geyser.outputOffset.x != 0 || geyser.outputOffset.y != 0))
				outputCell = Grid.OffsetCell(origin,
					new CellOffset(geyser.outputOffset.x, geyser.outputOffset.y));

			var storage = go.GetComponent<Storage>();
			if (storage != null && storage.dropOffset != Vector2.zero)
				outputCell = Grid.PosToCell(
					originPos + new Vector3(storage.dropOffset.x, storage.dropOffset.y, 0f));

			var dispenser = go.GetComponent<ObjectDispenser>();
			if (dispenser != null) {
				var rotatable = go.GetComponent<Rotatable>();
				CellOffset resolved = (rotatable != null)
					? rotatable.GetRotatedCellOffset(dispenser.dropOffset)
					: dispenser.dropOffset;
				outputCell = Grid.OffsetCell(origin, resolved);
			}

			bool isOutput = (cell == outputCell);

			if (!isAccess && !isOutput) return;

			if (isAccess && isOutput) {
				tokens.Add((string)STRINGS.ONIACCESS.GLANCE.TILE_OF_INTEREST);
			} else {
				if (isAccess)
					tokens.Add((string)STRINGS.ONIACCESS.GLANCE.ACCESS_POINT);
				if (isOutput)
					tokens.Add((string)STRINGS.ONIACCESS.GLANCE.OUTPUT_POINT);
			}
		}

		private static void ReadOverlayDetails(
				GameObject go, Building building, int origin, int cell,
				List<string> tokens) {
			if (OverlayScreen.Instance == null) return;

			var activeMode = OverlayScreen.Instance.GetMode();
			var def = building.Def;
			var orientation = building.Orientation;
			var conduitType = OverlayModeToConduitType(activeMode);

			// Count generic (unlabeled) inputs/outputs for numbered fallback.
			// Ports with semantic labels (filtered, overflow, priority) are
			// excluded from the count because they don't need numbering.
			int genericInputs = (def.InputConduitType == conduitType ? 1 : 0)
				+ CountGenericSecondaryPorts<ISecondaryInput>(go, conduitType);
			int genericOutputs = (def.OutputConduitType == conduitType ? 1 : 0)
				+ CountGenericSecondaryPorts<ISecondaryOutput>(go, conduitType);

			// Enumerate inputs. Primary port first, then secondaries.
			int inputOrdinal = 0;
			if (def.InputConduitType != ConduitType.None
				&& def.InputConduitType == conduitType) {
				inputOrdinal++;
				var rotated = Rotatable.GetRotatedCellOffset(
					def.UtilityInputOffset, orientation);
				if (Grid.OffsetCell(origin, rotated) == cell)
					AddPortLabel(ConduitInputLabel(conduitType),
						inputOrdinal, genericInputs, tokens);
			}
			if (conduitType != ConduitType.None) {
				foreach (var sec in go.GetComponents<ISecondaryInput>()) {
					if (!sec.HasSecondaryConduitType(conduitType))
						continue;
					var offset = sec.GetSecondaryConduitOffset(conduitType);
					var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
					string semantic = SemanticInputLabel(sec, conduitType);
					if (semantic != null) {
						if (Grid.OffsetCell(origin, rotated) == cell)
							tokens.Add(semantic);
					} else {
						inputOrdinal++;
						if (Grid.OffsetCell(origin, rotated) == cell)
							AddPortLabel(ConduitInputLabel(conduitType),
								inputOrdinal, genericInputs, tokens);
					}
				}
			}

			// Enumerate outputs. Primary port first, then secondaries.
			int outputOrdinal = 0;
			if (def.OutputConduitType != ConduitType.None
				&& def.OutputConduitType == conduitType) {
				outputOrdinal++;
				var rotated = Rotatable.GetRotatedCellOffset(
					def.UtilityOutputOffset, orientation);
				if (Grid.OffsetCell(origin, rotated) == cell)
					AddPortLabel(ConduitOutputLabel(conduitType),
						outputOrdinal, genericOutputs, tokens);
			}
			if (conduitType != ConduitType.None) {
				foreach (var sec in go.GetComponents<ISecondaryOutput>()) {
					if (!sec.HasSecondaryConduitType(conduitType))
						continue;
					var offset = sec.GetSecondaryConduitOffset(conduitType);
					var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
					string semantic = SemanticOutputLabel(sec, conduitType);
					if (semantic != null) {
						if (Grid.OffsetCell(origin, rotated) == cell)
							tokens.Add(semantic);
					} else {
						outputOrdinal++;
						if (Grid.OffsetCell(origin, rotated) == cell)
							AddPortLabel(ConduitOutputLabel(conduitType),
								outputOrdinal, genericOutputs, tokens);
					}
				}
			}

			// Power ports (never have duplicates, no numbering needed)
			if (activeMode == OverlayModes.Power.ID) {
				if (def.RequiresPowerInput) {
					var rotated = Rotatable.GetRotatedCellOffset(
						def.PowerInputOffset, orientation);
					if (Grid.OffsetCell(origin, rotated) == cell)
						tokens.Add((string)STRINGS.ONIACCESS.GLANCE.POWER_INPUT);
				}
				if (def.RequiresPowerOutput) {
					var rotated = Rotatable.GetRotatedCellOffset(
						def.PowerOutputOffset, orientation);
					if (Grid.OffsetCell(origin, rotated) == cell)
						tokens.Add((string)STRINGS.ONIACCESS.GLANCE.POWER_OUTPUT);
				}
				var wireLink = go.GetComponent<WireUtilityNetworkLink>();
				if (wireLink != null) {
					wireLink.GetCells(origin, orientation,
						out int linkCell1, out int linkCell2);
					if (cell == linkCell1 || cell == linkCell2)
						tokens.Add((string)STRINGS.ONIACCESS.GLANCE.CONNECTION);
				}
			}

			if (activeMode == OverlayModes.TileMode.ID) {
				var pe = go.GetComponent<PrimaryElement>();
				if (pe != null)
					tokens.Add(pe.Element.name);
			}
		}

		private static int CountGenericSecondaryPorts<T>(
				GameObject go, ConduitType conduitType) where T : class {
			if (conduitType == ConduitType.None) return 0;
			int count = 0;
			foreach (var comp in go.GetComponents<T>()) {
				if (comp is ISecondaryInput input
					&& input.HasSecondaryConduitType(conduitType)
					&& SemanticInputLabel(input, conduitType) == null)
					count++;
				else if (comp is ISecondaryOutput output
					&& output.HasSecondaryConduitType(conduitType)
					&& SemanticOutputLabel(output, conduitType) == null)
					count++;
			}
			return count;
		}

		internal static string SemanticOutputLabel(
				ISecondaryOutput port, ConduitType conduitType) {
			if (port is ElementFilter)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.FILTERED_PORT,
					ConduitOutputLabel(conduitType));
			if (port is ConduitOverflow)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.OVERFLOW_PORT,
					ConduitOutputLabel(conduitType));
			return null;
		}

		internal static string SemanticInputLabel(
				ISecondaryInput port, ConduitType conduitType) {
			if (port is ConduitPreferentialFlow)
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.PRIORITY_PORT,
					ConduitInputLabel(conduitType));
			return null;
		}

		private static void AddPortLabel(
				string label, int ordinal, int totalOfKind,
				List<string> tokens) {
			if (totalOfKind <= 1)
				tokens.Add(label);
			else
				tokens.Add(string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.NUMBERED_PORT,
					label, ordinal));
		}

		private static void ReadAutomationPorts(
				Building building, int origin, int cell, List<string> tokens) {
			if (OverlayScreen.Instance == null) return;
			if (OverlayScreen.Instance.GetMode() != OverlayModes.Logic.ID) return;

			var logicPorts = building.GetComponent<LogicPorts>();
			if (logicPorts != null) {
				var orientation = building.Orientation;
				ReadAutomationPortArray(
					logicPorts.inputPortInfo, orientation, origin, cell, tokens);
				ReadAutomationPortArray(
					logicPorts.outputPortInfo, orientation, origin, cell, tokens);
				return;
			}

			var gate = building.GetComponent<LogicGate>();
			if (gate != null && gate.TryGetPortAtCell(cell, out var portId)) {
				var desc = gate.GetPortDescription(portId);
				tokens.Add(desc.name);
			}
		}

		private static void ReadAutomationPortArray(
				LogicPorts.Port[] ports, Orientation orientation,
				int origin, int cell, List<string> tokens) {
			if (ports == null) return;

			// Count how many ports share each description (across all cells)
			var descCounts = new Dictionary<string, int>();
			foreach (var port in ports) {
				string desc = port.description;
				if (descCounts.ContainsKey(desc))
					descCounts[desc]++;
				else
					descCounts[desc] = 1;
			}

			// Track per-description ordinal for numbering
			var descOrdinals = new Dictionary<string, int>();
			foreach (var port in ports) {
				string desc = port.description;
				if (!descOrdinals.ContainsKey(desc))
					descOrdinals[desc] = 1;
				else
					descOrdinals[desc]++;

				var rotated = Rotatable.GetRotatedCellOffset(
					port.cellOffset, orientation);
				if (Grid.OffsetCell(origin, rotated) == cell) {
					if (descCounts[desc] > 1)
						tokens.Add(string.Format(
							(string)STRINGS.ONIACCESS.GLANCE.NUMBERED_PORT,
							desc, descOrdinals[desc]));
					else
						tokens.Add(desc);
				}
			}
		}

		private static void ReadRadboltPorts(
				Building building, int origin, int cell, List<string> tokens) {
			if (OverlayScreen.Instance == null) return;
			if (OverlayScreen.Instance.GetMode() != OverlayModes.Radiation.ID) return;

			var def = building.Def;
			var orientation = building.Orientation;

			if (def.UseHighEnergyParticleInputPort) {
				var rotated = Rotatable.GetRotatedCellOffset(
					def.HighEnergyParticleInputOffset, orientation);
				if (Grid.OffsetCell(origin, rotated) == cell)
					tokens.Add((string)STRINGS.ONIACCESS.GLANCE.RADBOLT_INPUT);
			}
			if (def.UseHighEnergyParticleOutputPort) {
				var rotated = Rotatable.GetRotatedCellOffset(
					def.HighEnergyParticleOutputOffset, orientation);
				if (Grid.OffsetCell(origin, rotated) == cell) {
					var dirComponent = building.gameObject
						.GetComponent<IHighEnergyParticleDirection>();
					if (dirComponent != null) {
						var dirString = EightDirectionToString(dirComponent.Direction);
						tokens.Add(string.Format(
							(string)STRINGS.ONIACCESS.GLANCE.RADBOLT_OUTPUT_DIRECTION, dirString));
					} else {
						tokens.Add((string)STRINGS.ONIACCESS.GLANCE.RADBOLT_OUTPUT);
					}
				}
			}
		}

		private static string EightDirectionToString(EightDirection dir) {
			switch (dir) {
				case EightDirection.Up: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_UP;
				case EightDirection.UpLeft: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_UP_LEFT;
				case EightDirection.Left: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_LEFT;
				case EightDirection.DownLeft: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN_LEFT;
				case EightDirection.Down: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN;
				case EightDirection.DownRight: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_DOWN_RIGHT;
				case EightDirection.Right: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_RIGHT;
				case EightDirection.UpRight: return (string)STRINGS.ONIACCESS.SCANNER.DIRECTION_UP_RIGHT;
				default: return dir.ToString();
			}
		}

		private static ConduitType OverlayModeToConduitType(HashedString mode) {
			if (mode == OverlayModes.GasConduits.ID) return ConduitType.Gas;
			if (mode == OverlayModes.LiquidConduits.ID) return ConduitType.Liquid;
			if (mode == OverlayModes.SolidConveyor.ID) return ConduitType.Solid;
			return ConduitType.None;
		}

		internal static string ConduitInputLabel(ConduitType type) {
			switch (type) {
				case ConduitType.Gas: return (string)STRINGS.ONIACCESS.GLANCE.GAS_INPUT;
				case ConduitType.Liquid: return (string)STRINGS.ONIACCESS.GLANCE.LIQUID_INPUT;
				case ConduitType.Solid: return (string)STRINGS.ONIACCESS.GLANCE.SOLID_INPUT;
				default: return (string)STRINGS.ONIACCESS.GLANCE.INPUT_PORT;
			}
		}

		internal static string ConduitOutputLabel(ConduitType type) {
			switch (type) {
				case ConduitType.Gas: return (string)STRINGS.ONIACCESS.GLANCE.GAS_OUTPUT;
				case ConduitType.Liquid: return (string)STRINGS.ONIACCESS.GLANCE.LIQUID_OUTPUT;
				case ConduitType.Solid: return (string)STRINGS.ONIACCESS.GLANCE.SOLID_OUTPUT;
				default: return (string)STRINGS.ONIACCESS.GLANCE.OUTPUT_PORT;
			}
		}

		private static bool IsOverlayFocused() {
			return OverlayScreen.Instance != null
				&& StatusFilter.IsOverlayFocused(OverlayScreen.Instance.GetMode());
		}

		private static bool IsDecorOverlay() {
			return OverlayScreen.Instance != null
				&& OverlayScreen.Instance.GetMode() == OverlayModes.Decor.ID;
		}

		private static string GetBuildingName(GameObject go, KSelectable selectable) {
			if (IsDecorOverlay())
				return selectable.GetName();
			string name;
			var facade = go.GetComponent<BuildingFacade>();
			if (facade != null && !facade.IsOriginal) {
				var building = go.GetComponent<Building>();
				if (building != null)
					name = building.Def.Name;
				else
					name = selectable.GetName();
			} else {
				name = selectable.GetName();
			}
			var kpid = go.GetComponent<KPrefabID>();
			if (kpid != null && kpid.HasTag(GameTags.PlantBranch))
				name = string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.PLANT_BRANCH, name);
			return name;
		}

		private static void ReadPixelPack(
				GameObject go, int cell, List<string> tokens) {
			var pixelPack = go.GetComponent<PixelPack>();
			if (pixelPack == null || pixelPack.colorSettings == null) return;

			var building = go.GetComponent<Building>();
			if (building == null) return;

			int origin = Grid.PosToCell(building.transform.GetPosition());
			int pixelIndex = -1;
			for (int x = 0; x < 4; x++) {
				var offset = new CellOffset(x, 0);
				var rotated = Rotatable.GetRotatedCellOffset(offset, building.Orientation);
				if (Grid.OffsetCell(origin, rotated) == cell) {
					pixelIndex = x;
					break;
				}
			}
			if (pixelIndex < 0 || pixelIndex >= pixelPack.colorSettings.Count)
				return;

			var logicPorts = go.GetComponent<LogicPorts>();
			int bit = pixelIndex;
			if (logicPorts != null
				&& logicPorts.GetConnectedWireBitDepth(PixelPack.PORT_ID)
					== LogicWire.BitDepth.OneBit)
				bit = 0;

			bool active = LogicCircuitNetwork.IsBitActive(bit, pixelPack.logicValue);
			var colorPair = pixelPack.colorSettings[pixelIndex];
			Color color = active ? colorPair.activeColor : colorPair.standbyColor;

			string state = active
				? (string)STRINGS.ONIACCESS.PIXEL_PACK.ACTIVE
				: (string)STRINGS.ONIACCESS.PIXEL_PACK.STANDBY;
			string colorName = Widgets.ColorNameUtil.GetColorName(color);
			if (colorName != null)
				tokens.Add($"{state}, {colorName}");
			else
				tokens.Add(state);
		}
	}
}
