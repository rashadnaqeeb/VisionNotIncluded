using System.Collections.Generic;
using Database;
using HarmonyLib;
using UnityEngine;

namespace OniAccess.Handlers.Screens.Details {
	/// <summary>
	/// Reads the MinionPersonalityPanel (Bio tab) into structured sections.
	/// Six CollapsibleDetailContentPanel sections (bio, traits, attributes,
	/// resume, amenities, equipment), all populated via SetLabel/Commit.
	/// Same reading strategy as PropertiesTab via CollapsiblePanelReader.
	/// </summary>
	class PersonalityTab: IDetailTab {
		public string DisplayName => (string)STRINGS.UI.DETAILTABS.PERSONALITY.NAME;
		public int StartLevel => 0;
		public string GameTabId => "PERSONALITY";

		public bool IsAvailable(GameObject target) => true;

		public void OnTabSelected() { }

		private static readonly string[] SectionFields = {
			"bioPanel",
			"traitsPanel",
			"attributesPanel",
			"resumePanel",
			"amenitiesPanel",
			"equipmentPanel",
		};

		public void Populate(GameObject target, List<DetailSection> sections) {
			var panel = FindPanel();
			if (panel == null) {
				Util.Log.Warn("PersonalityTab.Populate: MinionPersonalityPanel not found");
				return;
			}

			if (panel.gameObject.activeSelf)
				panel.SetTarget(target);

			foreach (var fieldName in SectionFields) {
				CollapsibleDetailContentPanel gameSection;
				try {
					gameSection = Traverse.Create(panel)
						.Field<CollapsibleDetailContentPanel>(fieldName).Value;
				} catch (System.Exception ex) {
					Util.Log.Warn($"PersonalityTab: field '{fieldName}' read failed: {ex.Message}");
					continue;
				}

				if (gameSection == null || !gameSection.gameObject.activeSelf) continue;

				var section = CollapsiblePanelReader.BuildSection(gameSection);
				section.Key = fieldName;
				if (section.Items.Count > 0)
					sections.Add(section);
			}

			AppendAppearanceWidget(target, sections);
		}

		private static void AppendAppearanceWidget(GameObject target, List<DetailSection> sections) {
			var identity = target.GetComponent<MinionIdentity>();
			if (identity == null) return;

			var personality = Db.Get().Personalities.Get(identity.personalityResourceId);
			if (personality == null) return;

			string key = "STRINGS.ONIACCESS.DUPE_DESCRIPTIONS."
				+ personality.Name.Replace("-", "_").ToUpper();
			if (!Strings.TryGet(key, out var entry)) return;

			var bioSection = sections.Find(s => s.Key == "bioPanel");
			if (bioSection == null) return;

			bioSection.Items.Add(new Widgets.LabelWidget {
				Key = "appearance",
				Label = string.Format((string)STRINGS.ONIACCESS.INFO.DUPE_DESCRIPTION, entry.String)
			});
		}

		private static MinionPersonalityPanel FindPanel() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return null;

			var tabHeader = Traverse.Create(ds)
				.Field<DetailTabHeader>("tabHeader").Value;
			if (tabHeader == null) return null;

			var tabPanels = Traverse.Create(tabHeader)
				.Field<Dictionary<string, TargetPanel>>("tabPanels").Value;
			if (tabPanels == null || !tabPanels.TryGetValue("PERSONALITY", out var panel))
				return null;

			return panel as MinionPersonalityPanel;
		}
	}
}
