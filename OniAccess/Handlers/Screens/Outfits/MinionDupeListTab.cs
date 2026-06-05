using System.Collections.Generic;

using OniAccess.Speech;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens.Outfits {
	/// <summary>
	/// Tab 1 of MinionBrowserHandler: flat list of duplicant personalities
	/// or minion instances from MinionBrowserScreen.Config.items.
	///
	/// Enter on a dupe switches to the detail tab.
	/// Label shows "Name (PersonalityName)" when the dupe has been renamed.
	/// </summary>
	internal class MinionDupeListTab: BaseMenuHandler, IScreenTab {
		private readonly MinionBrowserHandler _parent;

		internal MinionDupeListTab(MinionBrowserHandler parent) : base(screen: null) {
			_parent = parent;
		}

		public string TabName => (string)STRINGS.UI.MINION_BROWSER_SCREEN.CATEGORY_HEADER;

		public override string DisplayName => TabName;

		public override IReadOnlyList<HelpEntry> HelpEntries => ListNavHelpEntries;

		// ========================================
		// IScreenTab
		// ========================================

		public void OnTabActivated(bool announce) {
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(label));
			}
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

		/// <summary>
		/// Reactivate the list tab, landing on the dupe that was being
		/// viewed in the detail tab.
		/// </summary>
		internal void OnTabActivatedOnDupe(bool announce, MinionBrowserScreen.GridItem dupe) {
			if (dupe != null)
				NavigateToDupe(dupe);
			if (announce)
				SpeechPipeline.SpeakInterrupt(TabName);
			if (ItemCount > 0) {
				string label = GetItemLabel(CurrentIndex);
				if (!string.IsNullOrEmpty(label))
					SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(label));
			}
		}

		// ========================================
		// BaseMenuHandler overrides
		// ========================================

		private MinionBrowserScreen.GridItem[] Items => _parent.BrowserScreen.Config.items;

		public override int ItemCount => Items.Length;

		public override string GetItemLabel(int index) {
			if (index < 0 || index >= Items.Length) return null;
			return GetDupeLabel(Items[index]);
		}

		public override void SpeakCurrentItem(string parentContext = null) {
			if (ItemCount == 0) return;
			if (CurrentIndex < 0 || CurrentIndex >= ItemCount) return;
			string text = GetItemLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakInterrupt(WidgetSpeech.ComposeLabel(text));
		}

		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= ItemCount) return;
			PlaySound("HUD_Click_Open");
			_parent.JumpToDetailTab(Items[CurrentIndex]);
		}

		// ========================================
		// HELPERS
		// ========================================

		private static string GetDupeLabel(MinionBrowserScreen.GridItem item) {
			string name = item.GetName();
			var personality = item.GetPersonality();
			string personalityName = personality.Name;

			string label;
			if (name != personalityName)
				label = string.Format(
					(string)STRINGS.ONIACCESS.WARDROBE.DUPE_RENAMED,
					name, personalityName);
			else
				label = name;

			string descKey = "STRINGS.ONIACCESS.DUPE_DESCRIPTIONS."
				+ personalityName.Replace("-", "_").ToUpper();
			if (Strings.TryGet(descKey, out var descEntry))
				label += ", " + descEntry.String;

			string desc = personality.description;
			if (!string.IsNullOrEmpty(desc))
				label += ", " + desc;

			return label;
		}

		private void NavigateToDupe(MinionBrowserScreen.GridItem dupe) {
			var items = Items;
			for (int i = 0; i < items.Length; i++) {
				if (items[i].Equals(dupe)) {
					CurrentIndex = i;
					_search.Clear();
					SuppressSearchThisFrame();
					return;
				}
			}
		}
	}
}
