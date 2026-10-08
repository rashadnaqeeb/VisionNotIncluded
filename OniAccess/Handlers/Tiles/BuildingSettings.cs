using System.Collections.Generic;
using UnityEngine;

using OniAccess.Util;

namespace OniAccess.Handlers.Tiles {
	/// <summary>
	/// Describes how a building is configured: the values the player set on its
	/// side screen. The game's hover text reports what a building is doing, never
	/// what it was told to do, so these have no tooltip line to borrow and are
	/// read from the live components at the moment they are asked for.
	///
	/// Every setting reads wherever the building does, with no overlay routing:
	/// the readouts this feeds are on-demand, so the player asking about a
	/// building is asking about all of it. The exception is a setting the game
	/// publishes itself in one overlay and hides in the rest, which is skipped
	/// where it would be heard twice.
	///
	/// Settings whose value cannot be stated in one line (storage filters,
	/// cluster location lists, access permissions) are deliberately absent, as is
	/// storage capacity - IUserControlledCapacity sits on dozens of common
	/// buildings and would bury the settings worth hearing. The Storage Tile is
	/// the exception: it holds a single item, and its fill line never reaches
	/// the hover text.
	/// </summary>
	internal static class BuildingSettings {
		/// <summary>
		/// Layers a configurable building can occupy. Logic gates, counters, and
		/// ribbon readers live on LogicGate rather than Building.
		/// </summary>
		private static readonly int[] SettingLayers = {
			(int)ObjectLayer.Building,
			(int)ObjectLayer.FoundationTile,
			(int)ObjectLayer.AttachableBuilding,
			(int)ObjectLayer.LogicGate,
		};

		/// <summary>
		/// The settings of every building at a cell, or null when none of them
		/// has one.
		/// </summary>
		internal static string DescribeAtCell(int cell) {
			var tokens = new List<string>();
			var seen = new HashSet<GameObject>();
			foreach (int layer in SettingLayers) {
				var go = Grid.Objects[cell, layer];
				if (go == null || !seen.Add(go)) continue;
				Append(go, tokens);
			}
			return Join(tokens);
		}

		/// <summary>One building's settings, or null when it has none.</summary>
		internal static string Describe(GameObject go) {
			var tokens = new List<string>();
			Append(go, tokens);
			return Join(tokens);
		}

		private static string Join(List<string> tokens) {
			return tokens.Count > 0 ? string.Join(", ", tokens.ToArray()) : null;
		}

		/// <summary>
		/// A reader that throws takes the settings after it with it, so the log
		/// names the building the rest was lost on.
		/// </summary>
		private static void Append(GameObject go, List<string> tokens) {
			try {
				ReadThreshold(go, tokens);
				ReadFilter(go, tokens);
				ReadActivationRange(go, tokens);
				ReadSlider(go, tokens);
				ReadValve(go, tokens);
				ReadLimitValve(go, tokens);
				ReadFuelCapacity(go, tokens);
				ReadStorageTile(go, tokens);
				ReadCounter(go, tokens);
				ReadTimer(go, tokens);
				ReadTimeRange(go, tokens);
				ReadCritterCounting(go, tokens);
				ReadRibbonBits(go, tokens);
				ReadAlarm(go, tokens);
				ReadBroadcastChannel(go, tokens);
				ReadRedirector(go, tokens);
				ReadAutomationOnly(go, tokens);
				ReadReceptacle(go, tokens);
				ReadLure(go, tokens);
			} catch (System.Exception ex) {
				Log.Warn($"BuildingSettings.Append on {go.name}: {ex}");
			}
		}

		/// <summary>Threshold sensors and switches.</summary>
		private static void ReadThreshold(GameObject go, List<string> tokens) {
			var threshold = go.GetComponent<IThresholdSwitch>();
			if (threshold == null) return;

			string direction = threshold.ActivateAboveThreshold
				? (string)STRINGS.UI.UISIDESCREENS.THRESHOLD_SWITCH_SIDESCREEN.ABOVE_BUTTON
				: (string)STRINGS.UI.UISIDESCREENS.THRESHOLD_SWITCH_SIDESCREEN.BELOW_BUTTON;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_THRESHOLD,
				(string)threshold.ThresholdValueName,
				direction,
				threshold.Format(threshold.Threshold, true)));
		}

		/// <summary>
		/// Element filters and element sensors. An empty sensor already raises a
		/// "no element selected" status item of its own, so only the filters speak
		/// that case - and only outside the overlay where the game states the
		/// filtered element itself.
		/// </summary>
		private static void ReadFilter(GameObject go, List<string> tokens) {
			var filterable = go.GetComponent<Filterable>();
			if (filterable == null) return;
			if (GameShowsFilter(go)) return;

			var tag = filterable.SelectedTag;
			if (!tag.IsValid || tag == GameTags.Void) {
				if (go.GetComponent<LogicElementSensor>() == null
					&& go.GetComponent<ConduitElementSensor>() == null)
					tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_FILTER_NONE);
				return;
			}
			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_FILTER, tag.ProperName()));
		}

		/// <summary>
		/// Whether the game's own filter status item is on screen for this
		/// building. Mirrors ElementFilter.ShowInUtilityOverlay, which shows that
		/// status item only in the overlay of the filtered port's conduit type.
		/// The sensors carry no ElementFilter and so are never covered.
		/// </summary>
		private static bool GameShowsFilter(GameObject go) {
			var filter = go.GetComponent<ElementFilter>();
			if (filter == null || OverlayScreen.Instance == null) return false;

			var mode = OverlayScreen.Instance.GetMode();
			switch (filter.portInfo.conduitType) {
				case ConduitType.Gas: return mode == OverlayModes.GasConduits.ID;
				case ConduitType.Liquid: return mode == OverlayModes.LiquidConduits.ID;
				case ConduitType.Solid: return mode == OverlayModes.SolidConveyor.ID;
				default: return false;
			}
		}

		/// <summary>
		/// Two-ended activation ranges: the Smart Battery's charge window, the
		/// smart reservoirs' fill window, the Massage Table's stress window.
		/// </summary>
		private static void ReadActivationRange(GameObject go, List<string> tokens) {
			var range = go.GetComponent<IActivationRangeTarget>();
			if (range == null) return;

			string format = range.UseWholeNumbers ? "F0" : "F1";
			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_ACTIVATION_RANGE,
				range.ActivateSliderLabelText,
				range.ActivateValue.ToString(format),
				range.DeactivateSliderLabelText,
				range.DeactivateValue.ToString(format)));
		}

		/// <summary>Single-slider settings, titled by the game's own slider label.</summary>
		private static void ReadSlider(GameObject go, List<string> tokens) {
			var slider = go.GetComponent<ISingleSliderControl>();
			if (slider == null) return;

			StringEntry title;
			if (!Strings.TryGet(slider.SliderTitleKey, out title)) {
				Log.Warn("BuildingSettings: missing slider title key "
					+ slider.SliderTitleKey);
				return;
			}

			string value = slider.GetSliderValue(0)
				.ToString("F" + slider.SliderDecimalPlaces(0));
			if (!string.IsNullOrEmpty(slider.SliderUnits))
				value = value + " " + slider.SliderUnits;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_SLIDER, title.String, value));
		}

		/// <summary>
		/// Valve throughput. DesiredFlow is the player's setting; MaxFlow is the
		/// building's fixed ceiling and would misreport every unadjusted valve.
		/// </summary>
		private static void ReadValve(GameObject go, List<string> tokens) {
			var valve = go.GetComponent<Valve>();
			if (valve == null) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_FLOW,
				GameUtil.GetFormattedMass(valve.DesiredFlow,
					GameUtil.TimeSlice.PerSecond, GameUtil.MetricMassFormat.Gram)));
		}

		private static void ReadLimitValve(GameObject go, List<string> tokens) {
			var limit = go.GetComponent<LimitValve>();
			if (limit == null) return;

			string amount = limit.displayUnitsInsteadOfMass
				? GameUtil.GetFormattedUnits(limit.Limit)
				: GameUtil.GetFormattedMass(limit.Limit);
			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_LIMIT, amount));
		}

		/// <summary>
		/// Radbolt fuel tank capacity. The other IUserControlledCapacity holders
		/// are storage buildings, left out with the rest of storage.
		/// </summary>
		private static void ReadFuelCapacity(GameObject go, List<string> tokens) {
			var capacity = go.GetComponent<HEPFuelTank>();
			if (capacity == null) return;

			float max = capacity.UserMaxCapacity;
			if (float.IsPositiveInfinity(max)) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_CAPACITY,
				max.ToString("F0") + " " + (string)capacity.CapacityUnits));
		}

		/// <summary>
		/// The Storage Tile's item and fill. The game's own "Storing" line is a
		/// Main status item that the building's operational status displaces, so
		/// it never shows; and it would state the tile's fixed 1000 kg rather than
		/// the player's cap, because it looks the cap up as a component and the
		/// tile keeps it on its state machine. An unset item already raises the
		/// game's "Filters Not Designated" status.
		/// </summary>
		private static void ReadStorageTile(GameObject go, List<string> tokens) {
			var tile = go.GetSMI<StorageTile.Instance>();
			if (tile == null) return;

			if (tile.TargetTag != StorageTile.INVALID_TAG)
				tokens.Add(string.Format(
					(string)STRINGS.ONIACCESS.GLANCE.SETTING_FILTER,
					tile.TargetTag.ProperName()));

			// Stored rounds down, as the game rounds its own line.
			float stored = Mathf.Floor(go.GetComponent<Storage>().MassStored());
			tokens.Add(((string)STRINGS.BUILDING.STATUSITEMS.STORAGELOCKER.NAME)
				.Replace("{Stored}", global::Util.FormatWholeNumber(stored))
				.Replace("{Capacity}", global::Util.FormatWholeNumber(tile.UserMaxCapacity))
				.Replace("{Units}", tile.CapacityUnits));
		}

		private static void ReadCounter(GameObject go, List<string> tokens) {
			var counter = go.GetComponent<LogicCounter>();
			if (counter == null) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_COUNT, counter.maxCount));
			if (counter.advancedMode)
				tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_COUNT_ADVANCED);
		}

		private static void ReadTimer(GameObject go, List<string> tokens) {
			var timer = go.GetComponent<LogicTimerSensor>();
			if (timer == null) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_TIMER,
				FormatDuration(timer.onDuration, timer.displayCyclesMode),
				FormatDuration(timer.offDuration, timer.displayCyclesMode)));
		}

		private static string FormatDuration(float seconds, bool cyclesMode) {
			return cyclesMode
				? GameUtil.GetFormattedCycles(seconds, "F2")
				: GameUtil.GetFormattedTime(seconds);
		}

		/// <summary>
		/// Time of day sensor. Both fields are normalized fractions of a cycle, so
		/// they read as percentages rather than being converted to a clock time.
		/// </summary>
		private static void ReadTimeRange(GameObject go, List<string> tokens) {
			var sensor = go.GetComponent<LogicTimeOfDaySensor>();
			if (sensor == null) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_TIME_RANGE,
				GameUtil.GetFormattedPercent(sensor.startTime * 100f),
				GameUtil.GetFormattedPercent(sensor.duration * 100f)));
		}

		private static void ReadCritterCounting(GameObject go, List<string> tokens) {
			var sensor = go.GetComponent<LogicCritterCountSensor>();
			if (sensor == null) return;

			if (sensor.countCritters && sensor.countEggs)
				tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_CRITTERS_AND_EGGS);
			else if (sensor.countCritters)
				tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_CRITTERS);
			else if (sensor.countEggs)
				tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_EGGS);
		}

		private static void ReadRibbonBits(GameObject go, List<string> tokens) {
			var selector = go.GetComponent<ILogicRibbonBitSelector>();
			if (selector == null) return;

			var active = new List<string>();
			int depth = selector.GetBitDepth();
			for (int bit = 0; bit < depth; bit++)
				if (selector.IsBitActive(bit))
					active.Add((bit + 1).ToString());

			tokens.Add(active.Count == 0
				? (string)STRINGS.ONIACCESS.GLANCE.SETTING_BITS_NONE
				: string.Format((string)STRINGS.ONIACCESS.GLANCE.SETTING_BITS,
					string.Join(", ", active.ToArray())));
		}

		private static void ReadAlarm(GameObject go, List<string> tokens) {
			var alarm = go.GetComponent<LogicAlarm>();
			if (alarm == null || string.IsNullOrEmpty(alarm.notificationName)) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_ALARM, alarm.notificationName));
		}

		private static void ReadBroadcastChannel(GameObject go, List<string> tokens) {
			var receiver = go.GetComponent<LogicBroadcastReceiver>();
			if (receiver == null) return;

			var channel = receiver.GetChannel();
			if (channel == null) return;

			var selectable = channel.GetComponent<KSelectable>();
			if (selectable == null) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_CHANNEL,
				selectable.GetName()));
		}

		private static void ReadRedirector(GameObject go, List<string> tokens) {
			var redirector = go.GetComponent<HighEnergyParticleRedirector>();
			if (redirector == null || !redirector.directionControllable) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_AIMED,
				Sections.BuildingSection.EightDirectionToString(redirector.Direction)));
		}

		private static void ReadAutomationOnly(GameObject go, List<string> tokens) {
			var automatable = go.GetComponent<Automatable>();
			if (automatable == null || !automatable.GetAutomationOnly()) return;

			tokens.Add((string)STRINGS.ONIACCESS.GLANCE.SETTING_AUTOMATION_ONLY);
		}

		/// <summary>
		/// The seed a planter is set to grow, or the egg an incubator is set to
		/// hatch. An empty receptacle says nothing here: the game raises its own
		/// status item for that, and it names what kind of thing is missing.
		/// </summary>
		private static void ReadReceptacle(GameObject go, List<string> tokens) {
			var receptacle = go.GetComponent<SingleEntityReceptacle>();
			if (receptacle == null) return;

			var tag = receptacle.requestedEntityTag;
			if (!tag.IsValid) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_SELECTED, tag.ProperName()));
		}

		private static void ReadLure(GameObject go, List<string> tokens) {
			var lure = go.GetComponent<CreatureLure>();
			if (lure == null || !lure.activeBaitSetting.IsValid) return;

			tokens.Add(string.Format(
				(string)STRINGS.ONIACCESS.GLANCE.SETTING_BAIT,
				lure.activeBaitSetting.ProperName()));
		}
	}
}
