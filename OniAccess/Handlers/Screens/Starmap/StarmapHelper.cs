using System.Collections.Generic;

using STRINGS;

namespace OniAccess.Handlers.Screens.Starmap {
	internal static class StarmapHelper {

		// ========================================
		// DESTINATION QUERIES
		// ========================================

		internal static List<SpaceDestination> GetAllDestinations() {
			return SpacecraftManager.instance.destinations;
		}

		internal static SpacecraftManager.DestinationAnalysisState GetAnalysisState(
				SpaceDestination dest) {
			return SpacecraftManager.instance.GetDestinationAnalysisState(dest);
		}

		internal static bool IsAnalyzed(SpaceDestination dest) {
			return GetAnalysisState(dest) ==
				SpacecraftManager.DestinationAnalysisState.Complete;
		}

		/// <summary>
		/// Returns sorted list of non-empty distance tiers.
		/// Each entry is the OneBasedDistance value.
		/// </summary>
		internal static List<int> GetPopulatedDistanceTiers() {
			var tiers = new HashSet<int>();
			foreach (var dest in GetAllDestinations())
				tiers.Add(dest.OneBasedDistance);
			var sorted = new List<int>(tiers);
			sorted.Sort();
			return sorted;
		}

		internal static List<SpaceDestination> GetDestinationsAtTier(int oneBasedDistance) {
			var result = new List<SpaceDestination>();
			foreach (var dest in GetAllDestinations()) {
				if (dest.OneBasedDistance == oneBasedDistance)
					result.Add(dest);
			}
			return result;
		}

		// ========================================
		// DESTINATION LABELS
		// ========================================

		internal static string GetTierLabel(int oneBasedDistance) {
			return DisplayDistance(oneBasedDistance * 10000f);
		}

		internal static string GetDestinationLabel(SpaceDestination dest) {
			var state = GetAnalysisState(dest);
			if (state == SpacecraftManager.DestinationAnalysisState.Complete)
				return dest.GetDestinationType().Name;
			if (state == SpacecraftManager.DestinationAnalysisState.Discovered) {
				float score = SpacecraftManager.instance.GetDestinationAnalysisScore(dest.id);
				float pct = score / (float)TUNING.ROCKETRY.DESTINATION_ANALYSIS.COMPLETE * 100f;
				return string.Format(UI.STARMAP.ANALYSIS_AMOUNT,
					GameUtil.GetFormattedPercent(pct));
			}
			return (string)UI.STARMAP.UNKNOWN_DESTINATION;
		}

		// ========================================
		// ROCKET QUERIES
		// ========================================

		internal static List<Spacecraft> GetSpacecraft() {
			return SpacecraftManager.instance.GetSpacecraft();
		}

		internal static string GetStatusText(Spacecraft rocket) {
			return StarmapScreen.GetTextForState(rocket.state, rocket).first;
		}

		// ========================================
		// ROCKET LIST LABELS
		// ========================================

		internal static string BuildRocketListLabel(Spacecraft rocket) {
			string name = rocket.GetRocketName();
			string status = Speech.TextFilter.FilterForSpeech(GetStatusText(rocket));

			if (rocket.state == Spacecraft.MissionState.Grounded)
				return $"{name}, {status}";

			// In-flight: show time remaining and progress
			float duration = rocket.GetDuration();
			float timeLeft = rocket.GetTimeLeft();
			float pct = duration == 0f ? 0f : (1f - timeLeft / duration) * 100f;

			string timePrefix = rocket.controlStationBuffTimeRemaining > 0f
				? UI.STARMAP.ROCKETSTATUS.BOOSTED_TIME_MODIFIER.text
				: "";
			string cycles = GameUtil.GetFormattedCycles(timeLeft);
			string progress = GameUtil.GetFormattedPercent(pct);

			return $"{name}, {status}, {timePrefix}{cycles} {STRINGS.ONIACCESS.STARMAP.REMAINING}, {progress} {STRINGS.ONIACCESS.STARMAP.COMPLETE}";
		}

		// ========================================
		// ROCKET DETAIL DATA
		// ========================================

		internal struct RocketCategory {
			public string Name;
			public List<string> Items;
		}

		internal static List<RocketCategory> BuildRocketCategories(Spacecraft rocket) {
			var categories = new List<RocketCategory>();
			var lcm = rocket.launchConditions;
			var cmd = lcm.GetComponent<CommandModule>();

			// Mission Status
			var statusItems = new List<string>();
			string status = Speech.TextFilter.FilterForSpeech(GetStatusText(rocket));
			statusItems.Add($"{UI.STARMAP.ROCKETSTATUS.STATUS}: {status}");
			if (rocket.state != Spacecraft.MissionState.Grounded) {
				float duration = rocket.GetDuration();
				float timeLeft = rocket.GetTimeLeft();
				float pct = duration == 0f ? 0f : (1f - timeLeft / duration) * 100f;
				string timePrefix = rocket.controlStationBuffTimeRemaining > 0f
					? UI.STARMAP.ROCKETSTATUS.BOOSTED_TIME_MODIFIER.text : "";
				statusItems.Add($"{UI.STARMAP.ROCKETSTATUS.TIMEREMAINING}: {timePrefix}{GameUtil.GetFormattedCycles(timeLeft)}, {GameUtil.GetFormattedPercent(pct)} {STRINGS.ONIACCESS.STARMAP.COMPLETE}");
			}
			categories.Add(new RocketCategory {
				Name = (string)UI.STARMAP.LISTTITLES.MISSIONSTATUS,
				Items = statusItems
			});

			// Launch Checklist
			var checkItems = new List<string>();
			foreach (var condition in lcm.GetLaunchConditionList()) {
				var evalStatus = condition.EvaluateCondition();
				string msg = condition.GetStatusMessage(evalStatus);
				string tooltip = condition.GetStatusTooltip(evalStatus);
				string detail = !string.IsNullOrEmpty(tooltip) ? tooltip : msg;
				string label;
				if (evalStatus == ProcessCondition.Status.Ready)
					label = $"{STRINGS.ONIACCESS.STARMAP.CHECK_READY}: {detail}";
				else if (evalStatus == ProcessCondition.Status.Warning)
					label = $"{STRINGS.ONIACCESS.STARMAP.CHECK_WARNING}: {detail}";
				else
					label = $"{STRINGS.ONIACCESS.STARMAP.CHECK_NOT_READY}: {detail}";
				checkItems.Add(label);
			}
			if (checkItems.Count > 0)
				categories.Add(new RocketCategory {
					Name = (string)UI.STARMAP.LISTTITLES.LAUNCHCHECKLIST,
					Items = checkItems
				});

			// Max Range
			var rangeItems = new List<string>();
			rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.TOTAL_OXIDIZABLE_FUEL}: {GameUtil.GetFormattedMass(cmd.rocketStats.GetTotalOxidizableFuel())}");
			rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.ENGINE_EFFICIENCY}: {GameUtil.GetFormattedEngineEfficiency(cmd.rocketStats.GetEngineEfficiency())}");
			rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.OXIDIZER_EFFICIENCY}: {GameUtil.GetFormattedPercent(cmd.rocketStats.GetAverageOxidizerEfficiency())}");
			float booster = cmd.rocketStats.GetBoosterThrust() * 1000f;
			if (booster != 0f)
				rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.SOLID_BOOSTER}: {GameUtil.GetFormattedDistance(booster)}");
			rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.TOTAL_THRUST}: {GameUtil.GetFormattedDistance(cmd.rocketStats.GetTotalThrust() * 1000f)}");
			rangeItems.Add($"{UI.STARMAP.ROCKETSTATS.TOTAL_RANGE}: {GameUtil.GetFormattedDistance(cmd.rocketStats.GetRocketMaxDistance() * 1000f)}");
			categories.Add(new RocketCategory {
				Name = (string)UI.STARMAP.LISTTITLES.MAXRANGE,
				Items = rangeItems
			});

			// Mass
			var massItems = new List<string>();
			massItems.Add($"{UI.STARMAP.ROCKETSTATS.DRY_MASS}: {GameUtil.GetFormattedMass(cmd.rocketStats.GetDryMass(), GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
			massItems.Add($"{UI.STARMAP.ROCKETSTATS.WET_MASS}: {GameUtil.GetFormattedMass(cmd.rocketStats.GetWetMass(), GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
			massItems.Add($"{UI.STARMAP.ROCKETSTATUS.TOTAL}: {GameUtil.GetFormattedMass(cmd.rocketStats.GetTotalMass(), GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
			categories.Add(new RocketCategory {
				Name = (string)UI.STARMAP.LISTTITLES.MASS,
				Items = massItems
			});

			// Fuel, Oxidizer, Storage from module network
			var network = new List<UnityEngine.GameObject>();
			AttachableBuilding.CollectAttachedBuildings(
				cmd.GetComponent<AttachableBuilding>(), network);
			Tag engineFuelTag = cmd.rocketStats.GetEngineFuelTag();

			var fuelItems = new List<string>();
			foreach (var module in network) {
				var tank = module.GetComponent<IFuelTank>();
				if (!tank.IsNullOrDestroyed()) {
					var storage = (tank as UnityEngine.MonoBehaviour)
						.GetComponent<Storage>();
					if (storage != null) {
						float mass = storage.GetMassAvailable(engineFuelTag);
						var elem = ElementLoader.FindElementByHash(
							ElementLoader.GetElementID(engineFuelTag));
						string elemName = elem != null ? elem.name : engineFuelTag.Name;
						fuelItems.Add($"{module.GetProperName()} ({elemName}): {GameUtil.GetFormattedMass(mass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
					}
				}
			}
			if (fuelItems.Count > 0)
				categories.Add(new RocketCategory {
					Name = (string)UI.STARMAP.LISTTITLES.FUEL,
					Items = fuelItems
				});

			var oxItems = new List<string>();
			foreach (var module in network) {
				var oxTank = module.GetComponent<OxidizerTank>();
				if (oxTank != null) {
					foreach (var kvp in oxTank.GetOxidizersAvailable()) {
						if (kvp.Value > 0f)
							oxItems.Add($"{module.GetProperName()} ({kvp.Key.Name}): {GameUtil.GetFormattedMass(kvp.Value, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
					}
				}
			}
			if (oxItems.Count > 0)
				categories.Add(new RocketCategory {
					Name = (string)UI.STARMAP.LISTTITLES.OXIDIZER,
					Items = oxItems
				});

			var storageItems = new List<string>();
			foreach (var module in network) {
				var bay = module.GetComponent<CargoBay>();
				if (bay != null) {
					var storage = module.GetComponent<Storage>();
					if (storage != null) {
						float used = storage.MassStored();
						float cap = storage.capacityKg;
						storageItems.Add($"{module.GetProperName()}: {GameUtil.GetFormattedMass(used)} / {GameUtil.GetFormattedMass(cap)}");
					}
				}
			}
			if (storageItems.Count > 0)
				categories.Add(new RocketCategory {
					Name = (string)UI.STARMAP.LISTTITLES.STORAGE,
					Items = storageItems
				});

			// Passengers
			var minionStorage = lcm.GetComponent<MinionStorage>();
			if (minionStorage != null) {
				var stored = minionStorage.GetStoredMinionInfo();
				if (stored.Count > 0)
					categories.Add(new RocketCategory {
						Name = (string)UI.STARMAP.LISTTITLES.PASSENGERS,
						Items = new List<string> { stored.Count.ToString() }
					});
			}

			// Modules
			var moduleItems = new List<string>();
			foreach (var module in network)
				moduleItems.Add(module.GetProperName());
			if (moduleItems.Count > 0)
				categories.Add(new RocketCategory {
					Name = (string)UI.STARMAP.LISTTITLES.MODULES,
					Items = moduleItems
				});

			return categories;
		}

		// ========================================
		// DESTINATION DETAIL DATA
		// ========================================

		internal struct DestinationSection {
			public string Name;
			public List<string> Items;
		}

		internal static List<DestinationSection> BuildDestinationSections(
				SpaceDestination dest, Spacecraft activeRocket) {
			var sections = new List<DestinationSection>();
			var destType = dest.GetDestinationType();
			bool analyzed = IsAnalyzed(dest);

			// Header: identity info
			string headerName;
			var headerItems = new List<string>();
			if (analyzed)
				headerName = destType.Name;
			else
				headerName = (string)UI.STARMAP.UNKNOWN_DESTINATION;
			headerItems.Add(DisplayDistance(dest.OneBasedDistance * 10000f));
			if (analyzed && !string.IsNullOrEmpty(destType.description))
				headerItems.Add(destType.description);
			if (activeRocket != null
					&& activeRocket.state != Spacecraft.MissionState.Grounded) {
				var rocketDest = SpacecraftManager.instance.GetSpacecraftDestination(
					activeRocket.launchConditions);
				if (rocketDest != null && rocketDest.id == dest.id)
					headerItems.Add((string)UI.STARMAP.ROCKETSTATUS.LOCKEDIN);
			}
			sections.Add(new DestinationSection {
				Name = headerName,
				Items = headerItems
			});

			// Analysis
			var analysisItems = new List<string>();
			float score = SpacecraftManager.instance.GetDestinationAnalysisScore(dest.id);
			float analysisPct = score
				/ (float)TUNING.ROCKETRY.DESTINATION_ANALYSIS.COMPLETE * 100f;
			if (!analyzed)
				analysisItems.Add(string.Format(UI.STARMAP.ANALYSIS_AMOUNT,
					GameUtil.GetFormattedPercent(analysisPct)));
			else
				analysisItems.Add((string)UI.STARMAP.ANALYSIS_COMPLETE);
			int currentTarget = SpacecraftManager.instance
				.GetStarmapAnalysisDestinationID();
			if (currentTarget == dest.id)
				analysisItems.Add(STRINGS.ONIACCESS.STARMAP.ANALYZING_THIS);
			sections.Add(new DestinationSection {
				Name = (string)UI.STARMAP.LISTTITLES.ANALYSIS,
				Items = analysisItems
			});

			if (analyzed) {
				// Research
				if (dest.researchOpportunities.Count > 0) {
					var researchItems = new List<string>();
					foreach (var opp in dest.researchOpportunities) {
						bool rare = opp.discoveredRareResource != SimHashes.Void;
						string fmt;
						if (opp.completed)
							fmt = rare ? STRINGS.ONIACCESS.STARMAP.RESEARCH_COMPLETE_RARE
								: STRINGS.ONIACCESS.STARMAP.RESEARCH_COMPLETE;
						else
							fmt = rare ? STRINGS.ONIACCESS.STARMAP.RESEARCH_INCOMPLETE_RARE
								: STRINGS.ONIACCESS.STARMAP.RESEARCH_INCOMPLETE;
						researchItems.Add(string.Format(fmt, opp.description, opp.dataValue));
					}
					sections.Add(new DestinationSection {
						Name = (string)UI.STARMAP.LISTTITLES.RESEARCH,
						Items = researchItems
					});
				}

				// Mass
				var massItems = new List<string>();
				massItems.Add($"{UI.STARMAP.CURRENT_MASS}: {GameUtil.GetFormattedMass(dest.CurrentMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
				massItems.Add($"{UI.STARMAP.MAXIMUM_MASS}: {GameUtil.GetFormattedMass(destType.maxiumMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
				massItems.Add($"{UI.STARMAP.MINIMUM_MASS}: {GameUtil.GetFormattedMass(destType.minimumMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Tonne)}");
				massItems.Add($"{UI.STARMAP.REPLENISH_RATE}: {GameUtil.GetFormattedMass(destType.replishmentPerCycle, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.Kilogram)}");
				sections.Add(new DestinationSection {
					Name = (string)UI.STARMAP.LISTTITLES.MASS,
					Items = massItems
				});

				// Composition (elements)
				float totalMass = dest.GetTotalMass();
				var compItems = new List<string>();
				foreach (var kvp in dest.recoverableElements) {
					var element = ElementLoader.FindElementByHash(kvp.Key);
					if (element == null) continue;
					string elemName = element.name;
					if (string.IsNullOrEmpty(elemName))
						elemName = element.tag.ProperName();
					if (string.IsNullOrEmpty(elemName)) continue;
					float pct = totalMass > 0f
						? dest.GetResourceValue(kvp.Key, kvp.Value) / totalMass * 100f
						: 0f;
					string pctStr = pct <= 1f
						? (string)UI.STARMAP.COMPOSITION_SMALL_AMOUNT
						: GameUtil.GetFormattedPercent(pct);
					string compat = GetCargoCompatibility(element, activeRocket);
					compItems.Add($"{elemName}: {pctStr}{compat}");
				}
				foreach (var opp in dest.researchOpportunities) {
					if (!opp.completed && opp.discoveredRareResource != SimHashes.Void)
						compItems.Add($"{UI.STARMAP.COMPOSITION_UNDISCOVERED}: {UI.STARMAP.COMPOSITION_UNDISCOVERED_AMOUNT}");
				}
				if (compItems.Count > 0)
					sections.Add(new DestinationSection {
						Name = (string)UI.STARMAP.LISTTITLES.WORLDCOMPOSITION,
						Items = compItems
					});

				// Resources (entities)
				var entityItems = new List<string>();
				foreach (var kvp in dest.GetRecoverableEntities()) {
					var prefab = Assets.GetPrefab(kvp.Key);
					string name = prefab.GetProperName();
					string compat = GetEntityCompatibility(activeRocket);
					entityItems.Add($"{name}{compat}");
				}
				if (entityItems.Count > 0)
					sections.Add(new DestinationSection {
						Name = (string)UI.STARMAP.LISTTITLES.RESOURCES,
						Items = entityItems
					});

				// Artifacts
				var dropTable = destType.artifactDropTable;
				if (dropTable != null && dropTable.rates.Count > 0) {
					var artifactItems = new List<string>();
					foreach (var rate in dropTable.rates) {
						string tierName = Strings.Get(rate.first.name_key);
						float dropPct = rate.second / dropTable.totalWeight * 100f;
						artifactItems.Add($"{tierName}: {GameUtil.GetFormattedPercent(dropPct)}");
					}
					sections.Add(new DestinationSection {
						Name = (string)UI.STARMAP.LISTTITLES.ARTIFACTS,
						Items = artifactItems
					});
				}
			}

			// Analyze action (leaf at level 0 — empty Items)
			sections.Add(new DestinationSection {
				Name = GetAnalyzeActionLabel(dest),
				Items = new List<string>()
			});

			return sections;
		}

		internal static string GetAnalyzeActionLabel(SpaceDestination dest) {
			if (IsAnalyzed(dest))
				return (string)UI.STARMAP.ANALYSIS_COMPLETE;
			if (dest.id == SpacecraftManager.instance.GetStarmapAnalysisDestinationID())
				return (string)UI.STARMAP.SUSPEND_DESTINATION_ANALYSIS;
			return (string)UI.STARMAP.ANALYZE_DESTINATION;
		}

		// ========================================
		// LAUNCH
		// ========================================

		internal static string TryLaunch(Spacecraft rocket) {
			if (rocket == null)
				return STRINGS.ONIACCESS.STARMAP.NO_ROCKET_SELECTED;
			if (rocket.state != Spacecraft.MissionState.Grounded)
				return STRINGS.ONIACCESS.STARMAP.ROCKET_NOT_GROUNDED;

			var lcm = rocket.launchConditions;
			var dest = SpacecraftManager.instance.GetSpacecraftDestination(lcm);

			if (dest == null)
				return (string)UI.STARMAP.DESTINATIONSELECTION.NOTSELECTED;

			// Check all conditions, report all failures
			var failures = new List<string>();
			foreach (var condition in lcm.GetLaunchConditionList()) {
				var status = condition.EvaluateCondition();
				if (status == ProcessCondition.Status.Failure) {
					string msg = condition.GetStatusMessage(status);
					string tooltip = condition.GetStatusTooltip(status);
					if (!string.IsNullOrEmpty(tooltip))
						failures.Add($"{msg}: {tooltip}");
					else
						failures.Add(msg);
				}
			}

			if (failures.Count > 0)
				return string.Join(", ", failures.ToArray());

			lcm.Launch(dest);
			PlaySound("HUD_Click");
			return string.Format(STRINGS.ONIACCESS.STARMAP.LAUNCHED,
				rocket.GetRocketName());
		}

		// ========================================
		// CARGO COMPATIBILITY HELPERS
		// ========================================

		private static string GetCargoCompatibility(Element element, Spacecraft rocket) {
			if (rocket == null || rocket.state != Spacecraft.MissionState.Grounded)
				return "";
			var cmd = rocket.launchConditions.GetComponent<CommandModule>();
			var network = new List<UnityEngine.GameObject>();
			AttachableBuilding.CollectAttachedBuildings(
				cmd.GetComponent<AttachableBuilding>(), network);

			CargoBay.CargoType targetType;
			string prefabId;
			if (element.IsGas) {
				targetType = CargoBay.CargoType.Gasses;
				prefabId = "GasCargoBay";
			} else if (element.IsLiquid) {
				targetType = CargoBay.CargoType.Liquids;
				prefabId = "LiquidCargoBay";
			} else if (element.IsSolid) {
				targetType = CargoBay.CargoType.Solids;
				prefabId = "CargoBay";
			} else {
				return "";
			}

			string bayName = Assets.GetPrefab(prefabId.ToTag()).GetProperName();
			bool hasMatchingBay = false;
			foreach (var m in network) {
				var bay = m.GetComponent<CargoBay>();
				if (bay != null && bay.storageType == targetType) {
					hasMatchingBay = true;
					break;
				}
			}

			if (string.IsNullOrEmpty(bayName))
				return "";
			return hasMatchingBay
				? $" ({STRINGS.ONIACCESS.STARMAP.CAN_CARRY})"
				: $" ({STRINGS.ONIACCESS.STARMAP.NEEDS_BAY}: {bayName})";
		}

		private static string GetEntityCompatibility(Spacecraft rocket) {
			if (rocket == null || rocket.state != Spacecraft.MissionState.Grounded)
				return "";
			var cmd = rocket.launchConditions.GetComponent<CommandModule>();
			var network = new List<UnityEngine.GameObject>();
			AttachableBuilding.CollectAttachedBuildings(
				cmd.GetComponent<AttachableBuilding>(), network);

			bool hasBay = false;
			foreach (var m in network) {
				if (m.GetComponent<CargoBay>() != null
						&& m.GetComponent<CargoBay>().storageType
							== CargoBay.CargoType.Entities) {
					hasBay = true;
					break;
				}
			}

			string bayName = DlcManager.IsPureVanilla()
				? Assets.GetPrefab("SpecialCargoBay".ToTag()).GetProperName()
				: Assets.GetPrefab("SpecialCargoBayCluster".ToTag()).GetProperName();
			return hasBay
				? $" ({STRINGS.ONIACCESS.STARMAP.CAN_CARRY})"
				: $" ({STRINGS.ONIACCESS.STARMAP.NEEDS_BAY}: {bayName})";
		}

		// ========================================
		// FORMATTING
		// ========================================

		private static string DisplayDistance(float distanceInKm) {
			return $"{distanceInKm:0} {STRINGS.UI.UNITSUFFIXES.DISTANCE.KILOMETER}";
		}

		internal static void PlaySound(string clipName) {
			BaseScreenHandler.PlaySound(clipName);
		}
	}
}
