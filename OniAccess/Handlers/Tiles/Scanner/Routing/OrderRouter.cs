namespace OniAccess.Handlers.Tiles.Scanner.Routing {
	/// <summary>
	/// Order detection configuration and clustering strategy.
	/// Defines per-order-type metadata used by GridScanner and OrderBackend.
	///
	/// Detection patterns mirror OrderSection.cs but return booleans
	/// and type keys for clustering instead of formatted text.
	/// </summary>
	public static class OrderRouter {
		public enum ClusterStrategy {
			BoxSelection,
			SameType,
			Individual,
		}

		public struct OrderType {
			public string Label;
			public ClusterStrategy Strategy;
		}

		public static readonly OrderType Dig = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_DIG,
			Strategy = ClusterStrategy.BoxSelection,
		};

		public static readonly OrderType Mop = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_MOP,
			Strategy = ClusterStrategy.BoxSelection,
		};

		public static readonly OrderType Sweep = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_SWEEP,
			Strategy = ClusterStrategy.BoxSelection,
		};

		public static readonly OrderType Disinfect = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_DISINFECT,
			Strategy = ClusterStrategy.BoxSelection,
		};

		public static readonly OrderType Build = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_BUILD,
			Strategy = ClusterStrategy.SameType,
		};

		public static readonly OrderType Deconstruct = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_DECONSTRUCT,
			Strategy = ClusterStrategy.SameType,
		};

		public static readonly OrderType Harvest = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_HARVEST,
			Strategy = ClusterStrategy.SameType,
		};

		public static readonly OrderType Uproot = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_UPROOT,
			Strategy = ClusterStrategy.SameType,
		};

		public static readonly OrderType Attack = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_ATTACK,
			Strategy = ClusterStrategy.Individual,
		};

		public static readonly OrderType Capture = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_CAPTURE,
			Strategy = ClusterStrategy.Individual,
		};

		public static readonly OrderType EmptyPipe = new OrderType {
			Label = (string)STRINGS.ONIACCESS.GLANCE.ORDER_EMPTY_PIPE,
			Strategy = ClusterStrategy.Individual,
		};

		// --- Detection methods ---
		// These replicate the checks from OrderSection.cs.
		// Each returns true if the order is present at the given cell/object.

		public static bool HasDigOrder(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.DigPlacer];
			return go != null && go.GetComponent<Diggable>() != null;
		}

		public static bool HasMopOrder(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.MopPlacer];
			return go != null && go.GetComponent<Moppable>() != null;
		}

		/// <summary>
		/// Returns the target material name for a dig order cell,
		/// used for cluster naming (e.g., "dig sandstone" vs "dig mixed").
		/// </summary>
		public static string GetDigTarget(int cell) {
			return Grid.Element[cell].name;
		}

		/// <summary>
		/// Returns the target liquid name for a mop order cell.
		/// </summary>
		public static string GetMopTarget(int cell) {
			return Grid.Element[cell].name;
		}

		/// <summary>
		/// Checks whether any pickupable at the cell is marked for sweep.
		/// </summary>
		public static bool HasSweepOrder(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (go == null) return false;
			var pickupable = go.GetComponent<Pickupable>();
			if (pickupable == null) return false;
			var item = pickupable.objectLayerListItem;
			while (item != null) {
				var prefabId = item.gameObject.GetComponent<KPrefabID>();
				if (prefabId != null && prefabId.HasTag(GameTags.Garbage))
					return true;
				item = item.nextItem;
			}
			return false;
		}

		public static bool HasDisinfectOrder(int cell) {
			return HasDisinfectOnLayer(cell, (int)ObjectLayer.Building)
				|| HasDisinfectOnLayer(cell, (int)ObjectLayer.FoundationTile)
				|| HasDisinfectOnLayer(cell, (int)ObjectLayer.Pickupables);
		}

		private static bool HasDisinfectOnLayer(int cell, int layer) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return false;
			var disinfectable = go.GetComponent<Disinfectable>();
			if (disinfectable == null) return false;
			var selectable = disinfectable.GetComponent<KSelectable>();
			return selectable != null
				&& selectable.HasStatusItem(
					Db.Get().MiscStatusItems.MarkedForDisinfection);
		}

		/// <summary>
		/// Checks for a build order (Constructable) on the Building layer.
		/// Returns the building prefab ID as the type key for same-type clustering.
		/// </summary>
		public static string GetBuildOrderType(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null || go.GetComponent<Constructable>() == null)
				go = Grid.Objects[cell, (int)ObjectLayer.AttachableBuilding];
			if (go == null) return null;
			var constructable = go.GetComponent<Constructable>();
			if (constructable == null) return null;
			return go.GetComponent<Building>().Def.PrefabID;
		}

		/// <summary>
		/// Returns the building name for a build order cell (for announcement).
		/// </summary>
		public static string GetBuildOrderName(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null || go.GetComponent<Constructable>() == null)
				go = Grid.Objects[cell, (int)ObjectLayer.AttachableBuilding];
			if (go == null) return null;
			return go.GetComponent<KSelectable>()?.GetName();
		}

		private static readonly int[] _deconstructLayers = {
			(int)ObjectLayer.Building,
			(int)ObjectLayer.AttachableBuilding,
			(int)ObjectLayer.FoundationTile,
			(int)ObjectLayer.Backwall,
			(int)ObjectLayer.Gantry,
			(int)ObjectLayer.Wire,
			(int)ObjectLayer.WireConnectors,
			(int)ObjectLayer.LiquidConduit,
			(int)ObjectLayer.LiquidConduitConnection,
			(int)ObjectLayer.GasConduit,
			(int)ObjectLayer.GasConduitConnection,
			(int)ObjectLayer.SolidConduit,
			(int)ObjectLayer.SolidConduitConnection,
			(int)ObjectLayer.LogicWire,
			(int)ObjectLayer.LogicGate,
		};

		public static string GetDeconstructOrderType(int cell) {
			for (int i = 0; i < _deconstructLayers.Length; i++) {
				string type = GetDeconstructOnLayer(cell, _deconstructLayers[i]);
				if (type != null) return type;
			}
			return null;
		}

		private static string GetDeconstructOnLayer(int cell, int layer) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return null;
			var deconstructable = go.GetComponent<Deconstructable>();
			if (deconstructable == null) return null;
			if (!deconstructable.IsMarkedForDeconstruction()) return null;
			return go.GetComponent<Building>().Def.PrefabID;
		}

		public static string GetDeconstructOrderName(int cell) {
			for (int i = 0; i < _deconstructLayers.Length; i++) {
				string name = GetDeconstructNameOnLayer(cell, _deconstructLayers[i]);
				if (name != null) return name;
			}
			return null;
		}

		private static string GetDeconstructNameOnLayer(int cell, int layer) {
			var go = Grid.Objects[cell, layer];
			if (go == null) return null;
			var d = go.GetComponent<Deconstructable>();
			if (d == null || !d.IsMarkedForDeconstruction()) return null;
			return go.GetComponent<KSelectable>()?.GetName();
		}

		public static string GetHarvestOrderType(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return null;
			var harvestable = go.GetComponent<HarvestDesignatable>();
			if (harvestable == null) return null;
			if (!harvestable.MarkedForHarvest) return null;
			return go.GetComponent<KPrefabID>().PrefabTag.Name;
		}

		public static string GetHarvestOrderName(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return null;
			return go.GetComponent<KSelectable>()?.GetName();
		}

		public static string GetUprootOrderType(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return null;
			var uprootable = go.GetComponent<Uprootable>();
			if (uprootable == null) return null;
			if (!uprootable.IsMarkedForUproot) return null;
			return go.GetComponent<KPrefabID>().PrefabTag.Name;
		}

		public static string GetUprootOrderName(int cell) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			if (go == null) return null;
			return go.GetComponent<KSelectable>()?.GetName();
		}

		/// <summary>
		/// Checks if any conduit at the cell is marked for emptying.
		/// </summary>
		public static bool HasEmptyPipeOrder(int cell, int conduitLayer) {
			var go = Grid.Objects[cell, conduitLayer];
			if (go == null) return false;
			var workable = go.GetComponent<IEmptyConduitWorkable>();
			if (workable.IsNullOrDestroyed()) return false;
			var selectable = (workable as UnityEngine.MonoBehaviour)?.GetComponent<KSelectable>();
			if (selectable == null) return false;
			var group = selectable.GetStatusItemGroup();
			if (group == null) return false;
			return group.HasStatusItemID("EmptyLiquidConduit")
				|| group.HasStatusItemID("EmptyGasConduit")
				|| group.HasStatusItemID("EmptySolidConduit");
		}
	}
}
