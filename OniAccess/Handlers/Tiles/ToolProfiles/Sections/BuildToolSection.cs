using System.Collections.Generic;
using OniAccess.Handlers.Build;

namespace OniAccess.Handlers.Tiles.ToolProfiles.Sections {
	/// <summary>
	/// Utility line feedback for the build tool cursor. When a utility
	/// start point is set, reports either "invalid" or the cell count.
	/// </summary>
	public class BuildToolSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var handler = BuildToolHandler.Instance;
			if (handler == null || !handler.UtilityStartSet)
				return System.Array.Empty<string>();

			return ReadUtilityLineStatus(cell, handler);
		}

		private static IEnumerable<string> ReadUtilityLineStatus(
				int cell, BuildToolHandler handler) {
			int startCell = handler.UtilityStartCell;
			int startCol = Grid.CellColumn(startCell);
			int startRow = Grid.CellRow(startCell);
			int endCol = Grid.CellColumn(cell);
			int endRow = Grid.CellRow(cell);

			if (startCol != endCol && startRow != endRow)
				return new[] { (string)STRINGS.ONIACCESS.BUILD_MENU.INVALID_LINE };

			if (!BuildToolHandler.IsUtilityLineValid(startCell, cell))
				return new[] { (string)STRINGS.ONIACCESS.BUILD_MENU.INVALID_LINE };

			int count = startRow == endRow
				? System.Math.Abs(endCol - startCol) + 1
				: System.Math.Abs(endRow - startRow) + 1;
			return new[] { string.Format(
				(string)STRINGS.ONIACCESS.BUILD_MENU.LINE_CELLS, count) };
		}
	}

	/// <summary>
	/// Delegates to the conduit section of the active utility overlay, so
	/// the wires, pipes, rails, or automation wire the overlay shows stay
	/// in the readout while a building is being placed. Sensors, valves,
	/// pumps, and every powered machine switch the game to their overlay
	/// without living on a conduit layer, so the overlay decides. Conduit
	/// sensors are the exception: the game shows the automation overlay
	/// while placing them, but they go on a pipe or rail, so that line is
	/// read before the automation wire. With no utility overlay on, falls
	/// back to the layer of the utility being placed. No-op for regular
	/// buildings outside the utility overlays.
	/// </summary>
	public class UtilityLayerSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var handler = BuildToolHandler.Instance;
			if (handler == null || handler._def == null)
				return System.Array.Empty<string>();

			HashedString overlay = OverlayScreen.Instance != null
				? OverlayScreen.Instance.GetMode()
				: OverlayModes.None.ID;
			var tokens = new List<string>();
			var sensorSection = MapSensorToSection(handler._def);
			if (sensorSection != null)
				tokens.AddRange(sensorSection.Read(cell, ctx));
			var section = Resolve(overlay, handler._def.ObjectLayer);
			if (section != null && section != sensorSection)
				tokens.AddRange(section.Read(cell, ctx));
			return tokens;
		}

		/// <summary>
		/// The active utility overlay's conduit section, else the section
		/// for the layer of the utility being placed, else null.
		/// </summary>
		private static ICellSection Resolve(HashedString overlay, ObjectLayer placingLayer) {
			return MapOverlayToSection(overlay) ?? MapDefToSection(placingLayer);
		}

		/// <summary>
		/// The section for the line a conduit sensor monitors, or null when
		/// the building is not a conduit sensor.
		/// </summary>
		private static ICellSection MapSensorToSection(BuildingDef def) {
			var sensor = def.BuildingComplete.GetComponent<ConduitSensor>();
			if (sensor == null) return null;
			switch (sensor.conduitType) {
				case ConduitType.Gas: return GlanceComposer.Ventilation;
				case ConduitType.Liquid: return GlanceComposer.Plumbing;
				case ConduitType.Solid: return GlanceComposer.Conveyor;
				default: return null;
			}
		}

		private static ICellSection MapOverlayToSection(HashedString mode) {
			if (mode == OverlayModes.Power.ID) return GlanceComposer.Power;
			if (mode == OverlayModes.GasConduits.ID) return GlanceComposer.Ventilation;
			if (mode == OverlayModes.LiquidConduits.ID) return GlanceComposer.Plumbing;
			if (mode == OverlayModes.SolidConveyor.ID) return GlanceComposer.Conveyor;
			if (mode == OverlayModes.Logic.ID) return GlanceComposer.Automation;
			return null;
		}

		private static ICellSection MapDefToSection(ObjectLayer layer) {
			switch (layer) {
				case ObjectLayer.Wire:
				case ObjectLayer.WireConnectors:
					return GlanceComposer.Power;
				case ObjectLayer.GasConduit:
				case ObjectLayer.GasConduitConnection:
					return GlanceComposer.Ventilation;
				case ObjectLayer.LiquidConduit:
				case ObjectLayer.LiquidConduitConnection:
					return GlanceComposer.Plumbing;
				case ObjectLayer.SolidConduit:
				case ObjectLayer.SolidConduitConnection:
					return GlanceComposer.Conveyor;
				case ObjectLayer.LogicWire:
				case ObjectLayer.LogicGate:
					return GlanceComposer.Automation;
				default: return null;
			}
		}
	}

	/// <summary>
	/// Reads the construction priority of a pending build order at the
	/// cursor cell. Lets the player check what priority their queued
	/// buildings have while the build tool is active. A pending line is
	/// only read when a conduit section has already named it, so a
	/// priority is never spoken without the order it belongs to.
	/// </summary>
	public class BuildPrioritySection: ICellSection {
		private static readonly int[] _layers = {
			(int)ObjectLayer.Building,
			(int)ObjectLayer.FoundationTile,
			(int)ObjectLayer.Wire,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.SolidConduit,
			(int)ObjectLayer.LogicWire,
		};

		public IEnumerable<string> Read(int cell, CellContext ctx) {
			foreach (int layer in _layers) {
				var go = Grid.Objects[cell, layer];
				if (go == null) continue;
				bool isLine = layer != (int)ObjectLayer.Building
					&& layer != (int)ObjectLayer.FoundationTile;
				if (isLine && !ctx.Claimed.Contains(go)) continue;

				var constructable = go.GetComponent<Constructable>();
				if (constructable == null) continue;

				var prioritizable = go.GetComponent<Prioritizable>();
				if (prioritizable == null) continue;

				return new[] { Widgets.PriorityWidget.FormatPriority(
					prioritizable.GetMasterPriority()) };
			}
			return System.Array.Empty<string>();
		}
	}
}
