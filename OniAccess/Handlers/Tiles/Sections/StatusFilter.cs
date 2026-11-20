using System.Collections.Generic;

namespace OniAccess.Handlers.Tiles.Sections {
	/// <summary>
	/// Decides which building status items to speak based on the active overlay.
	/// Overlay-specific warnings only appear when their overlay is active;
	/// the default set covers everything else.
	/// </summary>
	public static class StatusFilter {
		/// <summary>
		/// Status item IDs that only appear in a specific overlay.
		/// Keyed by overlay mode HashedString, values are sets of StatusItem.Id.
		/// </summary>
		private static Dictionary<HashedString, HashSet<string>> overlayItems;

		/// <summary>
		/// Neutral status items that should always be spoken regardless of overlay.
		/// </summary>
		private static HashSet<string> alwaysNeutrals;

		/// <summary>
		/// Union of all overlay-specific IDs, for fast exclusion in default mode.
		/// </summary>
		private static HashSet<string> allOverlayItems;

		/// <summary>
		/// Overlay-specific IDs that also speak in the default view: warnings
		/// about a building's own job that an overlay explains further.
		/// </summary>
		private static HashSet<string> sharedWithDefault;

		private static HashedString cropOverlay;

		public static void Initialize() {
			overlayItems = new Dictionary<HashedString, HashSet<string>>();

			var power = new HashSet<string> {
				"NeedPower", "NotEnoughPower", "PowerLoopDetected",
				"NoWireConnected", "NoPowerConsumers", "WireDisconnected",
				"GeneratorOffline", "PowerButtonOff", "Overloaded",
				"ReactorRefuelDisabled"
			};
			overlayItems[OverlayModes.Power.ID] = power;

			var gas = new HashSet<string> {
				"NeedGasIn", "NeedGasOut", "GasPipeEmpty",
				"GasVentObstructed", "GasVentOverPressure", "NoGasElementToPump"
			};
			overlayItems[OverlayModes.GasConduits.ID] = gas;

			var liquid = new HashSet<string> {
				"NeedLiquidIn", "NeedLiquidOut", "LiquidPipeEmpty",
				"LiquidVentObstructed", "LiquidVentOverPressure",
				"NoLiquidElementToPump",
				"OutputPipeFull", "OutputPipeFull_Lavatory"
			};
			overlayItems[OverlayModes.LiquidConduits.ID] = liquid;

			var solid = new HashSet<string> {
				"NeedSolidIn", "NeedSolidOut"
			};
			overlayItems[OverlayModes.SolidConveyor.ID] = solid;

			var logic = new HashSet<string> {
				"NoLogicWireConnected", "LogicOverloaded",
				"LogicSwitchStatusActive", "LogicSwitchStatusInactive",
				"LogicSensorStatusActive", "LogicSensorStatusInactive"
			};
			overlayItems[OverlayModes.Logic.ID] = logic;

			var farming = new HashSet<string> {
				"NeedPlant", "NeedSeed", "NoAvailableSeed",
				"NeedEgg", "NoAvailableEgg", "NoFishableWaterBelow",
				"TrapNeedsArming", "NoLureElementSelected",
				"AwaitingSeedDelivery", "AwaitingBaitDelivery",
				"AwaitingEggDelivery",
				"CREATURE_REUSABLE_TRAP.READY",
				"CREATURE_REUSABLE_TRAP.SPRUNG"
			};
			cropOverlay = OverlayModes.Crop.ID;
			overlayItems[cropOverlay] = farming;

			var oxygen = new HashSet<string> {
				"PressureOk", "UnderPressure"
			};
			overlayItems[OverlayModes.Oxygen.ID] = oxygen;

			allOverlayItems = new HashSet<string>();
			foreach (var set in overlayItems.Values)
				foreach (var id in set)
					allOverlayItems.Add(id);

			sharedWithDefault = new HashSet<string> {
				"OutputPipeFull", "OutputPipeFull_Lavatory"
			};

			alwaysNeutrals = new HashSet<string> {
				"UnderConstruction", "UnderConstructionNoWorker",
				"PendingSwitchToggle", "PendingUpgrade",
				"BuildingDisabled", "Expired",
				"WaitingForRepairMaterials", "MissingRequirements"
			};
		}

		/// <summary>
		/// Returns true if the active overlay is a focused utility view
		/// that should skip non-essential building layers like backwalls.
		/// </summary>
		public static bool IsOverlayFocused(HashedString activeOverlay) {
			return overlayItems.ContainsKey(activeOverlay);
		}

		/// <summary>
		/// Returns true if this status item should be spoken given the active overlay.
		/// </summary>
		public static bool ShouldSpeak(StatusItem item, HashedString activeOverlay,
				bool isPlant) {
			string id = item.Id;
			var severity = item.notificationType;

			// Overlays with their own list: only speak that overlay's items
			// (plus plant neutrals in the farming overlay)
			HashSet<string> activeSet;
			if (overlayItems.TryGetValue(activeOverlay, out activeSet)) {
				if (activeSet.Contains(id))
					return true;
				if (severity == NotificationType.Neutral && isPlant
					&& activeOverlay == cropOverlay)
					return true;
				return false;
			}

			// Default view: hand-picked neutrals always speak
			if (severity == NotificationType.Neutral && alwaysNeutrals.Contains(id))
				return true;

			// Other neutrals suppressed
			if (!IsWarning(item))
				return false;

			// Bad/BadMinor not claimed by any overlay
			return !allOverlayItems.Contains(id) || sharedWithDefault.Contains(id);
		}

		/// <summary>
		/// True for the severities the default view speaks.
		/// </summary>
		public static bool IsWarning(StatusItem item) {
			return item.notificationType == NotificationType.Bad
				|| item.notificationType == NotificationType.BadMinor;
		}
	}
}
