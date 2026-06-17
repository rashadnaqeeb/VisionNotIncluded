using System.Collections.Generic;
using HarmonyLib;

using OniAccess.Widgets;
namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the main menu (MainMenu class).
	/// MainMenu inherits directly from KScreen (NOT KButtonMenu), so we cannot
	/// use the buttons array pattern. Instead, we walk the buttonParent transform
	/// to discover KButton instances with LocText labels.
	///
	/// Also checks the Button_ResumeGame serialized field, which is separate from
	/// the MakeButton buttons and appears only when a save file exists.
	///
	/// Three sections reachable via Tab/Shift+Tab:
	/// - Buttons: the main menu button list (Resume, New Game, Load, etc.)
	/// - DLC: one logo per non-cosmetic DLC, showing name + ownership/activation status
	/// - News: MOTD boxes with headlines from Klei's server (async-loaded)
	/// </summary>
	public class MainMenuHandler: BaseWidgetHandler {
		private const int SectionButtons = 0;
		private const int SectionDLC = 1;
		private const int SectionNews = 2;
		private const int SectionCount = 3;

		private int _currentSection;

		private static readonly string[] MotdBoxFields = { "boxA", "boxB", "boxC" };

		public override string DisplayName => STRINGS.ONIACCESS.HANDLERS.MAIN_MENU;

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public MainMenuHandler(KScreen screen) : base(screen) {
			HelpEntries = BuildHelpEntries(new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_PANEL));
		}

		// ========================================
		// WIDGET DISCOVERY
		// ========================================

		public override bool DiscoverWidgets(KScreen screen) {
			_widgets.Clear();

			switch (_currentSection) {
				case SectionButtons: DiscoverButtonWidgets(screen); break;
				case SectionDLC: DiscoverDLCWidgets(screen); break;
				case SectionNews: DiscoverNewsWidgets(screen); break;
			}

			Util.Log.Debug($"MainMenuHandler.DiscoverWidgets: section={_currentSection}, {_widgets.Count} widgets");
			return true;
		}

		/// <summary>
		/// Discover the main menu buttons (Resume Game + MakeButton-created buttons).
		/// </summary>
		private void DiscoverButtonWidgets(KScreen screen) {
			// MainMenu has a separate Button_ResumeGame field (shown if a save exists)
			var resumeButton = Traverse.Create(screen).Field("Button_ResumeGame")
				.GetValue<KButton>();
			if (resumeButton != null && resumeButton.gameObject.activeInHierarchy
				&& resumeButton.isInteractable) {
				var resumeLabel = resumeButton.GetComponentInChildren<LocText>();
				string resumeText = resumeLabel != null ? resumeLabel.text : (string)STRINGS.UI.FRONTEND.MAINMENU.RESUMEGAME;
				_widgets.Add(new ButtonWidget {
					Label = resumeText,
					Component = resumeButton,
					GameObject = resumeButton.gameObject
				});
			}

			// Walk buttonParent children for MakeButton-created buttons
			// buttonParent is a GameObject (not Transform) per decompiled source
			var buttonParentGO = Traverse.Create(screen).Field("buttonParent")
				.GetValue<UnityEngine.GameObject>();
			UnityEngine.Transform parent = buttonParentGO != null
				? buttonParentGO.transform
				: screen.transform;

			for (int i = 0; i < parent.childCount; i++) {
				var child = parent.GetChild(i);
				if (child == null || !child.gameObject.activeInHierarchy) continue;

				var kbutton = child.GetComponent<KButton>();
				if (kbutton == null || !kbutton.isInteractable) continue;

				// Skip if this is the resume button (already added above)
				if (resumeButton != null && kbutton == resumeButton) continue;

				var locText = kbutton.GetComponentInChildren<LocText>();
				if (locText == null || string.IsNullOrEmpty(locText.text)) continue;

				_widgets.Add(new ButtonWidget {
					Label = locText.text,
					Component = kbutton,
					GameObject = kbutton.gameObject
				});
			}
		}

		/// <summary>
		/// Discover DLC logo entries. Each has a name (from DlcManager) and
		/// ownership/activation status.
		///
		/// The game serializes only logoDLC1 (Spaced Out); every other non-cosmetic
		/// pack is instantiated into logoGroup in DLC_PACKS order, with the whole
		/// sibling list optionally reversed. We rebuild that same ordering to pair
		/// each DlcInfo with its live widget (see MainMenu.BuildDlcLogoWidgets).
		/// </summary>
		private void DiscoverDLCWidgets(KScreen screen) {
			var screenTraverse = Traverse.Create(screen);
			var logoDLC1 = screenTraverse.Field("logoDLC1").GetValue<HierarchyReferences>();
			var logoGroupGO = screenTraverse.Field("logoGroup").GetValue<UnityEngine.GameObject>();

			// Collect the pack widgets from logoGroup, normalized to DLC_PACKS order
			// (Spaced-Out-first, before the optional sibling reversal the game applies).
			var packWidgets = new List<HierarchyReferences>();
			if (logoGroupGO != null) {
				var parent = logoGroupGO.transform;
				var children = new List<HierarchyReferences>();
				for (int i = 0; i < parent.childCount; i++) {
					var hr = parent.GetChild(i).GetComponent<HierarchyReferences>();
					if (hr != null && hr.gameObject.activeInHierarchy)
						children.Add(hr);
				}
				if (screenTraverse.Field("reverseDlcLogoOrder").GetValue<bool>())
					children.Reverse();
				foreach (var hr in children)
					if (hr != logoDLC1) packWidgets.Add(hr);
			}

			AddDLCWidget(DlcManager.EXPANSION1_INFO, logoDLC1);

			int packIndex = 0;
			foreach (var info in DlcManager.DLC_PACKS.Values) {
				if (info.isCosmetic) continue;
				var widget = packIndex < packWidgets.Count ? packWidgets[packIndex] : null;
				packIndex++;
				AddDLCWidget(info, widget);
			}

			if (packIndex != packWidgets.Count)
				Util.Log.Warn($"MainMenuHandler: {packWidgets.Count} DLC pack widgets but {packIndex} non-cosmetic packs; pairing may be off");
		}

		/// <summary>
		/// Add a navigable entry for one DLC, pairing its DlcManager info (name +
		/// status) with the live widget's MultiToggle for activation.
		/// </summary>
		private void AddDLCWidget(DlcManager.DlcInfo info, HierarchyReferences widget) {
			if (widget == null || !widget.gameObject.activeInHierarchy) return;

			string name = DlcManager.GetDlcTitleNoFormatting(info.id);
			string status = GetDlcStatus(info.id);

			_widgets.Add(new LabelWidget {
				Label = $"{name}, {status}",
				Component = widget.GetReference<MultiToggle>("multitoggle"),
				GameObject = widget.gameObject
			});
		}

		/// <summary>
		/// Get the DLC status string. Uses our own strings because the game's
		/// CONTENT_OWNED_NOTINSTALLED_LABEL is an empty string.
		/// </summary>
		private static string GetDlcStatus(string dlcId) {
			if (DlcManager.IsContentSubscribed(dlcId))
				return STRINGS.ONIACCESS.DLC.ACTIVE;
			if (DlcManager.IsContentOwned(dlcId))
				return STRINGS.ONIACCESS.DLC.OWNED_NOT_ACTIVE;
			return STRINGS.ONIACCESS.DLC.NOT_OWNED;
		}

		/// <summary>
		/// Discover MOTD news boxes. These are fetched async from Klei's server,
		/// so boxes may not be active yet (data still loading).
		/// </summary>
		private void DiscoverNewsWidgets(KScreen screen) {
			var motd = Traverse.Create(screen).Field("motd").GetValue<object>();
			if (motd == null) return;

			var motdTraverse = Traverse.Create(motd);

			foreach (var boxField in MotdBoxFields) {
				var box = motdTraverse.Field(boxField).GetValue<MotdBox>();
				if (box == null || !box.gameObject.activeInHierarchy) continue;

				var boxTraverse = Traverse.Create(box);
				var headerLabel = boxTraverse.Field("headerLabel").GetValue<LocText>();
				var imageLabel = boxTraverse.Field("imageLabel").GetValue<LocText>();

				// Use GetParsedText() instead of .text because SetText() updates
				// TMP's internal char buffer but not m_text.
				string header = headerLabel != null ? headerLabel.GetParsedText() : null;
				if (string.IsNullOrEmpty(header)) continue;

				string body = null;
				if (imageLabel != null && imageLabel.gameObject.activeInHierarchy)
					body = imageLabel.GetParsedText();

				string label = !string.IsNullOrEmpty(body)
					? $"{header}. {body}"
					: header;

				_widgets.Add(new LabelWidget {
					Label = label,
					Component = box,
					GameObject = box.gameObject
				});
			}
		}

		// ========================================
		// TAB NAVIGATION
		// ========================================

		protected override void NavigateTabForward() {
			_currentSection = (_currentSection + 1) % SectionCount;
			if (_currentSection == 0) PlaySound("HUD_Click");
			RediscoverForCurrentSection();
		}

		protected override void NavigateTabBackward() {
			int prev = _currentSection;
			_currentSection = (_currentSection - 1 + SectionCount) % SectionCount;
			if (_currentSection == SectionCount - 1 && prev == 0) PlaySound("HUD_Click");
			RediscoverForCurrentSection();
		}

		private void RediscoverForCurrentSection() {
			DiscoverWidgets(_screen);
			string sectionName = GetSectionName(_currentSection);
			Speech.SpeechPipeline.SpeakInterrupt(sectionName);
			if (_widgets.Count > 0) {
				CurrentIndex = 0;
				Speech.SpeechPipeline.SpeakQueued(GetWidgetSpeechText(_widgets[0]));
			} else if (_currentSection == SectionNews) {
				Speech.SpeechPipeline.SpeakQueued(STRINGS.ONIACCESS.PANELS.NO_NEWS);
			}
		}

		private static string GetSectionName(int section) {
			switch (section) {
				case SectionDLC: return STRINGS.ONIACCESS.PANELS.DLC;
				case SectionNews: return STRINGS.ONIACCESS.PANELS.NEWS;
				default: return STRINGS.ONIACCESS.PANELS.BUTTONS;
			}
		}

		// ========================================
		// WIDGET INTERACTION
		// ========================================

		/// <summary>
		/// DLC section: fire OnPointerClick on the widget's MultiToggle to trigger
		/// the game-wired onClick delegate. Spaced Out owned: opens activate/deactivate
		/// dialog. Spaced Out not owned: opens store. Other packs: open store page.
		/// News section: click the URLOpenFunction's triggerButton to open in browser.
		/// Buttons section: default KButton.SignalClick behavior.
		/// </summary>
		protected override void ActivateCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _widgets.Count) return;
			var widget = _widgets[CurrentIndex];

			if (_currentSection == SectionDLC) {
				var multiToggle = widget.Component as MultiToggle;
				if (multiToggle != null)
					ClickMultiToggle(multiToggle);
				return;
			}

			if (_currentSection == SectionNews) {
				var box = widget.Component as MotdBox;
				if (box != null) {
					var urlOpener = Traverse.Create(box).Field("urlOpener")
						.GetValue<URLOpenFunction>();
					if (urlOpener != null) {
						var triggerButton = Traverse.Create(urlOpener).Field("triggerButton")
							.GetValue<KButton>();
						if (triggerButton != null)
							ClickButton(triggerButton);
					}
				}
				return;
			}

			base.ActivateCurrentItem();
		}

		// ========================================
		// WIDGET VALIDITY
		// ========================================

	}
}
