using System.Collections.Generic;
using OniAccess.ConduitTracking;
using OniAccess.Handlers.Tiles.Scanner.Routing;
using UnityEngine;

namespace OniAccess.Handlers.Tiles.Scanner.Backends {
	/// <summary>
	/// Backend for the Warnings category: problems a sighted player spots
	/// by their red icons. Most are the game's own status items, read off
	/// buildings, critters and plants. The rest are network faults the
	/// game shows no status for (Flow Conflict, overloaded circuits, mixed
	/// wire ratings, dead batteries, pipe networks nothing drains).
	/// A status on a pipe or wire is grouped per network so a long run
	/// reads as one entry, and network faults are one entry per network.
	/// </summary>
	public class WarningBackend: IScannerBackend, IGridConsumerBackend {
		/// <summary>
		/// One warning. Either on a GameObject (a building, critter or
		/// plant) or across a set of cells (a stretch of one network).
		/// </summary>
		private sealed class Warning {
			// Groups instances into one item; free of status placeholders
			public string Name;
			// The warning as the game words it on this instance, with its
			// details filled in; null to speak Name
			public System.Func<int, string> SpokenName;
			public GameObject Go;
			public List<int> Cells;
			// True while the warning still holds at the cell. For a
			// network fault it only says the line is still there; Holds
			// then tests the network once at the chosen cell.
			public System.Func<int, bool> HoldsAt;
			public System.Func<int, bool> Holds;
			// What carries the warning, read at speech time from the cell
			// pointed to and the number of cells the warning covers
			public System.Func<int, int, string> Describe;
		}

		private static Dictionary<StatusItem, string> _buildingStatuses;
		private static Dictionary<StatusItem, string> _lifeStatuses;
		private Dictionary<int, List<int>>[] _networkCells;

		public void SetGridData(GridScanResult grid) {
			_networkCells = grid.NetworkCells;
		}

		public IEnumerable<ScanEntry> Scan(int worldId) {
			EnsureStatusTables();
			var warnings = new List<(Warning, string)>();
			ScanBuildingStatuses(worldId, warnings);
			ScanLifeStatuses(worldId, warnings);
			ScanCircuits(warnings);
			ScanBatteries(worldId, warnings);
			ScanFlowNetworks(warnings);
			foreach (var (warning, subcategory) in warnings) {
				yield return new ScanEntry {
					Cell = warning.Go != null
						? Grid.PosToCell(warning.Go.transform.GetPosition())
						: warning.Cells[0],
					Backend = this,
					BackendData = warning,
					Category = ScannerTaxonomy.Categories.Warnings,
					Subcategory = subcategory,
					ItemName = warning.Name,
				};
			}
		}

		public bool ValidateEntry(ScanEntry entry, int cursorCell) {
			var warning = (Warning)entry.BackendData;
			if (warning.Go != null) {
				if (warning.Go.IsNullOrDestroyed()) return false;
				int cell = Grid.PosToCell(warning.Go.transform.GetPosition());
				if (!Grid.IsVisible(cell) || !warning.HoldsAt(cell)) return false;
				entry.Cell = cell;
				return true;
			}
			if (!GridUtil.ValidateCluster(warning.Cells, cursorCell, entry,
					cell => Grid.IsVisible(cell) && warning.HoldsAt(cell)))
				return false;
			return warning.Holds == null || warning.Holds(entry.Cell);
		}

		public string FormatName(ScanEntry entry) {
			var warning = (Warning)entry.BackendData;
			int count = warning.Cells != null ? warning.Cells.Count : 1;
			string name = warning.SpokenName?.Invoke(entry.Cell);
			if (string.IsNullOrEmpty(name)) name = warning.Name;
			return string.Format((string)STRINGS.ONIACCESS.SCANNER.WARNINGS.LABEL,
				name, warning.Describe(entry.Cell, count));
		}

		// -------------------------------------------------------------------
		// Status items
		// -------------------------------------------------------------------

		private static void EnsureStatusTables() {
			if (_buildingStatuses != null) return;
			var b = Db.Get().BuildingStatusItems;
			_buildingStatuses = new Dictionary<StatusItem, string> {
				{ b.Overheated, ScannerTaxonomy.Subcategories.Damage },
				{ b.Broken, ScannerTaxonomy.Subcategories.Damage },
				{ b.Entombed, ScannerTaxonomy.Subcategories.Damage },
				{ b.Flooded, ScannerTaxonomy.Subcategories.Damage },
				{ b.LiquidPipeFrozenDamaged, ScannerTaxonomy.Subcategories.Damage },
				{ b.LiquidPipeBoiledDamaged, ScannerTaxonomy.Subcategories.Damage },
				{ b.GasPipeCondensedDamaged, ScannerTaxonomy.Subcategories.Damage },
				{ b.PipeMayMelt, ScannerTaxonomy.Subcategories.Damage },
				{ b.LogicOverloaded, ScannerTaxonomy.Subcategories.Damage },
				{ b.NeedPower, ScannerTaxonomy.Subcategories.Power },
				{ b.NotEnoughPower, ScannerTaxonomy.Subcategories.Power },
				{ b.NoWireConnected, ScannerTaxonomy.Subcategories.Power },
				{ b.WireDisconnected, ScannerTaxonomy.Subcategories.Power },
				{ b.PowerLoopDetected, ScannerTaxonomy.Subcategories.Power },
				{ b.NeedLiquidOut, ScannerTaxonomy.Subcategories.Plumbing },
				{ b.NeedGasOut, ScannerTaxonomy.Subcategories.Plumbing },
				{ b.NeedSolidOut, ScannerTaxonomy.Subcategories.Plumbing },
				{ b.OutputPipeFull, ScannerTaxonomy.Subcategories.Plumbing },
				{ b.MissingFoundation, ScannerTaxonomy.Subcategories.Placement },
				{ b.MissingFoundationBackwall, ScannerTaxonomy.Subcategories.Placement },
				{ b.InvalidPortOverlap, ScannerTaxonomy.Subcategories.Placement },
			};
			var c = Db.Get().CreatureStatusItems;
			_lifeStatuses = new Dictionary<StatusItem, string> {
				{ c.WiltingDomestic, ScannerTaxonomy.Subcategories.Living },
				{ c.Entombed, ScannerTaxonomy.Subcategories.Living },
				{ c.Drowning, ScannerTaxonomy.Subcategories.Living },
				{ c.Scalding, ScannerTaxonomy.Subcategories.Living },
			};
		}

		private static void ScanBuildingStatuses(int worldId,
				List<(Warning, string)> warnings) {
			// A status on a line piece is grouped with the same status on
			// the rest of its network, keyed by status, layer and network
			var lineGroups = new Dictionary<(StatusItem, int, int), Warning>();
			foreach (var building in Components.BuildingCompletes.GetWorldItems(worldId)) {
				var go = building.gameObject;
				int cell = Grid.PosToCell(go.transform.GetPosition());
				if (!Grid.IsVisible(cell)) continue;
				foreach (var status in StatusesOf(go, _buildingStatuses)) {
					string subcategory = _buildingStatuses[status];
					if (building.Def.isUtility
							&& TryLineNetwork(building, cell, out int layer, out int networkId)) {
						var key = (status, layer, networkId);
						if (!lineGroups.TryGetValue(key, out var group)) {
							group = LineStatusWarning(status, layer);
							lineGroups[key] = group;
							warnings.Add((group, subcategory));
						}
						group.Cells.Add(cell);
						continue;
					}
					warnings.Add((ObjectStatusWarning(go, status), subcategory));
				}
			}
		}

		private static void ScanLifeStatuses(int worldId,
				List<(Warning, string)> warnings) {
			foreach (var brain in Components.Brains.GetWorldItems(worldId)) {
				if (brain.GetComponent<CreatureBrain>() == null) continue;
				AddObjectStatuses(brain.gameObject, warnings);
			}
			foreach (var plant in Components.Uprootables.GetWorldItems(worldId))
				AddObjectStatuses(plant.gameObject, warnings);
		}

		private static void AddObjectStatuses(GameObject go,
				List<(Warning, string)> warnings) {
			if (!Grid.IsVisible(Grid.PosToCell(go.transform.GetPosition()))) return;
			foreach (var status in StatusesOf(go, _lifeStatuses))
				warnings.Add((ObjectStatusWarning(go, status), _lifeStatuses[status]));
		}

		private static List<StatusItem> StatusesOf(GameObject go,
				Dictionary<StatusItem, string> table) {
			var found = new List<StatusItem>();
			var group = go.GetComponent<KSelectable>()?.GetStatusItemGroup();
			if (group == null) return found;
			var enumerator = group.GetEnumerator();
			try {
				while (enumerator.MoveNext()) {
					var item = enumerator.Current.item;
					if (table.ContainsKey(item) && !found.Contains(item))
						found.Add(item);
				}
			} finally {
				enumerator.Dispose();
			}
			return found;
		}

		private static Warning ObjectStatusWarning(GameObject go, StatusItem status) {
			return new Warning {
				Name = PlainName(status),
				SpokenName = _ => ResolvedName(go, status),
				Go = go,
				HoldsAt = _ => go.GetComponent<KSelectable>().HasStatusItem(status),
				Describe = (_, __) => go.GetComponent<KSelectable>().GetName(),
			};
		}

		private static Warning LineStatusWarning(StatusItem status, int layer) {
			return new Warning {
				Name = PlainName(status),
				SpokenName = cell => ResolvedName(Grid.Objects[cell, layer], status),
				Cells = new List<int>(),
				HoldsAt = cell => {
					var piece = Grid.Objects[cell, layer];
					return piece != null
						&& piece.GetComponent<KSelectable>().HasStatusItem(status);
				},
				Describe = (cell, count) => DescribeLine(cell, layer, count),
			};
		}

		private static readonly System.Text.RegularExpressions.Regex Placeholder =
			new System.Text.RegularExpressions.Regex(@"\{[^}]*\}");

		/// <summary>
		/// The status's name without the placeholders the game fills per
		/// instance, such as "Growth Halted{Reasons}".
		/// </summary>
		private static string PlainName(StatusItem status) {
			return Placeholder.Replace(status.Name, "").Trim();
		}

		private static string ResolvedName(GameObject go, StatusItem status) {
			var group = go.GetComponent<KSelectable>().GetStatusItemGroup();
			var enumerator = group.GetEnumerator();
			try {
				while (enumerator.MoveNext())
					if (enumerator.Current.item == status)
						return enumerator.Current.GetName();
			} finally {
				enumerator.Dispose();
			}
			return null;
		}

		/// <summary>
		/// The layer and network of a wire, pipe or rail piece. False for
		/// utility buildings that are not line pieces, such as bridges.
		/// </summary>
		private static bool TryLineNetwork(BuildingComplete building, int cell,
				out int layer, out int networkId) {
			layer = (int)building.Def.ObjectLayer;
			networkId = -1;
			foreach (var net in NetworkLayerConfig.Types) {
				if ((int)net.SegmentLayer != layer) continue;
				var network = net.GetManager().GetNetworkForCell(cell);
				if (network == null) return false;
				networkId = network.id;
				return true;
			}
			return false;
		}

		// -------------------------------------------------------------------
		// Power networks
		// -------------------------------------------------------------------

		/// <summary>
		/// The visible cells of each network on a line layer, from the
		/// grid scan. The game keeps no cell list for a pipe network.
		/// </summary>
		private IEnumerable<List<int>> NetworksOn(ObjectLayer layer) {
			for (int i = 0; i < NetworkLayerConfig.Types.Length; i++) {
				if (NetworkLayerConfig.Types[i].SegmentLayer != layer) continue;
				foreach (var cells in _networkCells[i].Values)
					yield return cells;
			}
		}

		private void ScanCircuits(List<(Warning, string)> warnings) {
			int layer = (int)ObjectLayer.Wire;
			foreach (var cells in NetworksOn(ObjectLayer.Wire)) {

				if (IsOverloaded(cells[0]))
					warnings.Add((NetworkWarning(
						(string)STRINGS.MISC.NOTIFICATIONS.CIRCUIT_OVERLOADED.NAME,
						new List<int>(cells), layer, IsOverloaded,
						(cell, count) => string.Format(
							(string)STRINGS.ONIACCESS.GLANCE.WIRE_LOAD_PERCENT,
							LoadPercent(cell))
							+ ", " + DescribeLine(cell, layer, count)),
						ScannerTaxonomy.Subcategories.Damage));

				if (WireTypesOf(cells[0]).Count > 1)
					warnings.Add((NetworkWarning(
						(string)STRINGS.ONIACCESS.SCANNER.WARNINGS.MIXED_WIRES,
						new List<int>(cells), layer,
						cell => WireTypesOf(cell).Count > 1,
						(cell, _) => string.Join(", ", WireTypesOf(cell))),
						ScannerTaxonomy.Subcategories.Power));
			}
		}

		private static bool IsOverloaded(int cell) {
			var manager = Game.Instance.circuitManager;
			ushort circuitId = manager.GetCircuitID(cell);
			if (circuitId == ushort.MaxValue) return false;
			float max = manager.GetMaxSafeWattageForCircuit(circuitId);
			return max > 0f && manager.GetWattsUsedByCircuit(circuitId)
				> max + TUNING.POWER.FLOAT_FUDGE_FACTOR;
		}

		private static int LoadPercent(int cell) {
			var manager = Game.Instance.circuitManager;
			ushort circuitId = manager.GetCircuitID(cell);
			float max = manager.GetMaxSafeWattageForCircuit(circuitId);
			if (max <= 0f) return 0;
			return (int)(manager.GetWattsUsedByCircuit(circuitId) / max * 100f);
		}

		/// <summary>
		/// Names of the wire and wire bridge types on the circuit at cell,
		/// one per wattage rating, weakest first. Bridges are found where
		/// they land on the circuit's wires.
		/// </summary>
		private static List<string> WireTypesOf(int cell) {
			var names = new List<string>();
			if (!(Game.Instance.electricalConduitSystem.GetNetworkForCell(cell)
					is ElectricalUtilityNetwork circuit))
				return names;
			var byRating = new SortedDictionary<Wire.WattageRating, string>();
			foreach (var wire in circuit.allWires) {
				if (wire == null) continue;
				if (!byRating.ContainsKey(wire.MaxWattageRating))
					byRating[wire.MaxWattageRating] = wire.GetComponent<KSelectable>().GetName();
				int wireCell = Grid.PosToCell(wire.transform.GetPosition());
				var bridgeGo = Grid.Objects[wireCell, (int)ObjectLayer.WireConnectors];
				var bridge = bridgeGo != null
					? bridgeGo.GetComponent<WireUtilityNetworkLink>() : null;
				if (bridge != null && !byRating.ContainsKey(bridge.GetMaxWattageRating()))
					byRating[bridge.GetMaxWattageRating()] = bridge.GetComponent<KSelectable>().GetName();
			}
			names.AddRange(byRating.Values);
			return names;
		}

		private static void ScanBatteries(int worldId, List<(Warning, string)> warnings) {
			foreach (var battery in Components.Batteries.GetWorldItems(worldId)) {
				// Transformers carry a Battery for their input side
				if (battery.GetComponent<PowerTransformer>() != null) continue;
				var go = battery.gameObject;
				if (!Grid.IsVisible(Grid.PosToCell(go.transform.GetPosition()))) continue;
				if (!IsDead(battery)) continue;
				warnings.Add((new Warning {
					Name = (string)STRINGS.ONIACCESS.SCANNER.WARNINGS.DEAD_BATTERY,
					Go = go,
					HoldsAt = _ => IsDead(battery),
					Describe = (_, __) => go.GetComponent<KSelectable>().GetName(),
				}, ScannerTaxonomy.Subcategories.Power));
			}
		}

		private static bool IsDead(Battery battery) {
			return battery.JoulesAvailable < 1f;
		}

		// -------------------------------------------------------------------
		// Pipe and rail networks
		// -------------------------------------------------------------------

		private struct FlowNetworkType {
			public System.Func<IUtilityNetworkMgr> GetManager;
			public ObjectLayer Layer;
			// Null for rails, which the game never marks with Flow Conflict
			public System.Func<ConduitFlow> GetFlow;
			public System.Func<string> NoIntakeName;
		}

		private static readonly FlowNetworkType[] FlowNetworks = {
			new FlowNetworkType {
				GetManager = () => Game.Instance.liquidConduitSystem,
				Layer = ObjectLayer.LiquidConduit,
				GetFlow = () => Game.Instance.liquidConduitFlow,
				NoIntakeName = () => Db.Get().BuildingStatusItems.LiquidPipeNetworkNoSink.Name,
			},
			new FlowNetworkType {
				GetManager = () => Game.Instance.gasConduitSystem,
				Layer = ObjectLayer.GasConduit,
				GetFlow = () => Game.Instance.gasConduitFlow,
				NoIntakeName = () => Db.Get().BuildingStatusItems.GasPipeNetworkNoSink.Name,
			},
			new FlowNetworkType {
				GetManager = () => Game.Instance.solidConduitSystem,
				Layer = ObjectLayer.SolidConduit,
				GetFlow = null,
				NoIntakeName = () => (string)STRINGS.ONIACCESS.SCANNER.WARNINGS.CONVEYOR_NO_INTAKE,
			},
		};

		private void ScanFlowNetworks(List<(Warning, string)> warnings) {
			foreach (var type in FlowNetworks) {
				var manager = type.GetManager();
				int layer = (int)type.Layer;
				foreach (var cells in NetworksOn(type.Layer)) {

					if (IsIsolated(manager, cells[0]))
						warnings.Add((NetworkWarning(
							(string)STRINGS.ONIACCESS.SCANNER.WARNINGS.NO_INPUT_OR_OUTPUT,
							new List<int>(cells), layer,
							cell => IsIsolated(manager, cell),
							(cell, count) => DescribeLine(cell, layer, count)),
							ScannerTaxonomy.Subcategories.Plumbing));
					else if (HasNoIntake(manager, cells[0]))
						warnings.Add((NetworkWarning(
							type.NoIntakeName(),
							new List<int>(cells), layer,
							cell => HasNoIntake(manager, cell),
							(cell, count) => DescribeLine(cell, layer, count)),
							ScannerTaxonomy.Subcategories.Plumbing));

					if (type.GetFlow == null) continue;
					var flow = type.GetFlow();
					var conflicts = cells.FindAll(c => FlowRouting.IsFlowConflict(flow, c));
					if (conflicts.Count > 0)
						warnings.Add((new Warning {
							Name = (string)STRINGS.UI.OVERLAYS.CONDUITFLOW.CONFLICTED,
							Cells = conflicts,
							HoldsAt = c => FlowRouting.IsFlowConflict(flow, c),
							Describe = (cell, count) => DescribeLine(cell, layer, count),
						}, ScannerTaxonomy.Subcategories.Damage));
				}
			}
		}

		private static FlowUtilityNetwork FlowNetworkAt(IUtilityNetworkMgr manager, int cell) {
			return manager.GetNetworkForCell(cell) as FlowUtilityNetwork;
		}

		private static bool IsIsolated(IUtilityNetworkMgr manager, int cell) {
			var network = FlowNetworkAt(manager, cell);
			return network != null && network.sinks.Count == 0 && network.sources.Count == 0;
		}

		private static bool HasNoIntake(IUtilityNetworkMgr manager, int cell) {
			var network = FlowNetworkAt(manager, cell);
			return network != null && network.sinks.Count == 0 && network.sources.Count > 0;
		}

		// -------------------------------------------------------------------
		// Shared helpers
		// -------------------------------------------------------------------

		/// <summary>
		/// A fault of a whole network. Its cells only need the line to
		/// still be there; the fault is tested once on the network at the
		/// cell the scanner points to.
		/// </summary>
		private static Warning NetworkWarning(string name, List<int> cells, int layer,
				System.Func<int, bool> holds, System.Func<int, int, string> describe) {
			return new Warning {
				Name = name,
				Cells = cells,
				HoldsAt = cell => Grid.Objects[cell, layer] != null,
				Holds = holds,
				Describe = describe,
			};
		}

		/// <summary>"Liquid Pipe" for one cell, "12 Liquid Pipe" for more.</summary>
		private static string DescribeLine(int cell, int layer, int count) {
			var piece = Grid.Objects[cell, layer];
			string name = piece != null
				? piece.GetComponent<KSelectable>().GetName()
				: "";
			if (count == 1) return name;
			return string.Format((string)STRINGS.ONIACCESS.SCANNER.CLUSTER_LABEL,
				count, name);
		}
	}
}
