using System.Collections.Generic;
using UnityEngine;

using OniAccess.Util;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Details {
	/// <summary>
	/// Details-screen section for buildings with a SolidTransferArm
	/// (Auto-Sweeper): lists every building the arm can service, nearest
	/// first, with its priority and coordinates. Enter selects the building,
	/// re-targeting the details screen.
	///
	/// A building qualifies four ways:
	/// 1. Fetch destination or pickup source (see CanServiceBuilding) whose
	///    cell of interest is reachable. The cell of interest is the
	///    building's pivot cell (Grid.PosToCell): the game targets fetch
	///    chores at that cell, so a building whose edge is in range but
	///    whose pivot is not cannot be serviced.
	/// 2. Debris source whose droppings land in reach: conveyor chutes,
	///    automatic dispensers, element droppers (Water Sieve, Compost,
	///    and kin), the Polymerizer, Oxylite Refinery, and Ethanol
	///    Distiller, and fabricator product drop-offs. The drop cell
	///    comes from the same game data the buildings use to spawn
	///    items, then falls straight down to the first floor. Droppers
	///    above the range box are found by scanning each column upward
	///    until a solid tile, and the scans run DropReach columns wider
	///    than the box so sideways drops from outside it are seen.
	/// 3. Robo-Miner whose mining area overlaps the arm's reach: its mined
	///    debris drops where it digs, so the arm collects it.
	/// 4. Any building with a live fetch chore the arm could carry
	///    (repair materials, tinker microchips, and future chore sources),
	///    read from GlobalChoreProvider while the chore is active.
	///
	/// Reachability uses the same check the arm itself uses
	/// (Grid.IsPhysicallyAccessible with blocking tiles visible), so the
	/// list is correct even while the arm is unpowered.
	/// </summary>
	static class SweeperRangeSection {
		private static readonly int[] BuildingLayers = {
			(int)ObjectLayer.Building,
			(int)ObjectLayer.FoundationTile,
		};

		/// <summary>
		/// Furthest cell of an AutoMiner's mining area from its pivot
		/// (x spans -7..8, y spans 0..8, any rotation), used to widen the
		/// discovery scan so miners outside the arm's box are still found.
		/// </summary>
		private const int MinerReach = 8;

		/// <summary>
		/// Furthest horizontal distance a dropper's debris can land from
		/// the dropper's own cells: the dispenser drops 1 cell sideways,
		/// the Ethanol Distiller 2, and the Polymerizer up to 2 with its
		/// shift-away-from-solid fallback. Widens the candidate scans so
		/// droppers just outside the arm's box are still tested.
		/// </summary>
		private const int DropReach = 2;

		public static void Append(GameObject target, List<DetailSection> sections) {
			var arm = target.GetComponent<SolidTransferArm>();
			if (arm == null) return;

			int armCell = Grid.PosToCell(target.transform.GetPosition());
			Grid.CellToXY(armCell, out int ax, out int ay);
			int range = arm.pickupRange;

			var reachable = new HashSet<int>();
			for (int y = ay - range; y <= ay + range; y++) {
				for (int x = ax - range; x <= ax + range; x++) {
					int cell = Grid.XYToCell(x, y);
					if (Grid.IsValidCell(cell) && Grid.IsPhysicallyAccessible(
							ax, ay, x, y, blocking_tile_visible: true))
						reachable.Add(cell);
				}
			}

			// Candidates: every building in the box (a dropper on an
			// unreachable cell can still land debris on a reachable one),
			// widened by DropReach columns because a dropper just outside
			// the box can drop sideways into it, plus droppers above the
			// box found by column scan.
			var candidates = new HashSet<GameObject>();
			for (int y = ay - range; y <= ay + range; y++) {
				for (int x = ax - range - DropReach; x <= ax + range + DropReach; x++) {
					int cell = Grid.XYToCell(x, y);
					if (!Grid.IsValidCell(cell)) continue;
					foreach (int layer in BuildingLayers) {
						var go = Grid.Objects[cell, layer];
						if (go != null && go != target)
							candidates.Add(go);
					}
				}
			}
			for (int x = ax - range - DropReach; x <= ax + range + DropReach; x++) {
				int cell = Grid.XYToCell(x, ay + range);
				while (true) {
					cell = Grid.CellAbove(cell);
					if (!Grid.IsValidCell(cell) || Grid.Solid[cell]) break;
					var go = Grid.Objects[cell, (int)ObjectLayer.Building];
					if (go != null && go != target && GetDropCell(go) != Grid.InvalidCell)
						candidates.Add(go);
				}
			}

			// Building -> chebyshev distance from the arm to the cell it is
			// serviced through (pivot, or drop landing for debris sources).
			var found = new Dictionary<GameObject, int>();
			foreach (var go in candidates) {
				int cell = GetServiceCell(go, reachable);
				if (cell == Grid.InvalidCell) continue;
				found[go] = ChebyshevDistance(cell, ax, ay);
			}

			AppendMiners(target, ax, ay, range, reachable, found);
			AppendLiveChoreTargets(target, ax, ay, reachable, found);

			var section = new DetailSection {
				Key = "sweeperRange",
				Header = (string)STRINGS.ONIACCESS.DETAILS.IN_RANGE
			};

			var ordered = new List<KeyValuePair<GameObject, int>>(found);
			ordered.Sort((a, b) => {
				int byDist = a.Value.CompareTo(b.Value);
				if (byDist != 0) return byDist;
				return Grid.PosToCell(a.Key).CompareTo(Grid.PosToCell(b.Key));
			});

			foreach (var pair in ordered) {
				var go = pair.Key;
				section.Items.Add(new UserMenuButtonWidget {
					Key = go.GetInstanceID().ToString(),
					SpeechFunc = () => FormatItem(go, target),
					OnClick = () => SelectBuilding(go)
				});
			}

			if (section.Items.Count == 0)
				section.Items.Add(new LabelWidget {
					Key = "sweeperRangeEmpty",
					SpeechFunc = () =>
						(string)STRINGS.ONIACCESS.DETAILS.NOTHING_IN_RANGE
				});

			sections.Add(section);
		}

		/// <summary>
		/// Robo-Miners whose mining area overlaps the arm's reach. The scan
		/// box is widened by MinerReach because a miner well outside the
		/// arm's own box can still dig cells inside it.
		/// </summary>
		private static void AppendMiners(
				GameObject target, int ax, int ay, int range,
				HashSet<int> reachable, Dictionary<GameObject, int> found) {
			int reach = range + MinerReach;
			var miners = new HashSet<GameObject>();
			for (int y = ay - reach; y <= ay + reach; y++) {
				for (int x = ax - reach; x <= ax + reach; x++) {
					int cell = Grid.XYToCell(x, y);
					if (!Grid.IsValidCell(cell)) continue;
					var go = Grid.Objects[cell, (int)ObjectLayer.Building];
					if (go != null && go != target && !found.ContainsKey(go)
							&& go.GetComponent<AutoMiner>() != null)
						miners.Add(go);
				}
			}

			foreach (var go in miners) {
				int best = int.MaxValue;
				foreach (int cell in MiningAreaCells(go)) {
					if (!reachable.Contains(cell)) continue;
					best = Mathf.Min(best, ChebyshevDistance(cell, ax, ay));
				}
				if (best != int.MaxValue)
					found[go] = best;
			}
		}

		/// <summary>
		/// The cells an AutoMiner can dig, mirroring its RefreshDiggableCell
		/// iteration (rotated x/y/width/height area around the pivot),
		/// without the line-of-sight and diggability filters.
		/// </summary>
		private static IEnumerable<int> MiningAreaCells(GameObject go) {
			var miner = go.GetComponent<AutoMiner>();
			var rot = go.GetComponent<Rotatable>();
			int pivot = Grid.PosToCell(go);
			for (int i = 0; i < miner.height; i++) {
				for (int j = 0; j < miner.width; j++) {
					var offset = new CellOffset(miner.x + j, miner.y + i);
					if (rot != null)
						offset = rot.GetRotatedCellOffset(offset);
					int cell = Grid.OffsetCell(pivot, offset);
					if (Grid.IsValidCell(cell))
						yield return cell;
				}
			}
		}

		/// <summary>
		/// Buildings with a live fetch chore the arm could carry: repair
		/// materials, tinker microchips, and any future chore source. Only
		/// additive over the static rules, and only while the chore exists.
		/// </summary>
		private static void AppendLiveChoreTargets(
				GameObject target, int ax, int ay,
				HashSet<int> reachable, Dictionary<GameObject, int> found) {
			var provider = GlobalChoreProvider.Instance;
			if (provider == null) return;
			if (!provider.fetchMap.TryGetValue(
					target.GetMyParentWorldId(), out var chores))
				return;
			foreach (var chore in chores) {
				if (chore == null || chore.destination == null) continue;
				var go = chore.destination.gameObject;
				if (go == null || go == target || found.ContainsKey(go)) continue;
				if (go.GetComponent<MinionIdentity>() != null) continue;
				int cell = Grid.PosToCell(go);
				if (!reachable.Contains(cell)) continue;
				if (!IsArmCarryable(chore)) continue;
				found[go] = ChebyshevDistance(cell, ax, ay);
			}
		}

		/// <summary>
		/// Whether the arm could carry what this chore requests, mirroring
		/// Assets.TryAddSolidTransferArmConveyableTag: a tag matches when it
		/// is a conveyable prefab or one of the conveyable categories.
		/// </summary>
		private static bool IsArmCarryable(FetchChore chore) {
			if (chore.tags == null) return false;
			foreach (var tag in chore.tags)
				if (IsArmConveyableTag(tag)) return true;
			return false;
		}

		/// <summary>
		/// Whether a tag names something the arm can carry: a conveyable
		/// prefab or one of the conveyable storage-filter categories.
		/// </summary>
		private static bool IsArmConveyableTag(Tag tag) {
			if (Assets.IsTagSolidTransferArmConveyable(tag)) return true;
			foreach (var category in
					TUNING.STORAGEFILTERS.SOLID_TRANSFER_ARM_CONVEYABLE)
				if (category == tag) return true;
			return false;
		}

		private static int ChebyshevDistance(int cell, int ax, int ay) {
			Grid.CellToXY(cell, out int cx, out int cy);
			return Mathf.Max(Mathf.Abs(cx - ax), Mathf.Abs(cy - ay));
		}

		/// <summary>
		/// The cell the arm services this building through: the pivot for
		/// fetch destinations and pickup sources, or the drop landing for
		/// debris sources. Grid.InvalidCell when the arm cannot service it.
		/// </summary>
		private static int GetServiceCell(GameObject go, HashSet<int> reachable) {
			if (go.GetComponent<SolidTransferArm>() != null)
				return Grid.InvalidCell;
			int pivot = Grid.PosToCell(go);
			if (CanServiceBuilding(go) && reachable.Contains(pivot))
				return pivot;
			int drop = GetDropCell(go);
			if (drop != Grid.InvalidCell) {
				int landing = FallCell(drop);
				if (reachable.Contains(landing))
					return landing;
			}
			return Grid.InvalidCell;
		}

		/// <summary>
		/// Whether the arm can deliver to or pick up from this building
		/// directly. Delivery destinations are the components that create
		/// FetchChores to their own storage (FetchChore has no transfer-arm
		/// precondition, unlike WorkChore and MovePickupableChore):
		///   StorageLocker      - bins, smart bins, storage tiles, feeders
		///   TreeFilterable     - filtered receivers: conveyor loaders,
		///                        fridges, ration boxes (Storage required)
		///   ManualDeliveryKG   - machine inputs: generators, compost, etc.
		///   ComplexFabricator  - fabricator ingredients: grill, kiln, etc.
		///   SingleEntityReceptacle - farm tiles, planter boxes, incubators
		///   Constructable      - construction material delivery
		///   ObjectDispenser    - automatic dispenser
		///   SuitLocker         - suit delivery to docks
		///   TinkerStation      - tinker materials: Power Control Station
		///   OxidizerTank       - solid oxidizer loading
		///   RemoteWorkerDock   - steel to rebuild its remote worker
		/// Pickup sources:
		///   SolidConduitOutbox - conveyor receptacle
		///   arm-accessible Storage - plain storages the arm can steal
		///                        conveyable items from, e.g. the Sweepy
		///                        dock's collected-debris storage
		/// Excludes internal buffers the arm can never touch (pumps,
		/// reservoirs, valves). Transient chores (repair, tinker) are
		/// handled by AppendLiveChoreTargets instead.
		/// </summary>
		private static bool CanServiceBuilding(GameObject go) {
			return go.GetComponent<StorageLocker>() != null
				|| (go.GetComponent<TreeFilterable>() != null
					&& go.GetComponent<Storage>() != null)
				|| go.GetComponent<ManualDeliveryKG>() != null
				|| go.GetComponent<ComplexFabricator>() != null
				|| go.GetComponent<SingleEntityReceptacle>() != null
				|| go.GetComponent<Constructable>() != null
				|| go.GetComponent<ObjectDispenser>() != null
				|| go.GetComponent<SuitLocker>() != null
				|| go.GetComponent<TinkerStation>() != null
				|| go.GetComponent<OxidizerTank>() != null
				|| go.GetComponent<RemoteWorkerDock>() != null
				|| go.GetComponent<SolidConduitOutbox>() != null
				|| HasArmAccessibleStorage(go);
		}

		/// <summary>
		/// Whether the building has a Storage the arm can pick up from.
		/// Mirrors the game's fetch rules: stored items stay fetchable only
		/// when their storage allows item removal (FetchableMonitor tags
		/// them StoredPrivate otherwise), and the arm only takes conveyable
		/// items, approximated here by the storage's declared filters.
		/// The filter check keeps out buildings whose removable contents
		/// the arm can never carry: critter traps (live critters) and the
		/// pitcher pump and bottlers (liquid bottles) declare no filters.
		/// In practice this admits the Sweepy dock's debris storage.
		/// </summary>
		private static bool HasArmAccessibleStorage(GameObject go) {
			foreach (var storage in go.GetComponents<Storage>()) {
				if (!storage.allowItemRemoval || storage.storageFilters == null)
					continue;
				foreach (var tag in storage.storageFilters)
					if (IsArmConveyableTag(tag)) return true;
			}
			return false;
		}

		/// <summary>
		/// The cell where this building spawns dropped items, mirroring each
		/// component's own drop code, or Grid.InvalidCell for buildings that
		/// drop nothing. Conveyor chutes drop at their own cell, automatic
		/// dispensers at their rotated dropOffset, element droppers (Water
		/// Sieve, Rust Deoxidizer, Compost, Fertilizer Synthesizer, Air
		/// Filter) at their unrotated emitOffset, the Oxylite Refinery and
		/// Ethanol Distiller at their components' unrotated offsets, the
		/// Polymerizer at its rotated emitOffset shifted away from a solid
		/// target cell, and fabricators at outputOffset from their pivot.
		/// All offsets are read from the live component.
		/// </summary>
		private static int GetDropCell(GameObject go) {
			int pivot = Grid.PosToCell(go);
			if (go.GetComponent<SolidConduitDropper>() != null)
				return pivot;
			var dispenser = go.GetComponent<ObjectDispenser>();
			if (dispenser != null) {
				var rot = go.GetComponent<Rotatable>();
				var offset = rot != null
					? rot.GetRotatedCellOffset(dispenser.dropOffset)
					: dispenser.dropOffset;
				return Grid.OffsetCell(pivot, offset);
			}
			var dropper = go.GetComponent<ElementDropper>();
			if (dropper != null)
				return Grid.PosToCell(
					Grid.CellToPosCCC(pivot, Grid.SceneLayer.Ore)
					+ dropper.emitOffset);
			var refinery = go.GetComponent<OxyliteRefinery>();
			if (refinery != null)
				return Grid.PosToCell(
					go.transform.GetPosition() + refinery.dropOffset);
			var distillery = go.GetComponent<AlgaeDistillery>();
			if (distillery != null)
				return Grid.PosToCell(
					go.transform.GetPosition() + distillery.emitOffset);
			var polymerizer = go.GetComponent<Polymerizer>();
			if (polymerizer != null) {
				var rot = go.GetComponent<Rotatable>();
				var pos = go.transform.GetPosition()
					+ rot.GetRotatedOffset(polymerizer.emitOffset);
				if (Grid.Solid[Grid.PosToCell(pos)])
					pos += rot.GetRotatedOffset(Vector3.left);
				return Grid.PosToCell(pos);
			}
			var fabricator = go.GetComponent<ComplexFabricator>();
			if (fabricator != null)
				return Grid.PosToCell(
					Grid.CellToPosCCC(pivot, Grid.SceneLayer.Ore)
					+ fabricator.outputOffset);
			return Grid.InvalidCell;
		}

		/// <summary>Where an item dropped at this cell comes to rest.</summary>
		private static int FallCell(int cell) {
			if (!Grid.IsValidCell(cell)) return cell;
			while (true) {
				int below = Grid.CellBelow(cell);
				if (!Grid.IsValidCell(below) || Grid.Solid[below]) return cell;
				cell = below;
			}
		}

		private static string FormatItem(GameObject go, GameObject armGo) {
			if (go == null) return null;
			var parts = new List<string>();
			string name = go.GetComponent<KSelectable>().GetName();
			if (go.GetComponent<Constructable>() != null)
				name = string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.UNDER_CONSTRUCTION, name);
			parts.Add(name);
			var pri = go.GetComponent<Prioritizable>();
			if (pri != null && pri.IsPrioritizable())
				parts.Add(PriorityWidget.FormatPriority(pri.GetMasterPriority()));
			int pivot = Grid.PosToCell(go);
			parts.Add(GridCoordinates.Format(pivot));

			// Buildings with both a delivery cell (pivot) and a separate
			// output landing get each side qualified, so a fabricator the
			// arm can only half-service says which half works. Skipped when
			// the arm is gone: ArmReaches would report everything blocked.
			bool armAlive = armGo != null
				&& armGo.GetComponent<SolidTransferArm>() != null;
			if (armAlive && CanServiceBuilding(go) && !ArmReaches(armGo, pivot))
				parts.Add((string)STRINGS.ONIACCESS.DETAILS.DELIVERY_OUT_OF_REACH);

			int drop = GetDropCell(go);
			if (drop != Grid.InvalidCell) {
				int landing = FallCell(drop);
				if (landing != pivot)
					parts.Add(string.Format(
						!armAlive || ArmReaches(armGo, landing)
							? (string)STRINGS.ONIACCESS.DETAILS.DROPS_TO
							: (string)STRINGS.ONIACCESS.DETAILS.DROPS_TO_BLOCKED,
						GridCoordinates.Format(landing)));
			}

			if (go.GetComponent<AutoMiner>() != null
					&& MinerOverlapsReach(go, armGo))
				parts.Add((string)STRINGS.ONIACCESS.DETAILS.MINING_OVERLAP);
			else if (!CanServiceBuilding(go) && drop == Grid.InvalidCell) {
				var chore = FindLiveChore(go);
				if (chore != null)
					parts.Add(chore.choreType.Name);
			}

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Live check that the arm can reach this cell, recomputed at speech
		/// time so terrain changes between section rebuilds never produce a
		/// stale claim.
		/// </summary>
		private static bool ArmReaches(GameObject armGo, int cell) {
			if (armGo == null) return false;
			var arm = armGo.GetComponent<SolidTransferArm>();
			if (arm == null) return false;
			int armCell = Grid.PosToCell(armGo.transform.GetPosition());
			Grid.CellToXY(armCell, out int ax, out int ay);
			Grid.CellToXY(cell, out int cx, out int cy);
			if (Mathf.Abs(cx - ax) > arm.pickupRange
					|| Mathf.Abs(cy - ay) > arm.pickupRange)
				return false;
			return Grid.IsPhysicallyAccessible(
				ax, ay, cx, cy, blocking_tile_visible: true);
		}

		private static bool MinerOverlapsReach(GameObject go, GameObject armGo) {
			foreach (int cell in MiningAreaCells(go)) {
				if (ArmReaches(armGo, cell))
					return true;
			}
			return false;
		}

		private static FetchChore FindLiveChore(GameObject go) {
			var provider = GlobalChoreProvider.Instance;
			if (provider == null) return null;
			if (!provider.fetchMap.TryGetValue(
					go.GetMyParentWorldId(), out var chores))
				return null;
			foreach (var chore in chores) {
				if (chore == null || chore.destination == null) continue;
				if (chore.destination.gameObject == go && IsArmCarryable(chore))
					return chore;
			}
			return null;
		}

		private static void SelectBuilding(GameObject go) {
			var selectable = go.GetComponent<KSelectable>();
			if (!(PlayerController.Instance.ActiveTool is SelectTool))
				SelectTool.Instance.Activate();
			SelectTool.Instance.Select(selectable);
		}
	}
}
