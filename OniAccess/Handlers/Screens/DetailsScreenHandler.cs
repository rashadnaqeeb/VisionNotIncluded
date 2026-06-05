using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using OniAccess.Handlers.Screens.Details;
using OniAccess.Navigation;
using OniAccess.Speech;
using OniAccess.Util;
using OniAccess.Widgets;

namespace OniAccess.Handlers.Screens {
	/// <summary>
	/// Handler for the DetailsScreen (entity inspection panel), driven by the NavTree
	/// engine. The item tree is section headers (level 0), items within each section
	/// (level 1), and an item's children (level 2). Sections are the handler's stable
	/// model: tab readers populate them, SectionMerger keeps their order steady frame to
	/// frame, and the engine's path cursor survives the merge via ClampToTree. Tab
	/// cycling across informational and side screen tabs is managed here.
	///
	/// Lifecycle: Show-patch on DetailsScreen.OnShow(bool).
	/// The DetailsScreen is a persistent singleton that shows/hides rather than
	/// activating/deactivating, so KScreen.Activate patches skip it.
	/// </summary>
	public class DetailsScreenHandler: NavTreeHandler {
		private readonly IDetailTab[] _tabs;
		private readonly List<IDetailTab> _activeTabs = new List<IDetailTab>();
		private readonly List<int> _sectionStarts = new List<int>();
		private readonly List<DetailSection> _sections = new List<DetailSection>();
		private readonly Input.TextEditHelper _textEdit = new Input.TextEditHelper();
		private int _tabIndex;
		private int _sectionIndex;
		private GameObject _lastTarget;
		private bool _suppressDisplayName;
		private bool _pendingFirstSection;
		private bool _pendingSilentRebuild;
		private bool _pendingPreserveRebuild;
		private bool _pendingTabSpeech;
		private bool _pendingActivationSpeech;

		protected override SearchScope SearchScope => SearchScope.CurrentLevel;

		// Keep level-2 (storage contents) navigation and search inside the current
		// section, as the old index-model handler did. Sections and items stay global.
		protected override CrossingScope Crossing => CrossingScope.WithinGrandparent;

		protected override int StartDepth =>
			_tabIndex >= 0 && _tabIndex < _activeTabs.Count
				? _activeTabs[_tabIndex].StartLevel : 0;

		public override string DisplayName {
			get {
				if (_suppressDisplayName) return null;
				var ds = DetailsScreen.Instance;
				if (ds == null || ds.target == null)
					return STRINGS.ONIACCESS.HANDLERS.DETAILS_SCREEN;
				string entityName = DebrisNameHelper.GetDisplayName(ds.target);
				var resume = ds.target.GetComponent<MinionResume>();
				if (resume != null) {
					string hatName = null;
					if (resume.CurrentHat != null)
						foreach (var skill in Db.Get().Skills.resources)
							if (skill.hat == resume.CurrentHat) { hatName = skill.Name; break; }
					entityName = hatName != null
						? string.Format(STRINGS.ONIACCESS.DETAILS.DUPE_HAT_SUBTITLE,
							entityName, hatName, resume.GetSkillsSubtitle())
						: string.Format(STRINGS.ONIACCESS.DETAILS.DUPE_SUBTITLE,
							entityName, resume.GetSkillsSubtitle());
				}
				if (_tabIndex >= 0 && _tabIndex < _activeTabs.Count)
					return string.Format(STRINGS.ONIACCESS.DETAILS.ENTITY_TAB,
					entityName, _activeTabs[_tabIndex].DisplayName);
				return entityName;
			}
		}

		public override IReadOnlyList<HelpEntry> HelpEntries { get; }

		public DetailsScreenHandler(KScreen screen) : base(screen) {
			_tabs = BuildTabs();
			var list = new List<HelpEntry>();
			list.AddRange(DrillNavHelpEntries);
			list.Add(new HelpEntry("\\", STRINGS.ONIACCESS.HELP.COPY_SETTINGS));
			list.Add(new HelpEntry("Tab/Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_PANEL));
			list.Add(new HelpEntry("Ctrl+Tab/Ctrl+Shift+Tab", STRINGS.ONIACCESS.HELP.SWITCH_SECTION));
			HelpEntries = list.AsReadOnly();
		}

		// ========================================
		// TREE CONSTRUCTION
		// ========================================

		protected override IReadOnlyList<NavItem> BuildRoots() {
			var roots = new List<NavItem>(_sections.Count);
			for (int s = 0; s < _sections.Count; s++) {
				var section = _sections[s];
				roots.Add(new MenuNode(
					() => section.Header,
					children: () => SectionItems(section)));
			}
			return roots;
		}

		private static IReadOnlyList<NavItem> SectionItems(DetailSection section) {
			var items = new List<NavItem>(section.Items.Count);
			for (int i = 0; i < section.Items.Count; i++)
				items.Add(section.Items[i]);
			return items;
		}

		protected override string GetTooltip(NavItem item) =>
			item is Widget w ? WidgetOps.GetTooltipText(w) : null;

		protected override string FormatWithContext(string body, IReadOnlyList<NavItem> ancestors) {
			if (ancestors.Count == 0) return body;
			return string.Format(STRINGS.ONIACCESS.DETAILS.PARENT_ITEM, JoinAncestors(ancestors), body);
		}

		// ========================================
		// ACTIVATION
		// ========================================

		protected override void ActivateCurrentItem() {
			if (ItemCount == 0) return;
			RefreshSections();
			var w = Nav.Current() as Widget;
			if (w is ToggleWidget && Nav.Depth > 0) {
				ActivateLeafWidget(w);
				return;
			}
			if (Nav.CanDrill() && ShouldDrillOnActivate()) {
				Drill();
				return;
			}
			ActivateLeafWidget(Nav.Current() as Widget);
		}

		private void ActivateLeafWidget(Widget w) {
			if (w == null) return;

			if (!w.IsInteractable) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.FABRICATOR.UNAVAILABLE);
				return;
			}

			if (w is ButtonWidget bw) {
				bw.Activate();
				_pendingActivationSpeech = true;
				return;
			}

			if (w is ToggleWidget) {
				w.Activate();
				_pendingActivationSpeech = true;
				return;
			}

			if (w is SliderWidget) {
				SpeakCurrentItem();
				return;
			}

			if (w is TextInputWidget tiw) {
				var textField = tiw.GetTextField();
				if (textField != null) {
					if (!_textEdit.IsEditing)
						_textEdit.Begin(textField, onEnd: RebuildSections);
					else
						_textEdit.Confirm();
				}
				return;
			}

			if (w is DropdownWidget)
				CycleRadioGroup(w, 1);
		}

		// ========================================
		// UP/DOWN: REBUILD BEFORE NAVIGATING
		// ========================================

		protected override void NavigateNext() {
			RefreshSections();
			base.NavigateNext();
		}

		protected override void NavigatePrev() {
			RefreshSections();
			base.NavigatePrev();
		}

		protected override void Drill() {
			RefreshSections();
			base.Drill();
		}

		protected override void Back() {
			RefreshSections();
			base.Back();
		}

		// ========================================
		// LEFT/RIGHT: SLIDER ADJUSTMENT AT LEAF LEVEL
		// ========================================

		protected override void HandleLeftRight(int direction, int stepLevel) {
			if (Nav.Depth > 0) {
				RefreshSections();
				var w = Nav.Current() as Widget;
				if (w is SliderWidget) {
					AdjustSlider(w, direction, stepLevel);
					return;
				}
				if (w is PriorityWidget pw) {
					AdjustPriority(pw, direction);
					return;
				}
				if (w is DropdownWidget) {
					CycleRadioGroup(w, direction);
					return;
				}
			}
			base.HandleLeftRight(direction, stepLevel);
		}

		private void AdjustSlider(Widget w, int direction, int stepLevel) {
			var sw = (SliderWidget)w;
			bool changed = sw.Adjust(direction, stepLevel);

			// KSlider events only fire on mouse/keyboard input, not programmatic
			// value changes. Invoke onMove so side screens (e.g., CapacityControl)
			// sync related widgets like text fields.
			var slider = w.Component as KSlider;
			if (changed && slider != null) {
				try {
					Traverse.Create(slider).Field<System.Action>("onMove").Value?.Invoke();
				} catch (System.Exception ex) {
					Util.Log.Warn($"AdjustSlider: onMove invoke failed: {ex.Message}");
				}
			}

			PlaySliderSound(sw.GetBoundarySound(direction));

			if (changed) {
				RefreshSections();
				var fresh = Nav.Current() as Widget;
				if (fresh != null)
					SpeechPipeline.SpeakInterrupt(
						WidgetSpeech.ComposeLabel(WidgetOps.GetSpeechText(fresh)));
			}
		}

		private void AdjustPriority(PriorityWidget pw, int direction) {
			bool changed = pw.Adjust(direction, 0);
			if (changed) {
				try {
					PriorityScreen.PlayPriorityConfirmSound(
						pw.Prioritizable.GetMasterPriority());
				} catch (System.Exception ex) {
					Util.Log.Warn(
						$"AdjustPriority sound failed: {ex.Message}");
				}
				RefreshSections();
				var fresh = Nav.Current() as Widget;
				if (fresh != null)
					SpeechPipeline.SpeakInterrupt(
						WidgetSpeech.ComposeLabel(WidgetOps.GetSpeechText(fresh)));
			}
		}

		private void CycleRadioGroup(Widget w, int direction) {
			var members = w.Tag as List<SideScreenWalker.RadioMember>;
			if (members == null || members.Count == 0) return;

			int activeIndex = FindActiveRadioIndex(members);
			if (activeIndex < 0) return;

			int newIndex = ((activeIndex + direction) % members.Count + members.Count)
				% members.Count;
			var target = members[newIndex];
			if (target.OnSelect != null)
				target.OnSelect();
			else if (target.Toggle != null)
				target.Toggle.Click();
			else
				WidgetOps.ClickMultiToggle(target.MultiToggleRef);
			_pendingActivationSpeech = true;
		}

		private static void PlaySliderSound(string soundName) {
			BaseScreenHandler.PlaySound(soundName);
		}

		/// <summary>
		/// Find which RadioMember is currently active. KToggle members use
		/// IsToggleActive. MultiToggle members use CurrentState (state 1 =
		/// selected) unless they carry a NotificationType tag, where
		/// AlarmSideScreen's targetAlarm is matched instead (the alarm's
		/// default notificationType enum value is 0 which doesn't map to
		/// any valid type, leaving all toggles at state 1).
		/// </summary>
		private static int FindActiveRadioIndex(List<SideScreenWalker.RadioMember> members) {
			if (members[0].IsActive != null) {
				for (int i = 0; i < members.Count; i++) {
					if (members[i].IsActive()) return i;
				}
				return 0;
			}
			for (int i = 0; i < members.Count; i++) {
				if (members[i].Toggle != null && SideScreenWalker.IsToggleActive(members[i].Toggle))
					return i;
			}
			// AlarmSideScreen path: match by NotificationType tag.
			if (members[0].Tag is NotificationType) {
				var alarm = members[0].MultiToggleRef.GetComponentInParent<AlarmSideScreen>();
				if (alarm != null) {
					var activeType = alarm.targetAlarm.notificationType;
					for (int i = 0; i < members.Count; i++) {
						if (members[i].Tag is NotificationType nt && nt == activeType)
							return i;
					}
					return 0;
				}
			}
			// Generic MultiToggle path (FewOptionSideScreen, etc.):
			// state 1 = selected, state 0 = not selected.
			for (int i = 0; i < members.Count; i++) {
				if (members[i].MultiToggleRef != null && members[i].MultiToggleRef.CurrentState == 1)
					return i;
			}
			return -1;
		}

		// ========================================
		// KEY INTERCEPTION: TEXT EDITING
		// ========================================

		public override bool HandleKeyDown(KButtonEvent e) {
			if (_textEdit.HandleKeyDown(e))
				return true;
			if (base.HandleKeyDown(e))
				return true;
			if (e.TryConsume(Action.Escape)) {
				DetailsScreen.Instance.DeselectAndClose();
				return true;
			}
			return false;
		}

		// ========================================
		// SPEECH
		// ========================================

		public override void SpeakCurrentItem(string parentContext = null) {
			RefreshSections();
			AnnounceCurrent();
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		/// <summary>
		/// When set, the next DetailsScreenHandler activation suppresses all
		/// speech. Used by MoveToLocationHandler so the details screen
		/// reopens silently after the tool completes.
		/// </summary>
		internal static bool SuppressNextActivation;

		/// <summary>
		/// When set, the next same-target reactivation preserves the current
		/// navigation position instead of resetting to the first item.
		/// Used by RecipeQueueHandler so Escape/OK returns to the recipe
		/// the user came from.
		/// </summary>
		internal static bool PreserveNavigationOnReactivate;

		public override void OnActivate() {
			_pendingActivationSpeech = false;
			_pendingTabSpeech = false;
			_pendingSilentRebuild = false;
			_pendingPreserveRebuild = false;

			var currentTarget = DetailsScreen.Instance != null
				? DetailsScreen.Instance.target : null;
			bool sameTarget = currentTarget == _lastTarget && currentTarget != null;
			_lastTarget = currentTarget;
			RebuildActiveTabs(_lastTarget);
			if (!sameTarget)
				_tabIndex = 0;
			if (_tabIndex >= _activeTabs.Count) _tabIndex = 0;
			SwitchGameTab();
			if (SuppressNextActivation) {
				SuppressNextActivation = false;
				_pendingSilentRebuild = true;
			} else if (!sameTarget) {
				PreserveNavigationOnReactivate = false;
				_pendingFirstSection = true;
			} else if (PreserveNavigationOnReactivate) {
				PreserveNavigationOnReactivate = false;
				_pendingPreserveRebuild = true;
			} else {
				_pendingSilentRebuild = true;
			}
			_suppressDisplayName = true;
			var savedPath = new int[Nav.Path.Count];
			for (int i = 0; i < savedPath.Length; i++) savedPath[i] = Nav.Path[i];
			base.OnActivate();
			if (_pendingPreserveRebuild)
				Nav.SetPath(savedPath);
			_suppressDisplayName = false;
		}

		// ========================================
		// TICK: TARGET CHANGE DETECTION
		// ========================================

		public override bool Tick() {
			if (!_textEdit.IsEditing) {
				var titleBar = GetEditableTitleBar();
				if (titleBar != null
					&& titleBar.inputField != null
					&& titleBar.inputField.gameObject.activeInHierarchy) {
					_textEdit.Begin(titleBar.inputField, onEnd: RebuildSections);
				}
			}

			if (_textEdit.HandleTick())
				return false;

			if (_pendingActivationSpeech) {
				_pendingActivationSpeech = false;
				RebuildSections();
				SpeakCurrentItem();
			}

			if (_pendingTabSpeech) {
				_pendingTabSpeech = false;
				RebuildSections();
				ResetNavigation();
				SpeakFirstSection();
			}

			var currentTarget = DetailsScreen.Instance != null
				? DetailsScreen.Instance.target : null;

			if (currentTarget != _lastTarget) {
				_lastTarget = currentTarget;
				_pendingFirstSection = false;
				_pendingTabSpeech = false;
				if (currentTarget != null) {
					RebuildActiveTabs(currentTarget);
					_tabIndex = 0;
					SwitchGameTab();
					RebuildSections();
					ResetNavigation();

					SpeechPipeline.SpeakInterrupt(DisplayName);
					SpeakFirstSection();
				}
				return false;
			} else if (_pendingSilentRebuild) {
				RebuildSections();
				if (_sections.Count > 0) {
					_pendingSilentRebuild = false;
					ResetNavigation();
					return false;
				}
			} else if (_pendingPreserveRebuild) {
				RebuildSections();
				if (_sections.Count > 0) {
					_pendingPreserveRebuild = false;
					Nav.ClampToTree();
					SpeakCurrentItem();
					return false;
				}
			} else if (_pendingFirstSection) {
				RebuildSections();
				if (_sections.Count > 0) {
					_pendingFirstSection = false;
					SpeechPipeline.SpeakInterrupt(DisplayName);
					SpeakFirstSection();
					return false;
				}
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Backslash)
				&& !Input.InputUtil.AnyModifierHeld()) {
				ActivateCopySettings();
				return true;
			}

			if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab) && Input.InputUtil.CtrlHeld()) {
				AdvanceSection(Input.InputUtil.ShiftHeld() ? -1 : 1);
				return true;
			}

			return base.Tick();
		}

		private void ActivateCopySettings() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return;
			var target = ds.target;
			if (target == null) return;
			if (target.GetComponent<CopyBuildingSettings>() == null) {
				PlaySound("Negative");
				SpeechPipeline.SpeakInterrupt(
					(string)STRINGS.ONIACCESS.TOOLS.COPY_SETTINGS_UNAVAILABLE);
				return;
			}
			CopySettingsTool.Instance.SetSourceObject(target);
			PlayerController.Instance.ActivateTool(CopySettingsTool.Instance);
		}

		private EditableTitleBar GetEditableTitleBar() {
			var ds = DetailsScreen.Instance;
			if (ds == null) return null;
			return Traverse.Create(ds)
				.Field<EditableTitleBar>("TabTitle").Value;
		}

		// ========================================
		// TAB CYCLING
		// ========================================

		protected override void NavigateTabForward() {
			AdvanceTab(1);
		}

		protected override void NavigateTabBackward() {
			AdvanceTab(-1);
		}

		private void AdvanceTab(int direction) {
			if (_sectionStarts.Count == 0) return;

			int start = _sectionStarts[_sectionIndex];
			int end = _sectionIndex + 1 < _sectionStarts.Count
				? _sectionStarts[_sectionIndex + 1] : _activeTabs.Count;
			int sectionSize = end - start;
			if (sectionSize <= 1) return;

			int oldIndex = _tabIndex;
			int offset = ((_tabIndex - start + direction) % sectionSize + sectionSize)
				% sectionSize;
			_tabIndex = start + offset;

			bool wrapped = direction > 0
				? _tabIndex <= oldIndex
				: _tabIndex >= oldIndex;

			CommitTabSwitch(wrapped);
		}

		private void AdvanceSection(int direction) {
			if (_sectionStarts.Count <= 1) return;

			int oldSection = _sectionIndex;
			_sectionIndex = ((_sectionIndex + direction) % _sectionStarts.Count
				+ _sectionStarts.Count) % _sectionStarts.Count;
			_tabIndex = _sectionStarts[_sectionIndex];

			bool wrapped = direction > 0
				? _sectionIndex <= oldSection
				: _sectionIndex >= oldSection;

			CommitTabSwitch(wrapped);
		}

		private void CommitTabSwitch(bool wrapped) {
			SwitchGameTab();

			if (wrapped) PlaySound("HUD_Click");
			else PlaySound("HUD_Mouseover");

			SpeechPipeline.SpeakInterrupt(_activeTabs[_tabIndex].DisplayName);

			if (_activeTabs[_tabIndex].GameTabId == null) {
				_pendingTabSpeech = true;
			} else {
				RebuildSections();
				ResetNavigation();
				SpeakFirstSection();
			}
		}

		// ========================================
		// SECTION MANAGEMENT
		// ========================================

		private void RebuildSections() {
			_sections.Clear();
			var ds = DetailsScreen.Instance;
			if (ds == null || ds.target == null) return;
			if (_tabIndex < 0 || _tabIndex >= _activeTabs.Count) return;

			try {
				_activeTabs[_tabIndex].Populate(ds.target, _sections);
			} catch (System.Exception ex) {
				Util.Log.Error(
					$"DetailsScreenHandler: tab '{_activeTabs[_tabIndex].DisplayName}' " +
					$"Populate failed: {ex}");
			}
		}

		/// <summary>
		/// Merges fresh widget data into the existing sections, preserving
		/// navigation order. Matched items stay at their position with
		/// updated content; new items are inserted, gone items removed.
		/// The cursor is then clamped against the merged tree.
		/// </summary>
		private void RefreshSections() {
			var fresh = new List<DetailSection>();
			var ds = DetailsScreen.Instance;
			if (ds == null || ds.target == null) { Nav.ClampToTree(); return; }
			if (_tabIndex < 0 || _tabIndex >= _activeTabs.Count) { Nav.ClampToTree(); return; }

			try {
				_activeTabs[_tabIndex].Populate(ds.target, fresh);
			} catch (System.Exception ex) {
				Util.Log.Error(
					$"DetailsScreenHandler: tab '{_activeTabs[_tabIndex].DisplayName}' " +
					$"Populate failed: {ex}");
				Nav.ClampToTree();
				return;
			}

			SectionMerger.Merge(_sections, fresh);
			Nav.ClampToTree();
		}

		// ========================================
		// TAB MANAGEMENT
		// ========================================

		private void RebuildActiveTabs(GameObject target) {
			_activeTabs.Clear();
			if (target == null) return;

			Dictionary<string, MultiToggle> gameTabs = null;
			var ds = DetailsScreen.Instance;
			if (ds != null) {
				var tabHeader = Traverse.Create(ds)
					.Field<DetailTabHeader>("tabHeader").Value;
				if (tabHeader != null)
					gameTabs = Traverse.Create(tabHeader)
						.Field<Dictionary<string, MultiToggle>>("tabs").Value;
			}

			foreach (var tab in _tabs) {
				if (tab.GameTabId != null && gameTabs != null) {
					if (gameTabs.TryGetValue(tab.GameTabId, out var toggle)
							&& !toggle.gameObject.activeSelf)
						continue;
				} else if (!tab.IsAvailable(target)) {
					continue;
				}
				_activeTabs.Add(tab);
			}

			_sectionStarts.Clear();
			_sectionIndex = 0;
			int? lastKind = null;
			for (int i = 0; i < _activeTabs.Count; i++) {
				int kind = GetTabSectionKind(_activeTabs[i]);
				if (lastKind != kind) {
					_sectionStarts.Add(i);
					lastKind = kind;
				}
			}
		}

		/// <summary>
		/// Returns 0 for main game tabs, 1 for side screen tabs, 2 for actions.
		/// Used to group tabs into Ctrl+Tab sections.
		/// </summary>
		private static int GetTabSectionKind(IDetailTab tab) {
			if (tab is ActionsTab) return 2;
			if (tab.GameTabId != null) return 0;
			return 1;
		}

		/// <summary>
		/// Switch the game's visual tab to match our logical tab.
		/// For main tabs (non-null GameTabId), calls the game's ChangeTab.
		/// For all tabs, calls OnTabSelected() for tab-specific activation
		/// (side screen tabs click their game MultiToggle here).
		/// </summary>
		private void SwitchGameTab() {
			if (_tabIndex < 0 || _tabIndex >= _activeTabs.Count) return;

			_activeTabs[_tabIndex].OnTabSelected();

			var gameTabId = _activeTabs[_tabIndex].GameTabId;
			if (gameTabId == null) return;

			var ds = DetailsScreen.Instance;
			if (ds == null) return;

			var tabHeader = Traverse.Create(ds)
				.Field<DetailTabHeader>("tabHeader").Value;
			if (tabHeader == null) return;

			Traverse.Create(tabHeader).Method("ChangeTab", gameTabId).GetValue();
		}

		// ========================================
		// PRIVATE HELPERS
		// ========================================

		private void ResetNavigation() {
			Nav.Reset(StartDepth);
			_search.Clear();
			SuppressSearchThisFrame();
		}

		private void SpeakFirstSection() {
			if (_sections.Count == 0) {
				if (_tabIndex >= 0 && _tabIndex < _activeTabs.Count) {
					if (_activeTabs[_tabIndex] is ConfigSideTab)
						SpeechPipeline.SpeakQueued(
							(string)STRINGS.UI.UISIDESCREENS.NOCONFIG.LABEL);
					else if (_activeTabs[_tabIndex] is ErrandsSideTab)
						SpeechPipeline.SpeakQueued(
							(string)STRINGS.ONIACCESS.DETAILS.NO_ERRANDS);
					else if (_activeTabs[_tabIndex] is ActionsTab)
						SpeechPipeline.SpeakQueued(
							(string)STRINGS.ONIACCESS.DETAILS.NO_ACTIONS);
				}
				return;
			}
			if (string.IsNullOrEmpty(_sections[0].Header))
				return;
			string header = _sections[0].Header;
			bool headerIsTabName = _tabIndex >= 0 && _tabIndex < _activeTabs.Count
				&& string.Equals(header, _activeTabs[_tabIndex].DisplayName,
					System.StringComparison.OrdinalIgnoreCase);

			if (Nav.Depth > 0) {
				var items = _sections[0].Items;
				if (items.Count == 0) return;
				string item = WidgetOps.GetSpeechText(items[0]);
				if (string.IsNullOrWhiteSpace(item)) return;
				string label = headerIsTabName ? item
					: string.Format(STRINGS.ONIACCESS.DETAILS.HEADER_ITEM, header, item);
				SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(label));
			} else {
				if (!headerIsTabName)
					SpeechPipeline.SpeakQueued(WidgetSpeech.ComposeLabel(header));
			}
		}

		// ========================================
		// TAB CONFIGURATION
		// ========================================

		private static IDetailTab[] BuildTabs() {
			return new IDetailTab[] {
				// Main info tabs (match game's DetailTabHeader order).
				// Availability is determined by the game's tab toggle visibility,
				// not hardcoded predicates — see RebuildActiveTabs.
				new StatusTab(),
				new PersonalityTab(),
				new ChoresTab(),
				new PropertiesTab(),

				// Side screen tabs (null gameTabId — availability via SidescreenTab.IsVisible).
				new ConfigSideTab(),
				new ErrandsSideTab(),
				new MaterialTab(),
				new BlueprintTab(),

				// Actions section (null gameTabId — always available).
				new ActionsTab(),
			};
		}
	}
}
