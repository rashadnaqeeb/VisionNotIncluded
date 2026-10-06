using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Details {
	/// <summary>
	/// Reads active Config side screens into sections for the details screen.
	/// Each active SideScreenContent becomes one section whose header is
	/// the screen's GetTitle() and whose items are discovered by SideScreenWalker.
	/// </summary>
	class ConfigSideTab: IDetailTab {
		public string DisplayName => (string)STRINGS.UI.DETAILTABS.CONFIGURATION.NAME;
		public int StartLevel => 1;
		public string GameTabId => null;

		public bool IsAvailable(GameObject target) {
			var tab = DetailsScreen.Instance?.GetTabOfType(
				DetailsScreen.SidescreenTabTypes.Config);
			return tab != null && tab.IsVisible;
		}

		public void OnTabSelected() {
			var tab = DetailsScreen.Instance?.GetTabOfType(
				DetailsScreen.SidescreenTabTypes.Config);
			if (tab?.tabInstance != null)
				WidgetOps.ClickMultiToggle(tab.tabInstance);
		}

		public void Populate(GameObject target, List<DetailSection> sections) {
			var ds = DetailsScreen.Instance;
			if (ds == null) return;

			foreach (var screen in GetActiveScreens(
					ds, DetailsScreen.SidescreenTabTypes.Config)) {
				var section = new DetailSection();
				section.Key = screen.GetType().Name;
				string title = screen.GetTitle();
				if (title != null && title.StartsWith("MISSING"))
					title = GetFallbackTitle(screen);
				section.Header = title;
				SideScreenWalker.Walk(screen, section.Items);
				foreach (var item in section.Items) {
					if (item.Key == null && item.GameObject != null)
						item.Key = item.GameObject.GetInstanceID().ToString();
				}
				if (section.Items.Count > 0)
					sections.Add(section);
			}
		}

		private static string GetFallbackTitle(SideScreenContent screen) {
			var lt = screen.GetComponentInChildren<LocText>(false);
			if (lt != null) {
				string text = lt.GetParsedText();
				if (SideScreenWalker.HasVisibleContent(text))
					return text;
			}
			return screen.GetType().Name;
		}

		internal static IEnumerable<SideScreenContent> GetActiveScreens(
				DetailsScreen ds, DetailsScreen.SidescreenTabTypes tabType) {
			List<DetailsScreen.SideScreenRef> sideScreens;
			try {
				sideScreens = Traverse.Create(ds)
					.Field<List<DetailsScreen.SideScreenRef>>("sideScreens").Value;
			} catch (System.Exception ex) {
				Util.Log.Warn($"SideTab: sideScreens read failed: {ex.Message}");
				yield break;
			}
			if (sideScreens == null) {
				Util.Log.Warn("SideTab: sideScreens field was null");
				yield break;
			}

			foreach (var r in sideScreens) {
				if (r.tab != tabType) continue;
				if (r.screenInstance == null) continue;
				if (!r.screenInstance.gameObject.activeSelf) continue;
				yield return r.screenInstance;
			}
		}
	}
}
