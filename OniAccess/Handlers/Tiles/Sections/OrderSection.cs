using System.Collections.Generic;
using UnityEngine;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Reads pending player orders at a cell. Collects individual order
	/// labels (with per-order priority), then emits a single token
	/// prefixed with "pending": e.g. "pending dig priority 5, mop".
	/// Build and deconstruct orders are handled by BuildingSection
	/// and ConduitSection (Constructable/Deconstructable checks).
	/// </summary>
	public class OrderSection: ICellSection {
		public IEnumerable<string> Read(int cell, CellContext ctx) {
			var parts = new List<string>();
			CollectDigOrder(cell, ctx, parts);
			CollectMopOrder(cell, parts);
			CollectSweepOrder(cell, parts);
			CollectHarvestOrder(cell, parts);
			CollectUprootOrder(cell, parts);
			CollectDisinfectOrder(cell, parts);
			CollectAttackOrder(cell, parts);
			CollectCaptureOrder(cell, parts);
			CollectEmptyPipeOrder(cell, parts);

			if (parts.Count == 0)
				return parts;

			return new[] { string.Join(", ", parts.ToArray()) };
		}

		private static void CollectDigOrder(int cell, CellContext ctx, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.DigPlacer];
			if (go == null) return;
			var diggable = go.GetComponent<Diggable>();
			if (diggable == null) return;
			if (ctx.Claimed.Contains(go)) return;
			string label = MaybeBlocked(
				DigOrderLabel(cell, diggable),
				go, Db.Get().BuildingStatusItems.DigUnreachable);
			parts.Add(FormatOrder(label, go));
		}

		// Plain "dig" when the order removes everything diggable at the cell;
		// qualified by target when the cell has both a solid tile and a natural
		// backwall but the order only digs one of them
		private static string DigOrderLabel(int cell, Diggable diggable) {
			bool tilePresent = Grid.Solid[cell];
			bool backwallPresent = BackwallManager.HasBackwall(cell);
			if (!tilePresent || !backwallPresent)
				return (string)STRINGS.ONIACCESS.GLANCE.ORDER_DIG;
			bool digsTile = diggable.WillDigTile();
			bool digsBackwall = diggable.WillDigBackwall();
			if (digsTile && !digsBackwall)
				return (string)STRINGS.ONIACCESS.GLANCE.ORDER_DIG_TILE;
			if (digsBackwall && !digsTile)
				return (string)STRINGS.ONIACCESS.GLANCE.ORDER_DIG_BACKWALL;
			return (string)STRINGS.ONIACCESS.GLANCE.ORDER_DIG;
		}

		private static void CollectMopOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.MopPlacer];
			if (go == null) return;
			if (go.GetComponent<Moppable>() == null) return;
			string label = MaybeBlocked(
				(string)STRINGS.ONIACCESS.GLANCE.ORDER_MOP,
				go, Db.Get().BuildingStatusItems.MopUnreachable);
			parts.Add(FormatOrder(label, go));
		}

		private static void CollectSweepOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return;
			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return;

			var item = pickupable.objectLayerListItem;
			while (item != null) {
				var clearable = item.gameObject.GetComponent<Clearable>();
				if (clearable != null && IsMarkedForClear(clearable)) {
					string label = MaybeBlocked(
						(string)STRINGS.ONIACCESS.GLANCE.ORDER_SWEEP,
						item.gameObject,
						Db.Get().MiscStatusItems.PickupableUnreachable);
					parts.Add(FormatOrder(label, item.gameObject));
					return;
				}
				item = item.nextItem;
			}
		}

		private static void CollectHarvestOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return;
			var harvestable = go.GetComponent<HarvestDesignatable>();
			if (harvestable == null) return;
			if (!harvestable.MarkedForHarvest) return;
			parts.Add(FormatOrder(
				(string)STRINGS.ONIACCESS.GLANCE.ORDER_HARVEST, go));
		}

		private static void CollectUprootOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return;
			var uprootable = go.GetComponent<Uprootable>();
			if (uprootable == null) return;
			if (!uprootable.IsMarkedForUproot) return;
			parts.Add(FormatOrder(
				(string)STRINGS.ONIACCESS.GLANCE.ORDER_UPROOT, go));
		}

		private static void CollectDisinfectOrder(int cell, List<string> parts) {
			CollectDisinfectOnLayer(cell, (int)ObjectLayer.Building, parts);
			CollectDisinfectOnLayer(cell, (int)ObjectLayer.FoundationTile, parts);
			CollectDisinfectOnLayer(cell, (int)ObjectLayer.Pickupables, parts);
		}

		private static void CollectDisinfectOnLayer(
				int cell, int layer, List<string> parts) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return;
			var disinfectable = go.GetComponent<Disinfectable>();
			if (disinfectable == null) return;
			if (!IsMarkedForDisinfect(disinfectable)) return;
			parts.Add(FormatOrder(
				(string)STRINGS.ONIACCESS.GLANCE.ORDER_DISINFECT, go));
		}

		private static void CollectAttackOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return;
			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return;

			var item = pickupable.objectLayerListItem;
			while (item != null) {
				var faction = item.gameObject.GetComponent<FactionAlignment>();
				if (faction != null && faction.IsPlayerTargeted()) {
					parts.Add(FormatOrder(
						(string)STRINGS.ONIACCESS.GLANCE.ORDER_ATTACK,
						item.gameObject));
				}
				item = item.nextItem;
			}
		}

		private static void CollectCaptureOrder(int cell, List<string> parts) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return;
			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return;

			var item = pickupable.objectLayerListItem;
			while (item != null) {
				var capturable = item.gameObject.GetComponent<Capturable>();
				if (capturable != null && capturable.IsMarkedForCapture) {
					parts.Add(FormatOrder(
						(string)STRINGS.ONIACCESS.GLANCE.ORDER_CAPTURE,
						item.gameObject));
				}
				item = item.nextItem;
			}
		}

		private static readonly int[] _conduitLayers = {
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.SolidConduit,
		};

		private static void CollectEmptyPipeOrder(int cell, List<string> parts) {
			for (int i = 0; i < _conduitLayers.Length; i++) {
				var go = Grid.Objects[cell, _conduitLayers[i]];
				if (go == null) continue;
				var workable = go.GetComponent<IEmptyConduitWorkable>();
				if (workable.IsNullOrDestroyed()) continue;
				if (!IsMarkedForEmptying(workable)) continue;
				parts.Add(FormatOrder(
					(string)STRINGS.ONIACCESS.GLANCE.ORDER_EMPTY_PIPE, go));
			}
		}

		private static bool IsMarkedForEmptying(IEmptyConduitWorkable workable) {
			var selectable = (workable as MonoBehaviour)?.GetComponent<KSelectable>();
			if (selectable == null) return false;
			var group = selectable.GetStatusItemGroup();
			return group.HasStatusItemID("EmptyLiquidConduit")
				|| group.HasStatusItemID("EmptyGasConduit")
				|| group.HasStatusItemID("EmptySolidConduit");
		}

		private static string MaybeBlocked(
				string label, GameObject go, StatusItem unreachableItem) {
			var selectable = go.GetComponent<KSelectable>();
			if (selectable == null) return label;

			var skillItems = Db.Get().BuildingStatusItems;
			if (selectable.HasStatusItem(skillItems.ColonyLacksRequiredSkillPerk)
				|| selectable.HasStatusItem(skillItems.ClusterColonyLacksRequiredSkillPerk))
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.ORDER_NEEDS_SKILL, label);

			if (selectable.HasStatusItem(Db.Get().MiscStatusItems.PendingClearNoStorage))
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.ORDER_CANT_STORE, label);

			if (selectable.HasStatusItem(unreachableItem))
				return string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.ORDER_UNREACHABLE, label);

			return label;
		}

		private static string FormatOrder(string label, GameObject go) {
			string priority = GetPriority(go);
			return priority != null
				? string.Format((string)STRINGS.ONIACCESS.GLANCE.ORDER_PRIORITY,
					label, priority)
				: label;
		}

		private static string GetPriority(GameObject go) {
			var prioritizable = go.GetComponent<Prioritizable>();
			if (prioritizable == null) return null;
			var setting = prioritizable.GetMasterPriority();
			return setting.priority_value.ToString();
		}

		private static bool IsMarkedForClear(Clearable clearable) {
			return clearable.HasTag(GameTags.Garbage);
		}

		private static bool IsMarkedForDisinfect(Disinfectable disinfectable) {
			var selectable = disinfectable.GetComponent<KSelectable>();
			return selectable != null
				&& selectable.HasStatusItem(Db.Get().MiscStatusItems.MarkedForDisinfection);
		}
	}
}
