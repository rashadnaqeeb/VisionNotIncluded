using System.Collections.Generic;
using HarmonyLib;

using OniAccess.Input;
using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for ModeSelectScreen (Survival vs No Sweat),
	/// ClusterCategorySelectionScreen (game mode select), and
	/// ColonyDestinationSelectScreen (asteroid selection + settings).
	///
	/// All three screens are part of the new game setup flow. This handler serves
	/// them all because they share semantics -- behavioral differences flow from
	/// which widgets are present, not from fundamentally different navigation.
	///
	/// ModeSelectScreen: two MultiToggle buttons (Survival / No Sweat).
	/// ClusterCategorySelectionScreen: simple list of MultiToggle buttons for game modes.
	/// ColonyDestinationSelectScreen: flat widget list -- cluster selector at position 0
	///   (Left/Right cycles clusters, Enter opens info), then action buttons below.
	///   Customize button opens a sub-view with three sub-tabs:
	///   Settings → Mixing → Story Traits (cycled with Tab/Shift+Tab, Escape exits).
	///
	/// Per locked decisions:
	/// - Game mode entries speak name + description together
	/// - Cluster selector speaks name, difficulty, traits, moons
	/// - Enter on cluster selector opens info submenu
	/// - Story traits speak name + guaranteed/forbidden state; Enter toggles
	/// - Mixing DLC toggles speak name + enabled/disabled; Enter toggles
	/// - Mixing cyclers speak name + value; Left/Right cycles
	/// - Settings speak "label, value" with Left/Right cycling
	/// - Actions: Shuffle, Coordinate field, Customize, Launch (no back button)
	/// - Tab/Shift+Tab does nothing on the main destination screen
	/// </summary>
	public class ColonySetupHandler: BaseWidgetHandler {
		// Sub-tabs inside Customize overlay
		private const int SubTabSettings = 0;
		private const int SubTabMixing = 1;
		private const int SubTabStoryTraits = 2;
		private const int SubTabCount = 3;

		private static readonly System.Type ClusterCategoryScreenType =
			AccessTools.TypeByName("ClusterCategorySelectionScreen");
		private static readonly System.Type ModeSelectScreenType =
			AccessTools.TypeByName("ModeSelectScreen");

		/// <summary>
		/// Ordered list of cluster keys from the destination panel.
		/// </summary>
		private List<string> _clusterKeys;

		/// <summary>
		/// Current index into _clusterKeys for Left/Right cycling.
		/// </summary>
		private int _clusterIndex;

		/// <summary>
		/// Whether we are in the info submenu for a cluster.
		/// </summary>
		private bool _inInfoSubmenu;

		/// <summary>
		/// Cluster key stored when entering the info submenu, used to restore position on exit.
		/// </summary>
		private string _infoClusterKey;

		/// <summary>
		/// Whether we are inside the Customize sub-view (Story Traits / Mixing / Settings).
		/// </summary>
		private bool _inCustomize;

		/// <summary>
		/// Which sub-tab is active inside the Customize overlay.
		/// </summary>
		private int _currentSubTab;

		/// <summary>
		/// Delays speech by one frame after cluster cycling or shuffle so traits
		/// have time to populate after OnAsteroidClicked triggers ReInitialize.
		/// Only used for runtime re-queries (Left/Right cycling, shuffle button),
		/// not for initial screen open,that case returns false from DiscoverWidgets
		/// and the base class handles the retry.
		/// </summary>
		private bool _pendingClusterRefresh;

		/// <summary>
		/// When true, the deferred refresh came from the Shuffle button and should
		/// announce the newly applied world traits instead of the focused button.
		/// </summary>
		private bool _pendingShuffleTraitSpeech;

		/// <summary>
		/// When true, the next cluster speech omits the "Choose a Destination" prefix.
		/// Set by Left/Right cycling so the repeated prefix isn't annoying.
		/// </summary>
		private bool _speakClusterNameOnly;

		/// <summary>
		/// Display name changes based on which screen is active.
		/// </summary>
		public override string DisplayName {
			get {
				if (IsClusterCategoryScreen || IsModeSelectScreen)
					return STRINGS.UI.FRONTEND.MODESELECTSCREEN.HEADER;
				return STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.TITLE;
			}
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		/// <summary>
		/// Whether the active screen is ClusterCategorySelectionScreen.
		/// </summary>
		private bool IsClusterCategoryScreen =>
			_screen != null && _screen.GetType() == ClusterCategoryScreenType;

		/// <summary>
		/// Whether the active screen is ModeSelectScreen (Survival vs No Sweat).
		/// </summary>
		private bool IsModeSelectScreen =>
			_screen != null && _screen.GetType() == ModeSelectScreenType;

		public ColonySetupHandler(KScreen screen) : base(screen) {
			HelpEntries = BuildHelpEntries();
		}

		public override void OnActivate() {
			base.OnActivate();

			try {
				var field = HarmonyLib.Traverse.Create(_screen).Field("coordinate")
					.GetValue<KInputTextField>();
				if (field != null)
					field.DeactivateInputField();
			} catch (System.Exception ex) {
				Util.Log.Warn($"ColonySetupHandler: failed to deactivate coordinate field: {ex.Message}");
			}
		}

		// ========================================
		// TAB NAVIGATION (Customize sub-tabs only)
		// ========================================

		protected override void NavigateTabForward() {
			if (IsClusterCategoryScreen || IsModeSelectScreen) return;

			if (_inCustomize) {
				_currentSubTab = (_currentSubTab + 1) % SubTabCount;
				if (_currentSubTab == 0) PlaySound("HUD_Click");
				RefreshSubTab();
				return;
			}

			// No-op on the main destination screen
		}

		protected override void NavigateTabBackward() {
			if (IsClusterCategoryScreen || IsModeSelectScreen) return;

			if (_inCustomize) {
				int prev = _currentSubTab;
				_currentSubTab = (_currentSubTab - 1 + SubTabCount) % SubTabCount;
				if (_currentSubTab == SubTabCount - 1 && prev == 0) PlaySound("HUD_Click");
				RefreshSubTab();
				return;
			}

			// No-op on the main destination screen
		}

		/// <summary>
		/// Sync the game's visible tab with our navigation state.
		/// In normal mode, always sync to game tab 1 (clusters).
		/// In Customize mode, sub-tabs map to game tabs 2/3/4.
		/// </summary>
		private void SyncGameTab() {
			if (IsClusterCategoryScreen || IsModeSelectScreen) return;
			if (_inCustomize) {
				// Game tabs: 2=StoryTraits, 3=Mixing, 4=Settings
				// Sub-tabs:  0=Settings,    1=Mixing,  2=StoryTraits
				int gameTabIdx = _currentSubTab == SubTabSettings ? 4
					: _currentSubTab == SubTabMixing ? 3 : 2;
				var st = Traverse.Create(_screen);
				st.Field("selectedMenuTabIdx").SetValue(gameTabIdx);
				st.Method("RefreshMenuTabs").GetValue();
				return;
			}
			// Main screen: always sync to game tab 1
			int tabIdx = 1;
			var stn = Traverse.Create(_screen);
			stn.Field("selectedMenuTabIdx").SetValue(tabIdx);
			stn.Method("RefreshMenuTabs").GetValue();
		}

		private string GetPanelName() {
			if (_inCustomize) {
				switch (_currentSubTab) {
					case SubTabStoryTraits: return STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.STORY_TRAITS_HEADER;
					case SubTabMixing: return STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.MIXING_SETTINGS_HEADER;
					case SubTabSettings: return STRINGS.UI.FRONTEND.NEWGAMESETTINGS.HEADER;
					default: return "";
				}
			}
			return "";
		}

		/// <summary>
		/// Clear search, re-discover widgets, and reset to position 0.
		/// Callers add their own speech after this returns.
		/// </summary>
		private void RediscoverAndReset() {
			_search.Clear();
			DiscoverWidgets(_screen);
			CurrentIndex = 0;
		}

		/// <summary>
		/// Refresh after switching a Customize sub-tab: sync the game tab,
		/// re-discover widgets, and speak the panel name + first widget.
		/// </summary>
		private void RefreshSubTab() {
			SyncGameTab();
			RediscoverAndReset();
			string name = GetPanelName();
			Speech.SpeechPipeline.SpeakInterrupt(name);
			if (_widgets.Count > 0)
				Speech.SpeechPipeline.SpeakQueued(GetWidgetSpeechText(_widgets[0]));
		}

		// ========================================
		// WIDGET DISCOVERY
		// ========================================

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			if (IsModeSelectScreen) {
				DiscoverModeSelectWidgets(screen);
			} else if (IsClusterCategoryScreen) {
				DiscoverGameModeWidgets(screen);
			} else if (_inCustomize) {
				switch (_currentSubTab) {
					case SubTabStoryTraits:
						DiscoverStoryTraitWidgets(screen);
						break;
					case SubTabMixing:
						DiscoverMixingWidgets(screen);
						break;
					case SubTabSettings:
						DiscoverSettingsWidgets(screen);
						break;
				}
			} else if (_inInfoSubmenu) {
				DiscoverClusterInfoWidgets(screen);
			} else {
				return DiscoverDestinationWidgets(screen);
			}
			return true;
		}

		/// <summary>
		/// Discover game mode buttons on ClusterCategorySelectionScreen.
		/// Each ButtonConfig has a MultiToggle with a headerLabel (name) and
		/// a description shown on hover. We build composite: "name, description".
		/// </summary>
		private void DiscoverGameModeWidgets(KScreen screen) {
			// ClusterCategorySelectionScreen has named ButtonConfig fields:
			// vanillaStyle, classicStyle, spacedOutStyle, eventStyle
			string[] configNames = { "vanillaStyle", "classicStyle", "spacedOutStyle", "eventStyle" };

			foreach (var configName in configNames) {
				var buttonConfig = Traverse.Create(screen).Field(configName).GetValue<object>();
				if (buttonConfig == null) continue;

				var configTraverse = Traverse.Create(buttonConfig);
				var multiToggle = configTraverse.Field("button").GetValue<MultiToggle>();
				if (multiToggle == null || !multiToggle.gameObject.activeInHierarchy) continue;

				var hoverDesc = configTraverse.Field("hoverDescriptionText").GetValue<string>();

				// Read title from game STRINGS,LocText.text returns prefab
				// placeholders (e.g. "_event") because SetText() populates
				// TMPro's internal buffer, not the .text property.
				string name = "";
				switch (configName) {
					case "vanillaStyle": name = STRINGS.UI.FRONTEND.CLUSTERCATEGORYSELECTSCREEN.VANILLA_TITLE; break;
					case "classicStyle": name = STRINGS.UI.FRONTEND.CLUSTERCATEGORYSELECTSCREEN.CLASSIC_TITLE; break;
					case "spacedOutStyle": name = STRINGS.UI.FRONTEND.CLUSTERCATEGORYSELECTSCREEN.SPACEDOUT_TITLE; break;
					case "eventStyle": name = STRINGS.UI.FRONTEND.CLUSTERCATEGORYSELECTSCREEN.EVENT_TITLE; break;
				}
				string label = !string.IsNullOrEmpty(hoverDesc)
					? $"{name}, {Speech.TextFilter.FilterForSpeech(hoverDesc)}"
					: name;

				_widgets.Add(new ButtonWidget {
					Label = label,
					Component = multiToggle,
					GameObject = multiToggle.gameObject
				});
			}
		}

		/// <summary>
		/// Discover Survival / No Sweat buttons on ModeSelectScreen.
		/// Each button is a MultiToggle field. We use the game's localized title
		/// strings and pair with description strings for a composite label.
		/// </summary>
		private void DiscoverModeSelectWidgets(KScreen screen) {
			var screenTraverse = Traverse.Create(screen);

			var survivalToggle = screenTraverse.Field("survivalButton").GetValue<MultiToggle>();
			if (survivalToggle != null && survivalToggle.gameObject.activeInHierarchy) {
				string name = STRINGS.UI.FRONTEND.MODESELECTSCREEN.SURVIVAL_TITLE;
				string desc = STRINGS.UI.FRONTEND.MODESELECTSCREEN.SURVIVAL_DESC;
				string label = $"{name}, {Speech.TextFilter.FilterForSpeech(desc)}";
				_widgets.Add(new ButtonWidget {
					Label = label,
					Component = survivalToggle,
					GameObject = survivalToggle.gameObject
				});
			}

			var nosweatToggle = screenTraverse.Field("nosweatButton").GetValue<MultiToggle>();
			if (nosweatToggle != null && nosweatToggle.gameObject.activeInHierarchy) {
				string name = STRINGS.UI.FRONTEND.MODESELECTSCREEN.NOSWEAT_TITLE;
				string desc = STRINGS.UI.FRONTEND.MODESELECTSCREEN.NOSWEAT_DESC;
				string label = $"{name}, {Speech.TextFilter.FilterForSpeech(desc)}";
				_widgets.Add(new ButtonWidget {
					Label = label,
					Component = nosweatToggle,
					GameObject = nosweatToggle.gameObject
				});
			}
		}

		/// <summary>
		/// Discover the flat destination widget list:
		/// Position 0 = cluster selector (Left/Right cycles, Enter = info)
		/// Position 1+ = Shuffle, Coordinate, Customize, Launch
		/// </summary>
		/// <returns>
		/// false if the destination panel isn't initialized yet (cluster keys not
		/// available). The base class will retry next frame via _pendingRediscovery.
		/// </returns>
		private bool DiscoverDestinationWidgets(KScreen screen) {
			// Position 0: cluster selector
			PopulateClusterKeys(screen);
			if (_clusterKeys == null || _clusterKeys.Count == 0) {
				// Panel not yet initialized (OnSpawn hasn't finished).
				// Signal "not ready" so the base class retries next frame.
				return false;
			}

			_clusterIndex = UnityEngine.Mathf.Clamp(_clusterIndex, 0, _clusterKeys.Count - 1);
			string clusterLabel = BuildClusterSelectorLabel(_clusterKeys[_clusterIndex]);
			_widgets.Add(new LabelWidget {
				Label = clusterLabel,
				GameObject = null,
				Tag = "cluster_selector",
				SpeechFunc = () => {
					if (_clusterKeys != null && _clusterIndex >= 0 && _clusterIndex < _clusterKeys.Count) {
						bool includePrefix = !_speakClusterNameOnly;
						_speakClusterNameOnly = false;
						return BuildClusterSelectorLabel(_clusterKeys[_clusterIndex], includePrefix);
					}
					return clusterLabel;
				}
			});

			// Action buttons (no back button)
			WidgetDiscoveryUtil.TryAddButtonField(screen, "shuffleButton", null, _widgets);

			// Coordinate text field
			try {
				var coordinate = Traverse.Create(screen).Field("coordinate")
					.GetValue<KInputTextField>();
				if (coordinate != null && coordinate.gameObject.activeInHierarchy) {
					string currentValue = coordinate.text ?? "";
					_widgets.Add(new TextInputWidget {
						Label = $"{((string)STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.COORDINATE).TrimEnd(':')}, {currentValue}",
						Component = coordinate,
						GameObject = coordinate.gameObject,
						SpeechFunc = () => $"{((string)STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.COORDINATE).TrimEnd(':')}, {coordinate.text}"
					});
				}
			} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.DiscoverWidgets(coordinate): {ex.Message}"); }

			WidgetDiscoveryUtil.TryAddButtonField(screen, "customizeButton", null, _widgets);
			WidgetDiscoveryUtil.TryAddButtonField(screen, "launchButton", null, _widgets);
			return true;
		}

		/// <summary>
		/// Extract cluster keys from the destination panel and sync _clusterIndex
		/// to the game's currently selected cluster.
		/// </summary>
		private void PopulateClusterKeys(KScreen screen) {
			var panelTraverse = Traverse.Create(screen).Field("destinationMapPanel");
			var panel = panelTraverse.GetValue<object>();
			if (panel == null) {
				_clusterKeys = null;
				return;
			}

			var pt = Traverse.Create(panel);
			_clusterKeys = pt.Field("clusterKeys").GetValue<List<string>>();
			if (_clusterKeys == null || _clusterKeys.Count == 0) return;

			// Sync to game's selected index
			int selectedIndex = pt.Field("selectedIndex").GetValue<int>();
			_clusterIndex = UnityEngine.Mathf.Clamp(selectedIndex, 0, _clusterKeys.Count - 1);
		}

		/// <summary>
		/// Build the label for the cluster selector widget showing the cluster
		/// at the given key: name, difficulty, N traits, N planetoids.
		/// </summary>
		private string BuildClusterSelectorLabel(string clusterKey, bool includePrefix = true) {
			var belt = GetBelt(clusterKey);
			if (belt == null) return clusterKey;

			// Cluster name
			string name = "";
			string rawName = belt.properName;
			if (!string.IsNullOrEmpty(rawName))
				name = Strings.Get(rawName);
			if (string.IsNullOrEmpty(name))
				name = belt.startWorldName;

			// Difficulty
			int diffIdx = UnityEngine.Mathf.Clamp(
				belt.difficulty, 0,
				ColonyDestinationAsteroidBeltData.survivalOptions.Count - 1);
			string difficulty = ColonyDestinationAsteroidBeltData.survivalOptions[diffIdx].first;

			// Trait count,only actual world traits (colored entries)
			var traits = belt.GetTraitDescriptors();
			int traitCount = 0;
			foreach (var trait in traits) {
				string t = trait.text?.Trim() ?? "";
				if (t.StartsWith("<color")) traitCount++;
			}

			// Moon count (Spaced Out only)
			int moonCount = belt.worlds != null ? belt.worlds.Count : 0;

			// Build label: name, difficulty, N traits, N planetoids
			string filteredName = Speech.TextFilter.FilterForSpeech(name);
			string label = includePrefix
				? $"{STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.TITLE}: {filteredName}"
				: filteredName;
			label += $", {Speech.TextFilter.FilterForSpeech(difficulty)}";
			label += $", {traitCount} {STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.TRAITS_HEADER}";
			if (moonCount > 0) {
				label += $", {moonCount} {STRINGS.ONIACCESS.PANELS.PLANETOIDS}";
			}

			// Navigation hint — normally we avoid inline hints per project rules,
			// but this is a label widget with hidden left/right cycling and enter
			// drill-in, which is unlike any other control in the mod.
			if (includePrefix)
				label += ". " + (string)STRINGS.ONIACCESS.COLONY_SETUP.CLUSTER_SELECTOR_HINT;

			return label;
		}

		/// <summary>
		/// Look up the belt data for a cluster key from the destination panel.
		/// Returns null if the panel or asteroid data isn't available yet.
		/// </summary>
		private ColonyDestinationAsteroidBeltData GetBelt(string clusterKey) {
			if (string.IsNullOrEmpty(clusterKey)) return null;
			var panel = Traverse.Create(_screen).Field("destinationMapPanel").GetValue<object>();
			if (panel == null) return null;
			var asteroidData = Traverse.Create(panel).Field("asteroidData")
				.GetValue<Dictionary<string, ColonyDestinationAsteroidBeltData>>();
			if (asteroidData == null) return null;
			return asteroidData.TryGetValue(clusterKey, out var belt) ? belt : null;
		}

		/// <summary>
		/// The cluster key currently selected for Left/Right cycling, or null.
		/// </summary>
		private string CurrentClusterKey() =>
			(_clusterKeys != null && _clusterIndex >= 0 && _clusterIndex < _clusterKeys.Count)
				? _clusterKeys[_clusterIndex] : null;

		/// <summary>
		/// Format the colored trait descriptors for one world into spoken labels.
		/// With includeTooltips, each label is "name, description" for the info
		/// submenu; without, just the trait name for the quick shuffle overview.
		/// Returns an empty list when the world has no traits; callers decide how
		/// to announce the empty case.
		/// </summary>
		private static List<string> BuildWorldTraitLabels(ColonyDestinationAsteroidBeltData belt, ProcGen.World world, bool includeTooltips) {
			var labels = new List<string>();
			foreach (var trait in belt.GenerateTraitDescriptors(world)) {
				string text = trait.text?.Trim() ?? "";
				if (!text.StartsWith("<color")) continue;
				string traitLabel = Speech.TextFilter.FilterForSpeech(text);
				if (includeTooltips) {
					string tooltip = trait.tooltip?.Trim() ?? "";
					if (!string.IsNullOrEmpty(tooltip))
						traitLabel += $", {Speech.TextFilter.FilterForSpeech(tooltip)}";
				}
				labels.Add(traitLabel);
			}
			return labels;
		}

		/// <summary>
		/// Build the post-shuffle announcement: each world in the current cluster
		/// spoken as "world name: trait, trait", worlds separated by periods, with
		/// "no traits" for any world that has none. Returns null if no cluster is
		/// resolvable so the caller falls back to the focused widget.
		/// </summary>
		private string BuildShuffleTraitsSpeech() {
			try {
				var belt = GetBelt(CurrentClusterKey());
				if (belt == null) return null;

				var allWorlds = new List<ProcGen.World>();
				if (belt.GetStartWorld != null) allWorlds.Add(belt.GetStartWorld);
				if (belt.worlds != null) allWorlds.AddRange(belt.worlds);

				var segments = new List<string>();
				foreach (var world in allWorlds) {
					string wName = world.GetProperName();
					if (string.IsNullOrEmpty(wName)) continue;
					var traitLabels = BuildWorldTraitLabels(belt, world, includeTooltips: false);
					string traits = traitLabels.Count > 0
						? string.Join(", ", traitLabels)
						: (string)STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.NO_TRAITS;
					segments.Add($"{Speech.TextFilter.FilterForSpeech(wName)}: {traits}");
				}

				return segments.Count > 0 ? string.Join(". ", segments) : null;
			} catch (System.Exception ex) {
				Util.Log.Error($"ColonySetupHandler.BuildShuffleTraitsSpeech: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Build info submenu widgets for the cluster stored in _infoClusterKey.
		/// Single-world clusters: description, difficulty, traits.
		/// Multi-world clusters: description, difficulty, nearby/distant asteroid lists,
		/// then per-world sections (name, description, traits).
		/// </summary>
		private void DiscoverClusterInfoWidgets(KScreen screen) {
			var belt = GetBelt(_infoClusterKey);
			if (belt == null) return;

			var startWorld = belt.GetStartWorld;
			bool hasPlanetoids = belt.worlds != null && belt.worlds.Count > 0;

			// Description (start world / cluster description)
			if (startWorld != null) {
				string desc = startWorld.GetProperDescription();
				if (!string.IsNullOrEmpty(desc)) {
					_widgets.Add(new LabelWidget {
						Label = $"{STRINGS.UI.FRONTEND.SCENARIOS_MENU.SCENARIO_DESCRIPTION}: {Speech.TextFilter.FilterForSpeech(desc)}"
					});
				}
			}

			// Difficulty + flavor text
			int diffIdx = UnityEngine.Mathf.Clamp(
				belt.difficulty, 0,
				ColonyDestinationAsteroidBeltData.survivalOptions.Count - 1);
			string diffName = ColonyDestinationAsteroidBeltData.survivalOptions[diffIdx].first;
			string diffTooltip = ColonyDestinationAsteroidBeltData.survivalOptions[diffIdx].second;
			string diffLabel = $"{STRINGS.ONIACCESS.INFO.DIFFICULTY}: {Speech.TextFilter.FilterForSpeech(diffName)}";
			if (!string.IsNullOrEmpty(diffTooltip))
				diffLabel += $", {Speech.TextFilter.FilterForSpeech(diffTooltip)}";
			_widgets.Add(new LabelWidget {
				Label = diffLabel
			});

			if (hasPlanetoids) {
				// Classify planetoids by distance using WorldPlacement.locationType
				var nearbyNames = new List<string>();
				var distantNames = new List<string>();
				var placements = belt.Layout != null ? belt.Layout.worldPlacements : null;

				foreach (var world in belt.worlds) {
					string worldName = world.GetProperName();
					if (string.IsNullOrEmpty(worldName)) continue;
					string filtered = Speech.TextFilter.FilterForSpeech(worldName);

					// Determine location type from matching WorldPlacement
					bool isDistant = false;
					if (placements != null) {
						foreach (var wp in placements) {
							if (wp.world == world.filePath) {
								isDistant = wp.locationType == ProcGen.WorldPlacement.LocationType.Cluster;
								break;
							}
						}
					}

					if (isDistant)
						distantNames.Add(filtered);
					else
						nearbyNames.Add(filtered);
				}

				if (nearbyNames.Count > 0) {
					string header = STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.HEADER_ASTEROID_NEARBY;
					_widgets.Add(new LabelWidget {
						Label = $"{header}: {string.Join(", ", nearbyNames)}"
					});
				}
				if (distantNames.Count > 0) {
					string header = STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.HEADER_ASTEROID_DISTANT;
					_widgets.Add(new LabelWidget {
						Label = $"{header}: {string.Join(", ", distantNames)}"
					});
				}

				// Per-world detail sections: start world first, then planetoids
				var allWorlds = new List<ProcGen.World>();
				if (startWorld != null) allWorlds.Add(startWorld);
				allWorlds.AddRange(belt.worlds);

				foreach (var world in allWorlds) {
					// World name header
					string wName = world.GetProperName();
					if (string.IsNullOrEmpty(wName)) continue;
					_widgets.Add(new LabelWidget {
						Label = Speech.TextFilter.FilterForSpeech(wName)
					});

					// World description
					string wDesc = world.GetProperDescription();
					if (!string.IsNullOrEmpty(wDesc)) {
						_widgets.Add(new LabelWidget {
							Label = $"{STRINGS.UI.FRONTEND.SCENARIOS_MENU.SCENARIO_DESCRIPTION}: {Speech.TextFilter.FilterForSpeech(wDesc)}"
						});
					}

					// World traits for this specific world
					var traitLabels = BuildWorldTraitLabels(belt, world, includeTooltips: true);
					if (traitLabels.Count > 0) {
						foreach (var traitLabel in traitLabels)
							_widgets.Add(new LabelWidget { Label = traitLabel });
					} else {
						string noTraits = STRINGS.WORLD_TRAITS.NO_TRAITS.NAME;
						string noTraitsDesc = STRINGS.WORLD_TRAITS.NO_TRAITS.DESCRIPTION;
						string label = Speech.TextFilter.FilterForSpeech(noTraits);
						if (!string.IsNullOrEmpty(noTraitsDesc))
							label += $", {Speech.TextFilter.FilterForSpeech(noTraitsDesc)}";
						_widgets.Add(new LabelWidget { Label = label });
					}
				}
			} else {
				// Single world,just show traits directly
				_widgets.Add(new LabelWidget {
					Label = $"{STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.TRAITS_HEADER}:"
				});

				var traitDescriptors = belt.GetTraitDescriptors();
				bool hasTraits = false;
				foreach (var trait in traitDescriptors) {
					string text = trait.text?.Trim() ?? "";
					if (string.IsNullOrEmpty(text)) continue;
					if (text.StartsWith("<i>")) {
						_widgets.Add(new LabelWidget {
							Label = Speech.TextFilter.FilterForSpeech(text)
						});
						hasTraits = true;
						continue;
					}
					if (!text.StartsWith("<color")) continue;

					string traitLabel = Speech.TextFilter.FilterForSpeech(text);
					string tooltip = trait.tooltip?.Trim() ?? "";
					if (!string.IsNullOrEmpty(tooltip))
						traitLabel += $", {Speech.TextFilter.FilterForSpeech(tooltip)}";
					_widgets.Add(new LabelWidget {
						Label = traitLabel
					});
					hasTraits = true;
				}

				if (!hasTraits) {
					string noTraits = STRINGS.WORLD_TRAITS.NO_TRAITS.NAME;
					_widgets.Add(new LabelWidget {
						Label = Speech.TextFilter.FilterForSpeech(noTraits)
					});
				}
			}
		}

		/// <summary>
		/// Discover game settings widgets from NewGameSettingsPanel.
		/// Each CustomGameSettingListWidget has Label and ValueLabel LocTexts.
		/// </summary>
		private void DiscoverSettingsWidgets(KScreen screen) {
			var settingsPanel = Traverse.Create(screen).Field("newGameSettingsPanel").GetValue<object>();
			if (settingsPanel == null) return;

			var widgets = Traverse.Create(settingsPanel).Field("widgets")
				.GetValue<List<CustomGameSettingWidget>>();
			if (widgets == null) return;

			foreach (var widget in widgets) {
				if (widget == null || !widget.gameObject.activeSelf) continue;

				if (widget is CustomGameSettingListWidget listWidget) {
					var wt = Traverse.Create(listWidget);
					var labelText = wt.Field("Label").GetValue<LocText>();
					var valueText = wt.Field("ValueLabel").GetValue<LocText>();
					string name = labelText != null ? labelText.text : "";
					string value = valueText != null ? valueText.text : "";
					if (string.IsNullOrEmpty(name)) {
						// LocText may be empty if parent hierarchy is inactive;
						// fall back to config.label from the setting definition
						var config = wt.Field("config").GetValue<object>();
						if (config != null) {
							name = Traverse.Create(config).Property("label").GetValue<string>() ?? "";
							if (string.IsNullOrEmpty(name))
								name = Traverse.Create(config).Property("id").GetValue<string>() ?? (string)STRINGS.ONIACCESS.INFO.SETTING;
						}
						if (string.IsNullOrEmpty(name)) name = (string)STRINGS.ONIACCESS.INFO.SETTING;
					}
					string label = !string.IsNullOrEmpty(value)
						? $"{name}, {value}"
						: name;
					_widgets.Add(new DropdownWidget {
						Label = label,
						Component = widget,
						GameObject = widget.gameObject,
						SpeechFunc = () => BuildSettingDropdownSpeech(widget, name)
					});
				} else if (widget is CustomGameSettingToggleWidget toggleWidget) {
					var wt = Traverse.Create(toggleWidget);
					var labelText = wt.Field("Label").GetValue<LocText>();
					var toggle = wt.Field("Toggle").GetValue<MultiToggle>();
					string name = labelText != null ? labelText.text : "";
					if (string.IsNullOrEmpty(name)) {
						var config = wt.Field("config").GetValue<object>();
						if (config != null) {
							name = Traverse.Create(config).Property("label").GetValue<string>() ?? "";
							if (string.IsNullOrEmpty(name))
								name = Traverse.Create(config).Property("id").GetValue<string>() ?? (string)STRINGS.ONIACCESS.INFO.SETTING;
						}
						if (string.IsNullOrEmpty(name)) name = (string)STRINGS.ONIACCESS.INFO.SETTING;
					}
					string state = (toggle != null && toggle.CurrentState == 1) ? (string)STRINGS.ONIACCESS.STATES.ENABLED : (string)STRINGS.ONIACCESS.STATES.DISABLED;
					_widgets.Add(new ToggleWidget {
						Label = $"{name}, {state}",
						Component = widget,
						GameObject = widget.gameObject,
						SpeechFunc = () => {
							var t = Traverse.Create(toggleWidget);
							var lt = t.Field("Label").GetValue<LocText>();
							var tg = t.Field("Toggle").GetValue<MultiToggle>();
							string n = lt != null ? lt.text : name;
							string s = (tg != null && tg.CurrentState == 1) ? (string)STRINGS.ONIACCESS.STATES.ENABLED : (string)STRINGS.ONIACCESS.STATES.DISABLED;
							return $"{n}, {s}";
						}
					});
				} else if (widget is CustomGameSettingSeed seedWidget) {
					var wt = Traverse.Create(seedWidget);
					var labelText = wt.Field("Label").GetValue<LocText>();
					var inputField = wt.Field("Input").GetValue<KInputTextField>();
					string name = labelText != null ? labelText.text : (string)STRINGS.ONIACCESS.PANELS.SEED;
					string value = inputField != null ? inputField.text : "";
					_widgets.Add(new ButtonWidget { // Enter randomizes seed
						Label = $"{name}, {value}",
						Component = widget,
						GameObject = widget.gameObject,
						SpeechFunc = () => {
							var s = Traverse.Create(seedWidget);
							var lt = s.Field("Label").GetValue<LocText>();
							var inp = s.Field("Input").GetValue<KInputTextField>();
							string n = lt != null ? lt.text : name;
							string v = inp != null ? inp.text : "";
							return $"{n}, {v}";
						}
					});
				}
			}
		}

		/// <summary>
		/// Discover story trait entries from StoryContentPanel.
		/// Each row has a label and a checkbox MultiToggle for toggling
		/// Forbidden/Guaranteed state.
		/// </summary>
		private void DiscoverStoryTraitWidgets(KScreen screen) {
			// storyTraitShuffleButton lives on ColonyDestinationSelectScreen
			WidgetDiscoveryUtil.TryAddButtonField(screen, "storyTraitShuffleButton", null, _widgets);

			var storyPanel = Traverse.Create(screen).Field("storyContentPanel").GetValue<object>();
			if (storyPanel == null) return;

			var spt = Traverse.Create(storyPanel);

			// Walk storyRowContainer children instead of the storyRows dictionary.
			// The private storyStates dict uses a private enum value type which
			// Traverse can't extract as IDictionary. Active children are spawned
			// in Db.Get().Stories.resources order; child 0 is the inactive prefab
			// template, so we track a separate story index for active rows only.
			var containerGO = spt.Field("storyRowContainer").GetValue<UnityEngine.GameObject>();
			if (containerGO == null) return;

			var stories = Db.Get().Stories.resources;
			var container = containerGO.transform;
			bool isPureVanilla = DlcManager.IsPureVanilla();
			int storyIdx = 0;

			for (int i = 0; i < container.childCount; i++) {
				try {
					var rowGO = container.GetChild(i).gameObject;
					if (rowGO == null || !rowGO.activeSelf) continue;
					if (storyIdx >= stories.Count) break;

					var hierRef = rowGO.GetComponent<HierarchyReferences>();
					if (hierRef == null) { storyIdx++; continue; }

					// Get name from the database,LocText.text is empty at discovery time
					string name = "";
					var storyTrait = stories[storyIdx].StoryTrait;
					if (storyTrait != null)
						name = Strings.Get(storyTrait.name);
					if (string.IsNullOrEmpty(name)) { storyIdx++; continue; }

					MultiToggle checkbox = null;
					if (hierRef.HasReference("checkbox"))
						checkbox = hierRef.GetReference<MultiToggle>("checkbox");
					if (checkbox == null) { storyIdx++; continue; }

					string storyId = stories[storyIdx].Id;

					// Read state from public CustomGameSettings API, not the private dict
					string state = STRINGS.ONIACCESS.STATES.FORBIDDEN;
					try {
						var level = CustomGameSettings.Instance.GetCurrentStoryTraitSetting(storyId);
						if (level != null && level.id == "Guaranteed")
							state = STRINGS.ONIACCESS.STATES.GUARANTEED;
					} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.DiscoverStoryWidgets(state): {ex.Message}"); }

					// Build label with description
					string label = $"{name}, {state}";
					try {
						if (storyTrait != null) {
							string desc = isPureVanilla
								? Strings.Get(storyTrait.description + "_SHORT")
								: Strings.Get(storyTrait.description);
							if (!string.IsNullOrEmpty(desc))
								label = $"{name}, {state}, {Speech.TextFilter.FilterForSpeech(desc)}";
						}
					} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.DiscoverStoryWidgets(desc): {ex.Message}"); }

					var capturedStoryId = storyId;
					_widgets.Add(new ToggleWidget {
						Label = label,
						Component = checkbox,
						GameObject = checkbox.gameObject,
						Tag = storyId,
						SpeechFunc = () => BuildStoryTraitSpeech(capturedStoryId, label)
					});
					storyIdx++;
				} catch (System.Exception ex) {
					Util.Log.Error($"ColonySetupHandler.DiscoverStoryWidgets: {ex.Message}");
					storyIdx++;
				}
			}
		}

		/// <summary>
		/// Discover mixing widgets from MixingContentPanel.
		/// Walks contentPanel children (sections) instead of the flat widget list
		/// so that section headers (DLC Content, Asteroid Mixing, Biome Mixing)
		/// are announced before each group of widgets.
		/// </summary>
		private void DiscoverMixingWidgets(KScreen screen) {
			var mixingPanel = Traverse.Create(screen).Field("mixingPanel").GetValue<object>();
			if (mixingPanel == null) return;

			var contentPanelGO = Traverse.Create(mixingPanel).Field("contentPanel")
				.GetValue<UnityEngine.GameObject>();
			if (contentPanelGO == null) return;

			var contentPanel = contentPanelGO.transform;
			for (int s = 0; s < contentPanel.childCount; s++) {
				var section = contentPanel.GetChild(s);
				if (section == null || !section.gameObject.activeSelf) continue;

				// Read section title from "Title/Title Text"
				var titleTransform = section.Find("Title");
				if (titleTransform == null) continue;
				var titleTextTransform = titleTransform.Find("Title Text");
				string sectionName = "";
				if (titleTextTransform != null) {
					var titleLocText = titleTextTransform.GetComponent<LocText>();
					if (titleLocText != null) {
						sectionName = "";
						// SetText() populates TMPro's internal buffer but not m_text;
						// ForceMeshUpdate() processes that buffer so GetParsedText() works
						try {
							titleLocText.ForceMeshUpdate();
							sectionName = titleLocText.GetParsedText() ?? "";
						} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.DiscoverMixingWidgets(title): {ex.Message}"); }
						// Fallback to .text, but reject prefab placeholders (start with _)
						if (string.IsNullOrEmpty(sectionName)) {
							string raw = titleLocText.text ?? "";
							if (!raw.StartsWith("_")) sectionName = raw;
						}
						if (string.IsNullOrEmpty(sectionName)) {
							// Fallback: LocText.key stores the string table key
							string key = Traverse.Create(titleLocText).Field("key").GetValue<string>();
							if (!string.IsNullOrEmpty(key))
								sectionName = Strings.Get(new StringKey(key));
						}
					}
				}

				// Check if section content is visible (collapsible toggle)
				var contentTransform = section.Find("Content");
				if (contentTransform == null || !contentTransform.gameObject.activeSelf) continue;

				var gridTransform = contentTransform.Find("Grid");
				if (gridTransform == null) continue;

				// Count visible children in the grid
				int visibleCount = 0;
				for (int c = 0; c < gridTransform.childCount; c++) {
					if (gridTransform.GetChild(c).gameObject.activeSelf) visibleCount++;
				}

				// If no visible widgets, check for "no options" label
				if (visibleCount == 0) {
					var noOptionsTransform = contentTransform.Find("LabelNoOptions");
					if (noOptionsTransform != null && noOptionsTransform.gameObject.activeSelf)
						continue; // Skip empty section entirely
				}

				// Emit section header
				if (!string.IsNullOrEmpty(sectionName)) {
					_widgets.Add(new LabelWidget {
						Label = sectionName
					});
				}

				// Walk grid children for actual setting widgets
				for (int c = 0; c < gridTransform.childCount; c++) {
					var widgetGO = gridTransform.GetChild(c);
					if (widgetGO == null || !widgetGO.gameObject.activeSelf) continue;

					var widget = widgetGO.GetComponent<CustomGameSettingWidget>();
					if (widget == null) continue;

					// DLC toggle: has "Checkbox" child
					var checkboxTransform = widgetGO.Find("Checkbox");
					if (checkboxTransform != null) {
						var overlayDisabled = checkboxTransform.Find("OverlayDisabled");
						if (overlayDisabled != null && overlayDisabled.gameObject.activeSelf) continue;

						var toggle = checkboxTransform.GetComponent<MultiToggle>();
						if (toggle == null) continue;

						var labelLocText = widgetGO.Find("Label");
						string name = "";
						if (labelLocText != null) {
							var lt = labelLocText.GetComponent<LocText>();
							if (lt != null) name = lt.text;
						}
						if (string.IsNullOrEmpty(name)) continue;

						string state = toggle.CurrentState == 1 ? (string)STRINGS.ONIACCESS.STATES.ENABLED : (string)STRINGS.ONIACCESS.STATES.DISABLED;
						var mixToggle = toggle;
						var mixGO = widget.gameObject;
						string mixLabel = name;
						_widgets.Add(new ToggleWidget {
							Label = $"{name}, {state}",
							Component = toggle,
							GameObject = mixGO,
							SpeechFunc = () => {
								string n = "";
								if (mixGO != null) {
									var lt2 = mixGO.transform.Find("Label");
									if (lt2 != null) {
										var loc = lt2.GetComponent<LocText>();
										if (loc != null) n = loc.text;
									}
								}
								if (string.IsNullOrEmpty(n)) n = mixLabel;
								string s = mixToggle.CurrentState == 1 ? (string)STRINGS.ONIACCESS.STATES.ENABLED : (string)STRINGS.ONIACCESS.STATES.DISABLED;
								return $"{n}, {s}";
							}
						});
						continue;
					}

					// Cycler widget: has "Cycler" child
					var cyclerTransform = widgetGO.Find("Cycler");
					if (cyclerTransform != null) {
						var overlayDisabled = cyclerTransform.Find("OverlayDisabled");
						if (overlayDisabled != null && overlayDisabled.gameObject.activeSelf) continue;

						var labelLocText = widgetGO.Find("Label");
						string name = "";
						if (labelLocText != null) {
							var lt = labelLocText.GetComponent<LocText>();
							if (lt != null) name = lt.text;
						}
						if (string.IsNullOrEmpty(name)) continue;

						string value = "";
						var boxTransform = cyclerTransform.Find("Box");
						if (boxTransform != null) {
							var valueLabelTransform = boxTransform.Find("Value Label");
							if (valueLabelTransform != null) {
								var vlt = valueLabelTransform.GetComponent<LocText>();
								if (vlt != null) value = vlt.text;
							}
						}

						string label = !string.IsNullOrEmpty(value)
							? $"{name}, {value}"
							: name;

						_widgets.Add(new DropdownWidget {
							Label = label,
							Component = widget,
							GameObject = widget.gameObject,
							SpeechFunc = () => BuildSettingDropdownSpeech(widget, name)
						});
					}
				}
			}
		}

		// ========================================
		// WIDGET VALIDITY
		// ========================================


		// ========================================
		// WIDGET SPEECH
		// ========================================

		private string BuildStoryTraitSpeech(string storyId, string fallbackLabel) {
			string state = STRINGS.ONIACCESS.STATES.FORBIDDEN;
			try {
				var level = CustomGameSettings.Instance.GetCurrentStoryTraitSetting(storyId);
				if (level != null && level.id == "Guaranteed")
					state = STRINGS.ONIACCESS.STATES.GUARANTEED;
			} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.BuildStoryTraitSpeech(state): {ex.Message}"); }

			string name = "";
			try {
				var story = Db.Get().Stories.Get(storyId);
				if (story?.StoryTrait != null)
					name = Strings.Get(story.StoryTrait.name);
			} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.BuildStoryTraitSpeech(name): {ex.Message}"); }
			if (string.IsNullOrEmpty(name)) name = fallbackLabel;

			string label = $"{name}, {state}";
			try {
				bool isPureVanilla = DlcManager.IsPureVanilla();
				var story = Db.Get().Stories.Get(storyId);
				if (story?.StoryTrait != null) {
					string desc = isPureVanilla
						? Strings.Get(story.StoryTrait.description + "_SHORT")
						: Strings.Get(story.StoryTrait.description);
					if (!string.IsNullOrEmpty(desc))
						label = $"{name}, {state}, {Speech.TextFilter.FilterForSpeech(desc)}";
				}
			} catch (System.Exception ex) { Util.Log.Error($"ColonySetupHandler.BuildStoryTraitSpeech(desc): {ex.Message}"); }
			return label;
		}

		private static string BuildSettingDropdownSpeech(CustomGameSettingWidget settingWidget, string fallbackName) {
			var wt = Traverse.Create(settingWidget);
			var labelText = wt.Field("Label").GetValue<LocText>();
			var valueText = wt.Field("ValueLabel").GetValue<LocText>();
			string name = labelText != null ? labelText.text : "";
			if (string.IsNullOrEmpty(name)) name = fallbackName;

			string value = valueText != null ? valueText.text : "";

			if (string.IsNullOrEmpty(value) && settingWidget.gameObject != null) {
				var cyclerTransform = settingWidget.transform.Find("Cycler");
				if (cyclerTransform != null) {
					var boxTransform = cyclerTransform.Find("Box");
					if (boxTransform != null) {
						var valueLabelTransform = boxTransform.Find("Value Label");
						if (valueLabelTransform != null) {
							var vlt = valueLabelTransform.GetComponent<LocText>();
							if (vlt != null) value = vlt.text;
						}
					}
				}
			}

			return !string.IsNullOrEmpty(value) ? $"{name}, {value}" : name;
		}

		// ========================================
		// WIDGET INTERACTION
		// ========================================

		/// <summary>
		/// Activate the current widget:
		/// - Game mode buttons: invoke MultiToggle.onClick (selects mode, may auto-advance)
		/// - Cluster selector: open info submenu
		/// - Text input: enter edit mode
		/// - Other: base behavior
		/// </summary>
		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];

			// Game mode: MultiToggle click
			if ((IsClusterCategoryScreen || IsModeSelectScreen) && widget.Component is MultiToggle toggle) {
				toggle.onClick?.Invoke();
				return;
			}

			// Cluster selector: open info submenu
			if (CurrentIndex == 0 && widget.Tag is string selectorTag && selectorTag == "cluster_selector") {
				if (_clusterKeys != null && _clusterIndex >= 0 && _clusterIndex < _clusterKeys.Count) {
					_inInfoSubmenu = true;
					_infoClusterKey = _clusterKeys[_clusterIndex];
					RediscoverAndReset();
					Speech.SpeechPipeline.SpeakInterrupt((string)STRINGS.UI.FRONTEND.COLONYDESTINATIONSCREEN.SELECTED_CLUSTER_TRAITS_HEADER);
					if (_widgets.Count > 0)
						Speech.SpeechPipeline.SpeakQueued(GetWidgetSpeechText(_widgets[0]));
				}
				return;
			}

			// Story trait toggle: invoke checkbox onClick, then speak new state
			if (_inCustomize && _currentSubTab == SubTabStoryTraits && widget is ToggleWidget
				&& widget.Component is MultiToggle storyCheckbox) {
				storyCheckbox.onClick?.Invoke();
				Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(widget));
				return;
			}

			// Mixing DLC toggle: invoke MultiToggle onClick, then announce new state
			// ChangeState() runs async (next Update), so compute new state from old
			if (_inCustomize && _currentSubTab == SubTabMixing && widget is ToggleWidget
				&& widget.Component is MultiToggle mixingToggle) {
				string newState = mixingToggle.CurrentState == 1 ? (string)STRINGS.ONIACCESS.STATES.DISABLED : (string)STRINGS.ONIACCESS.STATES.ENABLED;
				mixingToggle.onClick?.Invoke();
				string name = widget.Label.Contains(",") ? widget.Label.Substring(0, widget.Label.IndexOf(',')) : widget.Label;
				Speech.SpeechPipeline.SpeakInterrupt($"{name}, {newState}");
				return;
			}

			// Customize button: open customSettings overlay and enter Customize sub-view
			if (widget.Component is KButton customizeBtn) {
				var btnField = Traverse.Create(_screen).Field("customizeButton").GetValue<KButton>();
				if (btnField != null && btnField == customizeBtn) {
					Traverse.Create(_screen).Method("CustomizeClicked").GetValue();
					_inCustomize = true;
					_currentSubTab = SubTabSettings;
					// Switch game tab to Settings (tab index 4)
					var st = Traverse.Create(_screen);
					st.Field("selectedMenuTabIdx").SetValue(4);
					st.Method("RefreshMenuTabs").GetValue();
					RediscoverAndReset();
					string panelName = GetPanelName();
					Speech.SpeechPipeline.SpeakInterrupt(panelName);
					if (_widgets.Count > 0)
						Speech.SpeechPipeline.SpeakQueued(GetWidgetSpeechText(_widgets[0]));
					return;
				} else {
					Util.Log.Warn("ColonySetupHandler: customizeButton field not found via Traverse");
				}
			}

			// Shuffle button: activate and flag for cluster refresh
			if (widget.Component is KButton shuffleCandidate) {
				var shuffleField = Traverse.Create(_screen).Field("shuffleButton").GetValue<KButton>();
				if (shuffleField != null && shuffleField == shuffleCandidate) {
					base.ActivateCurrentItem();
					_pendingClusterRefresh = true;
					_pendingShuffleTraitSpeech = true;
					return;
				} else {
					Util.Log.Warn("ColonySetupHandler: shuffleButton field not found via Traverse");
				}
			}

			// Settings toggle: compute new state from old, then call ToggleSetting()
			// ChangeState() runs async (next Update), so read before toggling
			if (_inCustomize && _currentSubTab == SubTabSettings && widget.Component is CustomGameSettingToggleWidget settingsToggle) {
				var twt = Traverse.Create(settingsToggle);
				var settingsMultiToggle = twt.Field("Toggle").GetValue<MultiToggle>();
				string newState = (settingsMultiToggle != null && settingsMultiToggle.CurrentState == 1) ? (string)STRINGS.ONIACCESS.STATES.DISABLED : (string)STRINGS.ONIACCESS.STATES.ENABLED;
				var labelText = twt.Field("Label").GetValue<LocText>();
				string name = labelText != null ? labelText.text : widget.Label;
				settingsToggle.ToggleSetting();
				settingsToggle.Refresh();
				var toggleTooltip = twt.Field("ToggleToolTip").GetValue<ToolTip>();
				string tooltip = toggleTooltip != null ? (ReadAllTooltipText(toggleTooltip) ?? "") : "";
				string speech = $"{name}, {newState}";
				if (!string.IsNullOrEmpty(tooltip))
					speech += $", {Speech.TextFilter.FilterForSpeech(tooltip)}";
				Speech.SpeechPipeline.SpeakInterrupt(speech);
				return;
			}

			// Settings seed: randomize, then speak new value
			if (_inCustomize && _currentSubTab == SubTabSettings && widget.Component is CustomGameSettingSeed settingsSeed) {
				var randomizeBtn = Traverse.Create(settingsSeed).Field("RandomizeButton")
					.GetValue<KButton>();
				if (randomizeBtn != null)
					ClickButton(randomizeBtn);
				Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(widget));
				return;
			}

			base.ActivateCurrentItem();
		}

		/// <summary>
		/// Select a cluster without speaking,used by Left/Right cycling.
		/// Fires OnAsteroidClicked to populate traits; speech comes after
		/// the one-frame delay via _pendingClusterRefresh.
		/// </summary>
		private void SelectClusterSilent(string clusterKey) {
			var panelTraverse = Traverse.Create(_screen).Field("destinationMapPanel");
			var panel = panelTraverse.GetValue<object>();
			if (panel == null) return;

			var pt = Traverse.Create(panel);
			var asteroidData = pt.Field("asteroidData")
				.GetValue<Dictionary<string, ColonyDestinationAsteroidBeltData>>();

			if (asteroidData != null && asteroidData.TryGetValue(clusterKey, out var belt)) {
				var onClicked = pt.Field("OnAsteroidClicked")
					.GetValue<System.Action<ColonyDestinationAsteroidBeltData>>();
				onClicked?.Invoke(belt);
			}
		}

		/// <summary>
		/// Cycle dropdown for settings widgets.
		/// CustomGameSettingListWidget has CycleLeft/CycleRight KButtons.
		/// We invoke the appropriate cycle direction.
		/// </summary>
		protected override void CycleDropdown(Widget widget, int direction) {
			if (!(widget.Component is CustomGameSettingWidget settingWidget)) return;

			var wt = Traverse.Create(settingWidget);
			bool cycled = false;

			// Snapshot current value to detect boundary no-ops
			string oldText = GetWidgetSpeechText(widget);

			// Try standard CycleLeft/CycleRight fields (game settings)
			if (direction > 0) {
				var cycleRight = wt.Field("CycleRight").GetValue<KButton>();
				if (cycleRight != null && cycleRight.isInteractable) {
					ClickButton(cycleRight);
					cycled = true;
				}
			} else {
				var cycleLeft = wt.Field("CycleLeft").GetValue<KButton>();
				if (cycleLeft != null && cycleLeft.isInteractable) {
					ClickButton(cycleLeft);
					cycled = true;
				}
			}

			// Fallback: try Cycler/Arrow_Left and Cycler/Arrow_Right (mixing widgets)
			if (!cycled) {
				var cyclerTransform = settingWidget.transform.Find("Cycler");
				if (cyclerTransform != null) {
					string arrowName = direction > 0 ? "Arrow_Right" : "Arrow_Left";
					var arrowTransform = cyclerTransform.Find(arrowName);
					if (arrowTransform != null) {
						var arrowButton = arrowTransform.GetComponent<KButton>();
						if (arrowButton != null && arrowButton.isInteractable) {
							ClickButton(arrowButton);
							cycled = true;
						}
					}
				}
			}

			if (!cycled) return;

			// Force synchronous UI refresh,the game defers Refresh() to the
			// next Update() via isDirty, so value labels are stale without this
			settingWidget.Refresh();

			// Read the value tooltip after refresh (describes what the new value does)
			string valueTooltip = "";
			if (settingWidget is CustomGameSettingListWidget) {
				var vtt = Traverse.Create(settingWidget).Field("ValueToolTip").GetValue<ToolTip>();
				if (vtt != null)
					valueTooltip = ReadAllTooltipText(vtt) ?? "";
			}

			// Only announce if the value actually changed (boundary clamp = no-op)
			string newText = GetWidgetSpeechText(widget);
			if (newText != oldText) {
				string speech = newText;
				if (!string.IsNullOrEmpty(valueTooltip))
					speech += $", {Speech.TextFilter.FilterForSpeech(valueTooltip)}";
				Speech.SpeechPipeline.SpeakInterrupt(speech);
			}
		}

		// ========================================
		// TICK: CLUSTER CYCLING
		// ========================================

		/// <summary>
		/// Handle Left/Right cluster cycling before base navigation.
		/// Text edit mode (Return to confirm, Escape to cancel) is handled by base.
		/// </summary>
		public override bool Tick() {
			// Deferred cluster refresh: traits needed one frame to populate
			// after Left/Right cycling or shuffle fired OnAsteroidClicked.
			// Re-discover widgets and speak the current cluster.
			if (_pendingClusterRefresh) {
				bool speakShuffleTraits = _pendingShuffleTraitSpeech;
				_pendingClusterRefresh = false;
				_pendingShuffleTraitSpeech = false;
				int savedIndex = CurrentIndex;
				DiscoverWidgets(_screen);
				CurrentIndex = UnityEngine.Mathf.Clamp(savedIndex, 0,
					_widgets.Count > 0 ? _widgets.Count - 1 : 0);
				if (speakShuffleTraits) {
					string traitsSpeech = BuildShuffleTraitsSpeech();
					if (!string.IsNullOrEmpty(traitsSpeech))
						Speech.SpeechPipeline.SpeakInterrupt(traitsSpeech);
					else
						SpeakCurrentWidget();
				} else {
					SpeakCurrentWidget();
				}
				return false;
			}

			// Left/Right cluster cycling when on the cluster selector (index 0)
			if (!IsClusterCategoryScreen && !IsModeSelectScreen
				&& !_inInfoSubmenu && !_inCustomize
				&& CurrentIndex == 0 && _widgets.Count > 0
				&& _widgets[0].Tag is string sTag && sTag == "cluster_selector"
				&& _clusterKeys != null && _clusterKeys.Count > 0) {

				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) {
					int next = (_clusterIndex + 1) % _clusterKeys.Count;
					if (next == 0) PlaySound("HUD_Click");
					_clusterIndex = next;
					SelectClusterSilent(_clusterKeys[_clusterIndex]);
					_speakClusterNameOnly = true;
					_pendingClusterRefresh = true;
					return true;
				}

				if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow)) {
					int next = (_clusterIndex - 1 + _clusterKeys.Count) % _clusterKeys.Count;
					if (next == _clusterKeys.Count - 1) PlaySound("HUD_Click");
					_clusterIndex = next;
					SelectClusterSilent(_clusterKeys[_clusterIndex]);
					_speakClusterNameOnly = true;
					_pendingClusterRefresh = true;
					return true;
				}
			}

			return base.Tick();
		}

		/// <summary>
		/// Intercept Escape for Customize sub-view and info submenu exits.
		/// Text edit Escape handling is in base.HandleKeyDown.
		/// </summary>
		public override bool HandleKeyDown(KButtonEvent e) {
			if (base.HandleKeyDown(e)) return true;

			// Escape exits Customize sub-view back to main destination list
			if (_inCustomize) {
				if (e.TryConsume(Action.Escape)) {
					Traverse.Create(_screen).Method("CustomizeClose").GetValue();
					_inCustomize = false;
					SyncGameTab();
					RediscoverAndReset();
					if (_widgets.Count > 0)
						Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
					return true;
				}
			}

			// Escape exits info submenu back to cluster selector
			if (_inInfoSubmenu) {
				if (e.TryConsume(Action.Escape)) {
					_inInfoSubmenu = false;
					RediscoverAndReset();
					if (_widgets.Count > 0)
						Speech.SpeechPipeline.SpeakInterrupt(GetWidgetSpeechText(_widgets[0]));
					return true;
				}
			}

			return false;
		}

	}
}
