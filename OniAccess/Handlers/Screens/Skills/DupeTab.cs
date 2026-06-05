using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Skills {
	/// <summary>
	/// Tab 1: flat list of all duplicants with type-ahead search.
	/// Enter selects a dupe and jumps to the Skills tab.
	/// </summary>
	internal class DupeTab: BaseMenuHandler, IScreenTab {
		private readonly SkillsScreenHandler _parent;

		internal DupeTab(SkillsScreenHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.ONIACCESS.SKILLS.DUPES_TAB;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }
			= new List<HelpEntry>(MenuHelpEntries) {
				new HelpEntry("Up/Down", STRINGS.ONIACCESS.HELP.NAVIGATE_ITEMS),
				new HelpEntry("Home/End", STRINGS.ONIACCESS.HELP.JUMP_FIRST_LAST),
				new HelpEntry("Enter", STRINGS.ONIACCESS.HELP.SELECT_ITEM),
			}.AsReadOnly();

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			CurrentIndex = 0;
			_search.Clear();
			SuppressSearchThisFrame();
			var dupes = GetDupeList();
			var selected = _parent.SelectedDupe;
			if (selected != null) {
				for (int i = 0; i < dupes.Count; i++) {
					if (dupes[i] == selected) {
						CurrentIndex = i;
						break;
					}
				}
			}
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (dupes.Count > 0 && CurrentIndex < dupes.Count)
				SpeechPipeline.SpeakQueued(
					WidgetSpeech.ComposeLabel(SkillsHelper.BuildDupeLabel(dupes[CurrentIndex])));
		}

		public void OnTabDeactivated() {
			_search.Clear();
		}

		public bool HandleInput() {
			return base.Tick();
		}

		public new bool HandleKeyDown(KButtonEvent e) {
			return base.HandleKeyDown(e);
		}

		// ========================================
		// BaseMenuHandler abstracts
		// ========================================

		public override int ItemCount => GetDupeList().Count;

		public override string GetItemLabel(int index) {
			var dupes = GetDupeList();
			if (index < 0 || index >= dupes.Count) return null;
			return dupes[index].GetProperName();
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			var dupes = GetDupeList();
			if (CurrentIndex < 0 || CurrentIndex >= dupes.Count) return;
			// Auto-select the dupe under the cursor
			_parent.SetSelectedDupe(dupes[CurrentIndex]);
			string label = SkillsHelper.BuildDupeLabel(dupes[CurrentIndex]);
			if (!string.IsNullOrEmpty(parentContext))
				label = parentContext + ", " + label;
			SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(label));
		}

		protected override void ActivateCurrentItem() {
			// Dupe is already selected by navigation; just jump to skills tab
			_parent.JumpToSkillsTab();
		}

		// ========================================
		// Data
		// ========================================

		private List<IAssignableIdentity> GetDupeList() {
			var list = new List<IAssignableIdentity>();
			foreach (var mi in Components.LiveMinionIdentities.Items)
				list.Add(mi);
			// Stored minions from sortableRows on the screen
			var screen = _parent.Screen as SkillsScreen;
			if (screen != null) {
				try {
					var rows = HarmonyLib.Traverse.Create(screen)
						.Field<List<SkillMinionWidget>>("sortableRows").Value;
					if (rows != null) {
						foreach (var widget in rows) {
							if (widget.assignableIdentity != null &&
								SkillsHelper.IsStored(widget.assignableIdentity)) {
								list.Add(widget.assignableIdentity);
							}
						}
					}
				} catch (System.Exception ex) {
					Util.Log.Warn($"DupeTab: failed to read sortableRows: {ex.Message}");
				}
			}
			list.Sort((a, b) => string.Compare(
				a.GetProperName(), b.GetProperName(),
				System.StringComparison.Ordinal));
			return list;
		}
	}
}
