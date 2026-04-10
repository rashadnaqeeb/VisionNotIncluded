using System;
using System.Collections.Generic;
using HarmonyLib;
using OniAccess.Util;
using OniAccess.Widgets;
using UnityEngine;

namespace OniAccess.Handlers.Screens.Details {
	/// <summary>
	/// Reads the AdditionalDetailsPanel (Properties tab) into structured sections.
	/// Eight CollapsibleDetailContentPanel sections, each with a header and DetailLabel children.
	/// All widgets use SpeechFunc for live text since the game updates labels every frame.
	/// </summary>
	class PropertiesTab: IDetailTab {
		public string DisplayName => (string)STRINGS.UI.DETAILTABS.DETAILS.NAME;
		public int StartLevel => 0;
		public string GameTabId => "DETAILS";

		public bool IsAvailable(GameObject target) => true;

		public void OnTabSelected() { }

		private static readonly string[] SectionFields = {
			"detailsPanel",
			"immuneSystemPanel",
			"diseaseSourcePanel",
			"currentGermsPanel",
			"overviewPanel",
			"generatorsPanel",
			"consumersPanel",
			"batteriesPanel",
		};

		private static readonly Dictionary<string, LocString> NavGridDescriptions =
			new Dictionary<string, LocString> {
				{ "MinionNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.DUPLICANT },
				{ "WalkerNavGrid1x1", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.WALKER_1X1 },
				{ "WalkerBabyNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.WALKER_BABY },
				{ "WalkerNavGrid1x2", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.WALKER_1X2 },
				{ "WalkerNavGrid2x2", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.WALKER_2X2 },
				{ "DreckoNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.SURFACE_CLIMBER },
				{ "DreckoBabyNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.SURFACE_CLIMBER_BABY },
				{ "SquirrelNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.TREE_CLIMBER },
				{ "FloaterNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.FLOATER },
				{ "FlyerNavGrid1x1", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.FLYER_1X1 },
				{ "FlyerNavGrid1x2", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.FLYER_1X2 },
				{ "FlyerNavGrid2x2", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.FLYER_2X2 },
				{ "SwimmerNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.SWIMMER_1X1 },
				{ "SwimmerGrid2x2", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.SWIMMER_2X2 },
				{ "DiggerNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.DIGGER },
				{ "RobotNavGrid", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.ROBOT },
				{ "RobotFlyerGrid1x1", STRINGS.ONIACCESS.DETAILS.PATHING_DESC.ROBOT_FLYER },
			};

		public void Populate(GameObject target, List<DetailSection> sections) {
			var panel = FindPanel();
			if (panel == null) {
				Util.Log.Warn("PropertiesTab.Populate: AdditionalDetailsPanel not found");
				return;
			}

			// The handler switches the game's visual tab before calling Populate,
			// so the panel is already active with SetTarget called by the game.
			// Guard against edge cases where the panel hasn't been refreshed yet.
			if (panel.gameObject.activeSelf)
				panel.SetTarget(target);

			foreach (var fieldName in SectionFields) {
				CollapsibleDetailContentPanel gameSection;
				try {
					gameSection = Traverse.Create(panel)
						.Field<CollapsibleDetailContentPanel>(fieldName).Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"PropertiesTab: field '{fieldName}' read failed: {ex.Message}");
					continue;
				}

				if (gameSection == null || !gameSection.gameObject.activeSelf) continue;

				var section = CollapsiblePanelReader.BuildSection(gameSection);
				section.Key = fieldName;
				if (section.Items.Count > 0)
					sections.Add(section);
			}

			AppendRangeWidget(target, sections);
			AppendSkyVisibilityWidget(target, sections);
			AppendPathingWidget(target, sections);
			AppendDescriptionWidget(target, sections);
			AppendReachWidget(target, sections);
		}

		private static void AppendRangeWidget(GameObject target, List<DetailSection> sections) {
			var rangeVis = target.GetComponent<RangeVisualizer>();
			if (rangeVis == null) return;

			var detailsSection = sections.Find(s => s.Key == "detailsPanel");
			if (detailsSection == null) {
				detailsSection = new DetailSection { Key = "range", Header = "Range" };
				sections.Add(detailsSection);
			}

			detailsSection.Items.Add(new LabelWidget {
				Key = "range",
				SpeechFunc = () => FormatRange(target, rangeVis)
			});
		}

		private static string FormatRange(GameObject target, RangeVisualizer rangeVis) {
			Grid.PosToXY(target.transform.GetPosition(), out int bx, out int by);

			var origin = rangeVis.OriginOffset;
			var rMin = rangeVis.RangeMin;
			var rMax = rangeVis.RangeMax;

			if (target.TryGetComponent<Rotatable>(out var rot)) {
				origin = rot.GetRotatedOffset(origin);
				var a = rot.GetRotatedOffset(rMin);
				var b = rot.GetRotatedOffset(rMax);
				rMin = new Vector2I(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
				rMax = new Vector2I(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
			}

			int blX = bx + origin.x + rMin.x;
			int blY = by + origin.y + rMin.y;
			int trX = bx + origin.x + rMax.x;
			int trY = by + origin.y + rMax.y;

			return string.Format((string)STRINGS.ONIACCESS.DETAILS.RANGE,
				GridCoordinates.Format(blX, blY),
				GridCoordinates.Format(trX, trY));
		}

		private static void AppendPathingWidget(GameObject target, List<DetailSection> sections) {
			var navigator = target.GetComponent<Navigator>();
			if (navigator == null) return;

			var detailsSection = sections.Find(s => s.Key == "detailsPanel");
			if (detailsSection == null) {
				detailsSection = new DetailSection { Key = "detailsPanel", Header = "Details" };
				sections.Add(detailsSection);
			}

			detailsSection.Items.Insert(0, new LabelWidget {
				Key = "pathing",
				SpeechFunc = () => FormatPathing(navigator)
			});
		}

		private static void AppendDescriptionWidget(GameObject target, List<DetailSection> sections) {
			if (target.GetComponent<CreatureBrain>() == null) return;

			var prefabId = target.GetComponent<KPrefabID>();
			if (prefabId == null) return;

			string id = prefabId.PrefabID().ToString().ToUpper();
			string key = "STRINGS.ONIACCESS.CRITTER_DESCRIPTIONS." + id;
			if (!Strings.TryGet(key, out var entry)) {
				if (!id.EndsWith("BABY")) return;
				key = "STRINGS.ONIACCESS.CRITTER_DESCRIPTIONS."
					+ id.Substring(0, id.Length - 4);
				if (!Strings.TryGet(key, out entry)) return;
			}

			var detailsSection = sections.Find(s => s.Key == "detailsPanel");
			if (detailsSection == null) {
				detailsSection = new DetailSection { Key = "detailsPanel", Header = "Details" };
				sections.Add(detailsSection);
			}

			var insertIndex = detailsSection.Items.FindIndex(w => w.Key == "pathing");
			insertIndex = insertIndex >= 0 ? insertIndex + 1 : detailsSection.Items.Count;

			detailsSection.Items.Insert(insertIndex, new LabelWidget {
				Key = "description",
				Label = string.Format((string)STRINGS.ONIACCESS.INFO.CRITTER_DESCRIPTION, entry.String)
			});
		}

		private static void AppendReachWidget(GameObject target, List<DetailSection> sections) {
			if (target.GetComponent<MinionIdentity>() == null) return;

			var detailsSection = sections.Find(s => s.Key == "detailsPanel");
			if (detailsSection == null) {
				detailsSection = new DetailSection { Key = "detailsPanel", Header = "Details" };
				sections.Add(detailsSection);
			}

			var insertIndex = detailsSection.Items.FindIndex(w => w.Key == "pathing");
			insertIndex = insertIndex >= 0 ? insertIndex + 1 : detailsSection.Items.Count;

			detailsSection.Items.Insert(insertIndex, new LabelWidget {
				Key = "reach",
				SpeechFunc = () => (string)STRINGS.ONIACCESS.DETAILS.REACH
			});
		}

		private static void AppendSkyVisibilityWidget(GameObject target, List<DetailSection> sections) {
			if (!Util.SkyVisibility.TryGetSkyVisibilityInfo(target, out var info))
				return;

			var detailsSection = sections.Find(s => s.Key == "detailsPanel");
			if (detailsSection == null) {
				detailsSection = new DetailSection { Key = "detailsPanel", Header = "Details" };
				sections.Add(detailsSection);
			}

			detailsSection.Items.Insert(0, new LabelWidget {
				Key = "skyVisibility",
				SpeechFunc = () => FormatSkyVisibility(target, info)
			});
		}

		private static string FormatSkyVisibility(GameObject target, SkyVisibilityInfo info) {
			var blocked = Util.SkyVisibility.GetBlockedColumns(target, info);
			int originCell = GridCoordinates.GetOriginCell();
			int originX = Grid.CellColumn(originCell);
			int originY = Grid.CellRow(originCell);

			if (blocked.Count == 0) {
				if (info.verticalStep == 0) {
					int scanY = Grid.CellRow(Grid.OffsetCell(
						Grid.PosToCell(target), info.scanLeftOffset));
					return string.Format(
						(string)STRINGS.ONIACCESS.DETAILS.SKY_CLEAR_AT_HEIGHT,
						scanY - originY);
				}
				return (string)STRINGS.ONIACCESS.DETAILS.SKY_CLEAR;
			}

			if (info.verticalStep == 0) {
				int scanY = Grid.CellRow(Grid.OffsetCell(
					Grid.PosToCell(target), info.scanLeftOffset));
				var xValues = new List<string>(blocked.Count);
				foreach (var (wx, _) in blocked)
					xValues.Add((wx - originX).ToString());
				return string.Format(
					(string)STRINGS.ONIACCESS.DETAILS.SKY_BLOCKED_AT_HEIGHT,
					scanY - originY, string.Join(", ", xValues));
			}

			var pairs = new List<string>(blocked.Count);
			foreach (var (wx, wy) in blocked)
				pairs.Add(GridCoordinates.Format(wx, wy));
			return string.Format(
				(string)STRINGS.ONIACCESS.DETAILS.SKY_BLOCKED,
				string.Join("; ", pairs));
		}

		private static string FormatPathing(Navigator navigator) {
			var gridName = navigator.NavGridName;

			if (NavGridDescriptions.TryGetValue(gridName, out var desc))
				return string.Format((string)STRINGS.ONIACCESS.DETAILS.PATHING, (string)desc);

			Util.Log.Warn($"PropertiesTab: unmapped NavGrid '{gridName}'");
			return string.Format((string)STRINGS.ONIACCESS.DETAILS.PATHING,
				string.Format((string)STRINGS.ONIACCESS.DETAILS.PATHING_UNKNOWN, gridName));
		}

		private static AdditionalDetailsPanel FindPanel() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return null;

			var tabHeader = Traverse.Create(ds)
				.Field<DetailTabHeader>("tabHeader").Value;
			if (tabHeader == null) return null;

			var tabPanels = Traverse.Create(tabHeader)
				.Field<Dictionary<string, TargetPanel>>("tabPanels").Value;
			if (tabPanels == null || !tabPanels.TryGetValue("DETAILS", out var panel))
				return null;

			return panel as AdditionalDetailsPanel;
		}

	}
}
